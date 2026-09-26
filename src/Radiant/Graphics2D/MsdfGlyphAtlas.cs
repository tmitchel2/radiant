using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Radiant.Text;
using Radiant.Text.Msdf;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D;

/// <summary>
/// Glyphs' multi-channel signed distance fields, generated at runtime (<see cref="MsdfGenerator"/>)
/// into RGBA textures, for text drawn at any size or under any transform. Unlike the coverage
/// atlas, a glyph is generated once, at <see cref="EmSize"/>, and keyed only by its font instance
/// and id: the same field is scaled to every size and angle it is drawn at.
/// <para>
/// Pages are packed in shelves, as <see cref="GlyphAtlas"/> packs its pages, and each glyph keeps a
/// one-texel blank border (zero in every channel: far outside), so sampling between texels never
/// picks up a neighbour. Each page has its own bind group for the MSDF pipeline, whose parameters
/// carry <see cref="Range"/>, the distance range in texels. When the atlas outgrows
/// <see cref="MaxPages"/>, it is emptied at the start of the next frame and glyphs are generated
/// again as they're drawn.
/// </para>
/// </summary>
internal sealed unsafe class MsdfGlyphAtlas : IDisposable
{
    /// <summary>A page's width and height in texels, unless a test asks for smaller pages.</summary>
    public const int DefaultPageSize = 1024;

    /// <summary>How many pages the atlas grows to before it is emptied.</summary>
    public const int MaxPages = 4;

    /// <summary>
    /// The em size, in texels, fields are generated at. At 40 a Latin glyph is about 30 × 40
    /// texels (the size the baked atlases use), enough for sharp display text, and takes about a
    /// millisecond to generate.
    /// </summary>
    public const float EmSize = 40f;

    /// <summary>
    /// The distance range in texels. Drawn at a quarter of <see cref="EmSize"/> (10 px text) it
    /// still spans one and a half screen pixels, which the edge's anti-aliasing needs.
    /// </summary>
    public const float Range = 6f;

    /// <summary>The size of the parameters uniform: a <c>vec4&lt;f32&gt;</c> whose x is <see cref="Range"/>.</summary>
    private const ulong ParamsSize = 16;

    // Generating several glyphs at once is spread over threads from this many on.
    private const int ParallelThreshold = 4;

    private readonly WebGPU _wgpu;
    private readonly Device* _device;
    private readonly Queue* _queue;
    private readonly BindGroupLayout* _layout;
    private readonly Buffer* _params;
    private readonly Sampler* _sampler;
    private readonly List<Page> _pages = [];
    private readonly Dictionary<GlyphKey, AtlasGlyph> _glyphs = [];
    private readonly List<GlyphKey> _missing = [];

    public MsdfGlyphAtlas(WebGPU wgpu, Device* device, Queue* queue, BindGroupLayout* layout, int pageSize = DefaultPageSize)
    {
        PageSize = pageSize;
        _wgpu = wgpu;
        _device = device;
        _queue = queue;
        _layout = layout;
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

        var descriptor = new BufferDescriptor { Size = ParamsSize, Usage = BufferUsage.Uniform | BufferUsage.CopyDst, MappedAtCreation = false };
        _params = _wgpu.DeviceCreateBuffer(_device, in descriptor);
        var values = stackalloc float[4] { Range, 0f, 0f, 0f };
        _wgpu.QueueWriteBuffer(_queue, _params, 0, values, (nuint)ParamsSize);
    }

    /// <summary>A page's width and height in texels.</summary>
    public int PageSize { get; }

    /// <summary>The number of pages in use.</summary>
    public int PageCount => _pages.Count;

    /// <summary>The number of glyphs cached.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>
    /// Generates and uploads, together, the glyphs of a run that aren't in the atlas yet. A
    /// paragraph drawn for the first time needs many glyphs at once, so they are generated on
    /// several threads; <see cref="Get"/> then finds them all.
    /// </summary>
    public void Prepare(FontInstance font, IReadOnlyList<uint> glyphs)
    {
        _missing.Clear();
        foreach (var glyph in glyphs)
        {
            var key = new GlyphKey(font, glyph);
            if (!_glyphs.ContainsKey(key) && !_missing.Contains(key))
            {
                _missing.Add(key);
            }
        }
        if (_missing.Count < ParallelThreshold)
        {
            return; // Get generates the odd glyph itself
        }
        // Outlines are read on this thread (HarfBuzz and the outline cache); only the fields are
        // generated in parallel, from immutable outlines.
        var outlines = new GlyphOutline[_missing.Count];
        for (var i = 0; i < outlines.Length; i++)
        {
            outlines[i] = font.GetOutline(_missing[i].Glyph);
        }
        var fields = new MsdfBitmap[outlines.Length];
        var scale = font.Scale(EmSize);
        Parallel.For(0, outlines.Length, i => fields[i] = MsdfGenerator.Generate(outlines[i], scale, Range));
        for (var i = 0; i < fields.Length; i++)
        {
            _glyphs[_missing[i]] = Place(fields[i]);
        }
    }

    /// <summary>A glyph's place in the atlas, generating and uploading its field the first time it's asked for.</summary>
    /// <param name="font">The font instance the glyph is from.</param>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>Where it is, in texels, and where it goes relative to the pen in texels at <see cref="EmSize"/>.</returns>
    public AtlasGlyph Get(FontInstance font, uint glyph)
    {
        var key = new GlyphKey(font, glyph);
        if (!_glyphs.TryGetValue(key, out var placed))
        {
            placed = Place(MsdfGenerator.Generate(font.GetOutline(glyph), font.Scale(EmSize), Range));
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
        if (_params != null)
        {
            _wgpu.BufferRelease(_params);
        }
        if (_sampler != null)
        {
            _wgpu.SamplerRelease(_sampler);
        }
    }

    private AtlasGlyph Place(MsdfBitmap field)
    {
        if (field.IsEmpty)
        {
            return AtlasGlyph.Empty;
        }
        var slotWidth = field.Width + 2;
        var slotHeight = field.Height + 2;
        if (slotWidth > PageSize || slotHeight > PageSize)
        {
            return AtlasGlyph.Empty;
        }

        if (_pages.Count == 0 || !_pages[^1].TryAllocate(slotWidth, slotHeight, out var x, out var y))
        {
            _pages.Add(CreatePage());
            _pages[^1].TryAllocate(slotWidth, slotHeight, out x, out y);
        }
        var pageIndex = _pages.Count - 1;
        Upload(_pages[pageIndex], field, x, y, slotWidth, slotHeight);
        return new AtlasGlyph(pageIndex, x + 1, y + 1, field.Width, field.Height, field.Left, field.Top);
    }

    private void Upload(Page page, MsdfBitmap field, int x, int y, int width, int height)
    {
        // The field with its blank border, so whatever the slot held before is cleared around it.
        var texels = new byte[width * height * 4];
        var pixels = field.Pixels;
        var rowBytes = field.Width * 4;
        for (var row = 0; row < field.Height; row++)
        {
            pixels.Slice(row * rowBytes, rowBytes).CopyTo(texels.AsSpan(((row + 1) * width + 1) * 4));
        }
        var destination = new ImageCopyTexture
        {
            Texture = page.Texture,
            MipLevel = 0,
            Origin = new Origin3D { X = (uint)x, Y = (uint)y, Z = 0 },
            Aspect = TextureAspect.All,
        };
        var layout = new TextureDataLayout { Offset = 0, BytesPerRow = (uint)(width * 4), RowsPerImage = (uint)height };
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
            Size = new Extent3D { Width = (uint)PageSize, Height = (uint)PageSize, DepthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = TextureDimension.Dimension2D,
            // Distances, not colours: a linear format, so they are sampled as stored.
            Format = TextureFormat.Rgba8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
        };
        var texture = _wgpu.DeviceCreateTexture(_device, in descriptor);
        var view = _wgpu.TextureCreateView(texture, null);

        var entries = stackalloc BindGroupEntry[3];
        entries[0] = new BindGroupEntry { Binding = 0, Sampler = _sampler };
        entries[1] = new BindGroupEntry { Binding = 1, TextureView = view };
        entries[2] = new BindGroupEntry { Binding = 2, Buffer = _params, Offset = 0, Size = ParamsSize };
        var bindGroupDescriptor = new BindGroupDescriptor { Layout = _layout, EntryCount = 3, Entries = entries };
        var bindGroup = _wgpu.DeviceCreateBindGroup(_device, in bindGroupDescriptor);
        return new Page(texture, view, bindGroup, PageSize);
    }

    /// <summary>A cache key: one field per glyph of a font instance, whatever size it's drawn at.</summary>
    private readonly record struct GlyphKey(FontInstance Font, uint Glyph);

    /// <summary>One texture of the atlas and its shelves.</summary>
    private sealed class Page(Texture* texture, TextureView* view, BindGroup* bindGroup, int size)
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
                if (height <= shelf.Height && shelf.Height <= height + height / 2 + 2 && shelf.X + width <= size)
                {
                    x = shelf.X;
                    y = shelf.Y;
                    _shelves[i] = shelf with { X = shelf.X + width };
                    return true;
                }
            }
            if (_nextShelfY + height <= size && width <= size)
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
