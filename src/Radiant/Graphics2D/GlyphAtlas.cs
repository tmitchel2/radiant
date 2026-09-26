using System;
using System.Collections.Generic;
using System.Numerics;
using Radiant.Text;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D;

/// <summary>
/// Glyphs rasterized on demand into single-channel coverage textures, for text drawn at its pixel
/// size. A glyph is keyed by its font instance, id, device pixel size and horizontal subpixel
/// position (quarters of a pixel), so text lands on the pixel grid the way it was rasterized for.
/// <para>
/// Pages are packed in shelves: rows as tall as their tallest glyph, filled left to right. Each
/// glyph keeps a one-texel blank border, so sampling between texels never picks up a neighbour.
/// When the atlas outgrows <see cref="MaxPages"/>, it is emptied at the start of the next frame
/// and glyphs are rasterized again as they're drawn.
/// </para>
/// </summary>
internal sealed unsafe class GlyphAtlas : IDisposable
{
    /// <summary>A page's width and height in texels.</summary>
    public const int PageSize = 1024;

    /// <summary>How many pages the atlas grows to before it is emptied.</summary>
    public const int MaxPages = 4;

    private readonly WebGPU _wgpu;
    private readonly Device* _device;
    private readonly Queue* _queue;
    private readonly BindGroupLayout* _layout;
    private readonly Buffer* _params;
    private readonly ulong _paramsSize;
    private readonly Sampler* _sampler;
    private readonly List<Page> _pages = [];
    private readonly Dictionary<GlyphKey, AtlasGlyph> _glyphs = [];

    public GlyphAtlas(WebGPU wgpu, Device* device, Queue* queue, BindGroupLayout* layout, Buffer* parameters, ulong parametersSize)
    {
        _wgpu = wgpu;
        _device = device;
        _queue = queue;
        _layout = layout;
        _params = parameters;
        _paramsSize = parametersSize;
        var samplerDescriptor = new SamplerDescriptor
        {
            AddressModeU = AddressMode.ClampToEdge,
            AddressModeV = AddressMode.ClampToEdge,
            AddressModeW = AddressMode.ClampToEdge,
            MagFilter = FilterMode.Linear,
            MinFilter = FilterMode.Linear,
            MipmapFilter = MipmapFilterMode.Nearest,
            LodMinClamp = 0,
            LodMaxClamp = 0,
            MaxAnisotropy = 1,
        };
        _sampler = _wgpu.DeviceCreateSampler(_device, in samplerDescriptor);
    }

    /// <summary>The number of pages in use.</summary>
    public int PageCount => _pages.Count;

    /// <summary>The number of glyphs cached.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>A glyph's place in the atlas, rasterizing and uploading it the first time it's asked for.</summary>
    /// <param name="font">The font instance the glyph is from.</param>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="size">The size in device pixels (the em).</param>
    /// <param name="subpixel">Where the pen sits within its pixel, in quarters: 0 to 3.</param>
    public AtlasGlyph Get(FontInstance font, uint glyph, float size, int subpixel)
    {
        var key = new GlyphKey(font, glyph, size, (byte)subpixel);
        if (!_glyphs.TryGetValue(key, out var placed))
        {
            var bitmap = GlyphRasterizer.Rasterize(font.GetOutline(glyph), font.Scale(size), new Vector2(subpixel / 4f, 0f));
            placed = Place(bitmap);
            _glyphs[key] = placed;
        }
        return placed;
    }

    /// <summary>The bind group that samples a page.</summary>
    public IntPtr BindGroup(int page) => (IntPtr)_pages[page].BindGroup;

    /// <summary>
    /// Empties the atlas if it has outgrown <see cref="MaxPages"/>. Call between frames: a frame's
    /// vertices point into the pages, so they can't be emptied while it's being recorded.
    /// </summary>
    public void TrimIfFull()
    {
        if (_pages.Count <= MaxPages)
        {
            return;
        }
        _glyphs.Clear();
        for (var i = 1; i < _pages.Count; i++)
        {
            _pages[i].Dispose(_wgpu);
        }
        _pages.RemoveRange(1, _pages.Count - 1);
        _pages[0].Clear();
    }

    public void Dispose()
    {
        foreach (var page in _pages)
        {
            page.Dispose(_wgpu);
        }
        _pages.Clear();
        _glyphs.Clear();
        if (_sampler != null)
        {
            _wgpu.SamplerRelease(_sampler);
        }
    }

    private AtlasGlyph Place(GlyphBitmap bitmap)
    {
        if (bitmap.IsEmpty)
        {
            return AtlasGlyph.Empty;
        }
        var slotWidth = bitmap.Width + 2;
        var slotHeight = bitmap.Height + 2;
        if (slotWidth > PageSize || slotHeight > PageSize)
        {
            // Too big for a page (text hundreds of pixels tall): nothing is drawn.
            return AtlasGlyph.Empty;
        }

        if (_pages.Count == 0 || !_pages[^1].TryAllocate(slotWidth, slotHeight, out var x, out var y))
        {
            _pages.Add(CreatePage());
            _pages[^1].TryAllocate(slotWidth, slotHeight, out x, out y);
        }
        var pageIndex = _pages.Count - 1;
        Upload(_pages[pageIndex], bitmap, x, y, slotWidth, slotHeight);
        return new AtlasGlyph(pageIndex, x + 1, y + 1, bitmap.Width, bitmap.Height, bitmap.Left, bitmap.Top);
    }

    private void Upload(Page page, GlyphBitmap bitmap, int x, int y, int width, int height)
    {
        // The glyph with its blank border, so whatever the slot held before is cleared around it.
        var texels = new byte[width * height];
        var coverage = bitmap.Coverage;
        for (var row = 0; row < bitmap.Height; row++)
        {
            coverage.Slice(row * bitmap.Width, bitmap.Width).CopyTo(texels.AsSpan((row + 1) * width + 1));
        }
        var destination = new ImageCopyTexture
        {
            Texture = page.Texture,
            MipLevel = 0,
            Origin = new Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
            Aspect = TextureAspect.All,
        };
        var layout = new TextureDataLayout { Offset = 0, BytesPerRow = (uint)width, RowsPerImage = (uint)height };
        var size = new Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
        fixed (byte* data = texels)
        {
            _wgpu.QueueWriteTexture(_queue, in destination, data, (nuint)texels.Length, in layout, in size);
        }
    }

    private Page CreatePage()
    {
        var descriptor = new TextureDescriptor
        {
            Size = new Extent3D { Width = PageSize, Height = PageSize, DepthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = TextureDimension.Dimension2D,
            Format = TextureFormat.R8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
        };
        var texture = _wgpu.DeviceCreateTexture(_device, in descriptor);
        var view = _wgpu.TextureCreateView(texture, null);

        var entries = stackalloc BindGroupEntry[3];
        entries[0] = new BindGroupEntry { Binding = 0, Sampler = _sampler };
        entries[1] = new BindGroupEntry { Binding = 1, TextureView = view };
        entries[2] = new BindGroupEntry { Binding = 2, Buffer = _params, Offset = 0, Size = _paramsSize };
        var bindGroupDescriptor = new BindGroupDescriptor { Layout = _layout, EntryCount = 3, Entries = entries };
        var bindGroup = _wgpu.DeviceCreateBindGroup(_device, in bindGroupDescriptor);
        return new Page(texture, view, bindGroup);
    }

    /// <summary>A cache key: the same glyph at another size or subpixel position is another bitmap.</summary>
    private readonly record struct GlyphKey(FontInstance Font, uint Glyph, float Size, byte Subpixel);

    /// <summary>One texture of the atlas and its shelves.</summary>
    private sealed class Page(Texture* texture, TextureView* view, BindGroup* bindGroup)
    {
        private readonly List<Shelf> _shelves = [];
        private int _nextShelfY;

        public Texture* Texture { get; } = texture;

        public TextureView* View { get; } = view;

        public BindGroup* BindGroup { get; } = bindGroup;

        /// <summary>
        /// Finds room for a slot: on the first shelf tall enough (but not wastefully taller) with
        /// space left, or on a new shelf below the last. False when the page is full.
        /// </summary>
        public bool TryAllocate(int width, int height, out int x, out int y)
        {
            for (var i = 0; i < _shelves.Count; i++)
            {
                var shelf = _shelves[i];
                if (height <= shelf.Height && shelf.Height <= height + height / 2 + 2 && shelf.X + width <= PageSize)
                {
                    x = shelf.X;
                    y = shelf.Y;
                    _shelves[i] = shelf with { X = shelf.X + width };
                    return true;
                }
            }
            if (_nextShelfY + height <= PageSize && width <= PageSize)
            {
                x = 0;
                y = _nextShelfY;
                _shelves.Add(new Shelf(_nextShelfY, height, width));
                _nextShelfY += height;
                return true;
            }
            x = y = 0;
            return false;
        }

        /// <summary>Forgets every allocation; the texels are overwritten as glyphs are placed again.</summary>
        public void Clear()
        {
            _shelves.Clear();
            _nextShelfY = 0;
        }

        public void Dispose(WebGPU wgpu)
        {
            wgpu.BindGroupRelease(BindGroup);
            wgpu.TextureViewRelease(View);
            wgpu.TextureRelease(Texture);
        }
    }

    /// <summary>A row of a page: its top, height, and how far along it is filled.</summary>
    private readonly record struct Shelf(int Y, int Height, int X);
}
