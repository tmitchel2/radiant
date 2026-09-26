using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Radiant.Text;
using Radiant.Text.Slug;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D;

/// <summary>
/// Every glyph drawn with Slug, prepared once per font instance and glyph (a prepared glyph serves
/// every size and transform) and kept in two storage buffers the fragment shader reads: the curves
/// and the bands (see <see cref="SlugGlyph"/>). Glyphs are appended on the CPU as they are first
/// drawn and uploaded at the end of the frame, only the new tail unless a buffer has to grow.
/// <para>
/// Storage buffers rather than textures: WebGPU reads them from a fragment shader as plain arrays,
/// so there is no texture width to wrap indexes at, and curves keep full 32-bit precision. When the
/// data outgrows <see cref="MaxBytes"/> (text in very many font instances), everything is dropped
/// at the start of the next frame and glyphs are prepared again as they are drawn.
/// </para>
/// </summary>
internal sealed unsafe class SlugGlyphCache : IDisposable
{
    /// <summary>How big the curve and band data may grow together before it is emptied.</summary>
    public const int MaxBytes = 32 << 20;

    // The size of the parameters uniform: x = the text gamma (Renderer2D.TextGamma).
    public const ulong ParamsSize = 16;

    private const ulong MinBufferBytes = 64 << 10;

    private readonly WebGPU _wgpu;
    private readonly Device* _device;
    private readonly Queue* _queue;
    private readonly BindGroupLayout* _layout;
    private readonly Dictionary<(FontInstance Font, uint Glyph), SlugGlyphPlacement> _glyphs = [];
    private readonly List<Vector4> _curves = [];
    private readonly List<uint> _bands = [];
    private readonly Buffer* _params;
    private Buffer* _curveBuffer;
    private Buffer* _bandBuffer;
    private ulong _curveCapacity;
    private ulong _bandCapacity;
    private int _uploadedCurves;
    private int _uploadedBands;
    private BindGroup* _bindGroup;

    public SlugGlyphCache(WebGPU wgpu, Device* device, Queue* queue, BindGroupLayout* layout)
    {
        _wgpu = wgpu;
        _device = device;
        _queue = queue;
        _layout = layout;
        var descriptor = new BufferDescriptor { Size = ParamsSize, Usage = BufferUsage.Uniform | BufferUsage.CopyDst };
        _params = _wgpu.DeviceCreateBuffer(_device, in descriptor);
        // Buffers exist from the start, so the bind group is always valid, even before any text.
        _curveBuffer = CreateStorage(MinBufferBytes);
        _bandBuffer = CreateStorage(MinBufferBytes);
        _curveCapacity = _bandCapacity = MinBufferBytes;
        CreateBindGroup();
    }

    /// <summary>The number of glyphs prepared.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>The bytes of curve and band data held.</summary>
    public int ByteCount => (_curves.Count * sizeof(Vector4)) + (_bands.Count * sizeof(uint));

    /// <summary>The group-1 bind group: curves, bands and parameters. It changes when a buffer grows.</summary>
    public BindGroup* BindGroup => _bindGroup;

    /// <summary>A glyph's place in the buffers, preparing it the first time it's asked for.</summary>
    public SlugGlyphPlacement Get(FontInstance font, uint glyph)
    {
        if (_glyphs.TryGetValue((font, glyph), out var placed))
        {
            return placed;
        }
        var prepared = SlugGlyphBuilder.Build(font.GetOutline(glyph), font.Face.UnitsPerEm);
        if (prepared.IsEmpty)
        {
            placed = SlugGlyphPlacement.Empty;
        }
        else
        {
            placed = new SlugGlyphPlacement((uint)_bands.Count, (uint)_curves.Count, prepared.Min, prepared.Max);
            foreach (var texel in prepared.Curves)
            {
                _curves.Add(texel);
            }
            foreach (var word in prepared.Bands)
            {
                _bands.Add(word);
            }
        }
        _glyphs[(font, glyph)] = placed;
        return placed;
    }

    /// <summary>
    /// Empties the cache if it has outgrown <see cref="MaxBytes"/>. Call between frames: a frame's
    /// vertices point into the buffers.
    /// </summary>
    public void TrimIfFull()
    {
        if (ByteCount <= MaxBytes)
        {
            return;
        }
        _glyphs.Clear();
        _curves.Clear();
        _bands.Clear();
        _uploadedCurves = 0;
        _uploadedBands = 0;
    }

    /// <summary>Uploads what was prepared since the last upload, and the parameters.</summary>
    public void Upload(float gamma)
    {
        var parameters = stackalloc float[4] { gamma, 0f, 0f, 0f };
        _wgpu.QueueWriteBuffer(_queue, _params, 0, parameters, (nuint)ParamsSize);

        var grew = false;
        UploadTail(_curves, ref _curveBuffer, ref _curveCapacity, ref _uploadedCurves, ref grew);
        UploadTail(_bands, ref _bandBuffer, ref _bandCapacity, ref _uploadedBands, ref grew);
        if (grew)
        {
            _wgpu.BindGroupRelease(_bindGroup);
            CreateBindGroup();
        }
    }

    public void Dispose()
    {
        _glyphs.Clear();
        if (_bindGroup != null) _wgpu.BindGroupRelease(_bindGroup);
        if (_curveBuffer != null) _wgpu.BufferRelease(_curveBuffer);
        if (_bandBuffer != null) _wgpu.BufferRelease(_bandBuffer);
        if (_params != null) _wgpu.BufferRelease(_params);
        _bindGroup = null;
        _curveBuffer = null;
        _bandBuffer = null;
    }

    // Writes the part of a list not yet on the GPU; if the buffer is too small, replaces it with one
    // twice as big (or more) and writes the whole list.
    private void UploadTail<T>(List<T> data, ref Buffer* buffer, ref ulong capacity, ref int uploaded, ref bool grew)
        where T : unmanaged
    {
        if (uploaded == data.Count)
        {
            return;
        }
        var bytes = (ulong)(data.Count * sizeof(T));
        if (bytes > capacity)
        {
            _wgpu.BufferRelease(buffer);
            capacity = Math.Max(System.Numerics.BitOperations.RoundUpToPowerOf2(bytes), MinBufferBytes);
            buffer = CreateStorage(capacity);
            uploaded = 0;
            grew = true;
        }
        var span = CollectionsMarshal.AsSpan(data)[uploaded..];
        fixed (T* start = span)
        {
            _wgpu.QueueWriteBuffer(_queue, buffer, (ulong)(uploaded * sizeof(T)), start, (nuint)(span.Length * sizeof(T)));
        }
        uploaded = data.Count;
    }

    private Buffer* CreateStorage(ulong size)
    {
        var descriptor = new BufferDescriptor { Size = size, Usage = BufferUsage.Storage | BufferUsage.CopyDst };
        return _wgpu.DeviceCreateBuffer(_device, in descriptor);
    }

    private void CreateBindGroup()
    {
        var entries = stackalloc BindGroupEntry[3];
        entries[0] = new BindGroupEntry { Binding = 0, Buffer = _curveBuffer, Offset = 0, Size = _curveCapacity };
        entries[1] = new BindGroupEntry { Binding = 1, Buffer = _bandBuffer, Offset = 0, Size = _bandCapacity };
        entries[2] = new BindGroupEntry { Binding = 2, Buffer = _params, Offset = 0, Size = ParamsSize };
        var descriptor = new BindGroupDescriptor { Layout = _layout, EntryCount = 3, Entries = entries };
        _bindGroup = _wgpu.DeviceCreateBindGroup(_device, in descriptor);
    }
}
