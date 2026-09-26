using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// Runtime MSDF font: holds a baked atlas (texture + glyph metric table)
    /// and the GPU resources (texture + sampler + bind group) used to sample
    /// it. Created once per font, kept for the lifetime of the renderer.
    /// </summary>
    [JsonSerializable(typeof(MsdfAtlasManifest))]
    internal sealed partial class MsdfAtlasJsonContext : JsonSerializerContext
    {
    }

    public sealed unsafe class MsdfFont : IDisposable
    {

        private readonly Dictionary<int, MsdfAtlasGlyph> _glyphs;
        private readonly Dictionary<long, float> _kerning;

        public string FamilyName { get; }
        public int AtlasWidth { get; }
        public int AtlasHeight { get; }
        public int GlyphPixelSize { get; }
        public float DistanceRangePx { get; }
        public float LineHeightEm { get; }
        public float AscenderEm { get; }
        public float DescenderEm { get; }

        /// <summary>Raw atlas texture — exposed for renderers that want to build their own bind group.</summary>
        public Texture* Texture { get; private set; }
        /// <summary>2D view of <see cref="Texture"/>.</summary>
        public TextureView* TextureView { get; private set; }
        /// <summary>Linear-filtering sampler used for atlas reads.</summary>
        public Sampler* Sampler { get; private set; }
        /// <summary>Default bind group built by <see cref="Renderer2D.RegisterMsdfFont"/>.</summary>
        public BindGroup* BindGroup { get; private set; }

        /// <summary>
        /// Size in bytes of the per-font uniform the MSDF shader reads (binding 2): one
        /// <c>vec4&lt;f32&gt;</c> whose x is <see cref="DistanceRangePx"/>.
        /// </summary>
        internal const ulong AtlasParamsSize = 16;

        // Carries DistanceRangePx to the shader. The edge sharpness is computed from it, so it has
        // to be this atlas's own range: a shader constant only matches atlases baked with that value.
        private Buffer* _atlasParams;

        private WebGPU _wgpu = null!;

        private MsdfFont(MsdfAtlasManifest manifest)
        {
            FamilyName = manifest.FontFamily;
            AtlasWidth = manifest.AtlasWidth;
            AtlasHeight = manifest.AtlasHeight;
            GlyphPixelSize = manifest.GlyphPixelSize;
            DistanceRangePx = manifest.DistanceRange;
            LineHeightEm = manifest.LineHeight;
            AscenderEm = manifest.Ascender;
            DescenderEm = manifest.Descender;

            _glyphs = new Dictionary<int, MsdfAtlasGlyph>(manifest.Glyphs.Count);
            foreach (var g in manifest.Glyphs) _glyphs[g.Codepoint] = g;

            _kerning = new Dictionary<long, float>(manifest.Kerning.Count);
            foreach (var k in manifest.Kerning)
            {
                _kerning[((long)k.Left << 32) | (uint)k.Right] = k.Offset;
            }
        }

        public static MsdfFont LoadFromFiles(string pngPath, string jsonPath)
        {
            var manifest = JsonSerializer.Deserialize(
                File.ReadAllText(jsonPath), MsdfAtlasJsonContext.Default.MsdfAtlasManifest)
                ?? throw new InvalidDataException($"Failed to read atlas manifest: {jsonPath}");
            var pngBytes = File.ReadAllBytes(pngPath);
            var font = new MsdfFont(manifest);
            font.AtlasPngBytes = pngBytes;
            return font;
        }

        /// <summary>
        /// Load a font that's been shipped as embedded resources in the
        /// Radiant assembly (Assets/Fonts/&lt;name&gt;.png + .json). This is the
        /// default path for bundled fonts — no file paths leak into the
        /// consumer. Names are in <see cref="EmbeddedFonts"/>; "default" and
        /// "monospace" resolve to <see cref="EmbeddedFonts.Default"/> and
        /// <see cref="EmbeddedFonts.Monospace"/>.
        /// </summary>
        public static MsdfFont LoadEmbedded(string name)
        {
            name = EmbeddedFonts.Resolve(name);
            var asm = typeof(MsdfFont).Assembly;
            var prefix = "Radiant.Assets.Fonts.";
            using var pngStream = asm.GetManifestResourceStream(prefix + name + ".png")
                ?? throw new FileNotFoundException($"Embedded font PNG not found: {prefix}{name}.png");
            using var jsonStream = asm.GetManifestResourceStream(prefix + name + ".json")
                ?? throw new FileNotFoundException($"Embedded font JSON not found: {prefix}{name}.json");
            return LoadFromStreams(pngStream, jsonStream);
        }

        public static MsdfFont LoadFromStreams(Stream pngStream, Stream jsonStream)
        {
            var manifest = JsonSerializer.Deserialize(jsonStream, MsdfAtlasJsonContext.Default.MsdfAtlasManifest)
                ?? throw new InvalidDataException("Failed to deserialize atlas manifest.");
            using var ms = new MemoryStream();
            pngStream.CopyTo(ms);
            var font = new MsdfFont(manifest);
            font.AtlasPngBytes = ms.ToArray();
            return font;
        }

        /// <summary>Raw PNG bytes — decoded once by the renderer when GPU resources are created.</summary>
        internal byte[] AtlasPngBytes { get; private set; } = [];

        public bool TryGetGlyph(int codepoint, out MsdfAtlasGlyph glyph)
            => _glyphs.TryGetValue(codepoint, out glyph!);

        public float Kerning(int left, int right)
        {
            var key = ((long)left << 32) | (uint)right;
            return _kerning.TryGetValue(key, out var offset) ? offset : 0f;
        }

        /// <summary>
        /// Measure the un-kerned advance of a text run in pixels at the given
        /// pixel height. Used by callers that need to position the run before
        /// drawing (e.g. right-align, centre-align).
        /// </summary>
        public float MeasureTextWidth(string text, float pixelHeight)
        {
            var sum = 0f;
            int prev = -1;
            foreach (var rune in text.EnumerateRunes())
            {
                if (!_glyphs.TryGetValue(rune.Value, out var g)) continue;
                if (prev >= 0) sum += Kerning(prev, rune.Value) * pixelHeight;
                sum += g.Advance * pixelHeight;
                prev = rune.Value;
            }
            return sum;
        }

        internal void InitializeGpuResources(
            WebGPU wgpu,
            Device* device,
            Queue* queue,
            BindGroupLayout* layout,
            byte[] pixelBytes,
            int width,
            int height)
        {
            _wgpu = wgpu;
            CreateTexture(device, queue, pixelBytes, width, height);
            CreateSampler(device);
            CreateAtlasParams(device, queue);
            CreateBindGroup(device, layout);
        }

        private void CreateTexture(Device* device, Queue* queue, byte[] pixels, int width, int height)
        {
            var size = new Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
            var desc = new TextureDescriptor
            {
                Size = size,
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = TextureFormat.Rgba8Unorm,
                Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            };
            Texture = _wgpu.DeviceCreateTexture(device, in desc);

            var dataLayout = new TextureDataLayout
            {
                Offset = 0,
                BytesPerRow = (uint)(width * 4),
                RowsPerImage = (uint)height,
            };
            var imageCopy = new ImageCopyTexture
            {
                Texture = Texture,
                MipLevel = 0,
                Origin = default,
                Aspect = TextureAspect.All,
            };
            fixed (byte* ptr = pixels)
            {
                _wgpu.QueueWriteTexture(queue, in imageCopy, ptr, (nuint)pixels.Length, in dataLayout, in size);
            }

            var viewDesc = new TextureViewDescriptor
            {
                Format = TextureFormat.Rgba8Unorm,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All,
            };
            TextureView = _wgpu.TextureCreateView(Texture, in viewDesc);
        }

        private void CreateSampler(Device* device)
        {
            var desc = new SamplerDescriptor
            {
                AddressModeU = AddressMode.ClampToEdge,
                AddressModeV = AddressMode.ClampToEdge,
                AddressModeW = AddressMode.ClampToEdge,
                MagFilter = FilterMode.Linear,
                MinFilter = FilterMode.Linear,
                MipmapFilter = MipmapFilterMode.Linear,
                LodMinClamp = 0,
                LodMaxClamp = 0,
                MaxAnisotropy = 1,
            };
            Sampler = _wgpu.DeviceCreateSampler(device, in desc);
        }

        private void CreateAtlasParams(Device* device, Queue* queue)
        {
            var desc = new BufferDescriptor
            {
                Size = AtlasParamsSize,
                Usage = BufferUsage.Uniform | BufferUsage.CopyDst,
                MappedAtCreation = false,
            };
            _atlasParams = _wgpu.DeviceCreateBuffer(device, in desc);
            var values = stackalloc float[4] { DistanceRangePx, 0f, 0f, 0f };
            _wgpu.QueueWriteBuffer(queue, _atlasParams, 0, values, (nuint)AtlasParamsSize);
        }

        private void CreateBindGroup(Device* device, BindGroupLayout* layout)
        {
            var entries = stackalloc BindGroupEntry[3];
            entries[0] = new BindGroupEntry
            {
                Binding = 0,
                Sampler = Sampler,
            };
            entries[1] = new BindGroupEntry
            {
                Binding = 1,
                TextureView = TextureView,
            };
            entries[2] = new BindGroupEntry
            {
                Binding = 2,
                Buffer = _atlasParams,
                Offset = 0,
                Size = AtlasParamsSize,
            };
            var desc = new BindGroupDescriptor
            {
                Layout = layout,
                EntryCount = 3,
                Entries = entries,
            };
            BindGroup = _wgpu.DeviceCreateBindGroup(device, in desc);
        }

        public void Dispose()
        {
            if (_wgpu is null) return;
            if (BindGroup != null) _wgpu.BindGroupRelease(BindGroup);
            if (TextureView != null) _wgpu.TextureViewRelease(TextureView);
            if (Texture != null) _wgpu.TextureRelease(Texture);
            if (Sampler != null) _wgpu.SamplerRelease(Sampler);
            if (_atlasParams != null) _wgpu.BufferRelease(_atlasParams);
            _atlasParams = null;
            BindGroup = null;
            TextureView = null;
            Texture = null;
            Sampler = null;
            GC.SuppressFinalize(this);
        }
    }
}
