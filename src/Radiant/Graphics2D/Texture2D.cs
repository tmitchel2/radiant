using System;
using Silk.NET.WebGPU;

namespace Radiant.Graphics2D
{
    /// <summary>
    /// A mutable GPU texture (sampler + view + bind group) that can be drawn with
    /// <see cref="Renderer2D.DrawImage(Texture2D, float, float, float, float)"/>. Pixels can be
    /// re-uploaded each frame via <see cref="Update"/>, and the texture can be re-sized via
    /// <see cref="Resize"/>. Created against a specific <see cref="Renderer2D"/> so its bind group
    /// matches that renderer's image pipeline layout.
    ///
    /// <para>Added for the multi-process tab host, which uploads a renderer's published frame
    /// (BGRA bytes from a shared-memory buffer) into this texture every frame and blits it.</para>
    /// </summary>
    public sealed unsafe class Texture2D : IDisposable
    {
        private readonly WebGPU _wgpu;
        private readonly Device* _device;
        private readonly Queue* _queue;
        private readonly BindGroupLayout* _layout;
        private readonly TextureFormat _format;

        private Texture* _texture;
        private TextureView* _view;
        private Sampler* _sampler;
        private BindGroup* _bindGroup;
        private bool _disposed;

        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>Group-1 bind group (sampler @0 + texture @1) for the image pipeline.</summary>
        internal BindGroup* BindGroup => _bindGroup;

        private Texture2D(Renderer2D renderer, int width, int height, TextureFormat format)
        {
            _wgpu = renderer.Wgpu;
            _device = renderer.Device;
            _queue = renderer.Queue;
            _layout = renderer.ImageBindGroupLayout;
            _format = format;
            CreateSampler();
            Allocate(width, height);
        }

        /// <summary>
        /// Create a drawable texture of the given size. Default format <c>Bgra8Unorm</c> matches the
        /// swapchain byte order used by the tab renderer's published frames (no swizzle needed).
        /// </summary>
        public static Texture2D Create(Renderer2D renderer, int width, int height, TextureFormat format = TextureFormat.Bgra8Unorm)
        {
            ArgumentNullException.ThrowIfNull(renderer);
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
            }
            return new Texture2D(renderer, width, height, format);
        }

        /// <summary>
        /// Upload <paramref name="pixels"/> (tightly packed, 4 bytes/pixel, matching this texture's
        /// format and current <see cref="Width"/>×<see cref="Height"/>) to the GPU.
        /// </summary>
        public void Update(ReadOnlySpan<byte> pixels)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var expected = Width * Height * 4;
            if (pixels.Length < expected)
            {
                throw new ArgumentException($"Pixel span ({pixels.Length} bytes) is smaller than {Width}x{Height} ({expected} bytes).", nameof(pixels));
            }

            var size = new Extent3D { Width = (uint)Width, Height = (uint)Height, DepthOrArrayLayers = 1 };
            var layout = new TextureDataLayout { Offset = 0, BytesPerRow = (uint)(Width * 4), RowsPerImage = (uint)Height };
            var copy = new ImageCopyTexture { Texture = _texture, MipLevel = 0, Origin = default, Aspect = TextureAspect.All };
            fixed (byte* ptr = pixels)
            {
                _wgpu.QueueWriteTexture(_queue, in copy, ptr, (nuint)expected, in layout, in size);
            }
        }

        /// <summary>Resize the texture (recreates the GPU texture, view, and bind group). No-op if unchanged.</summary>
        public void Resize(int width, int height)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
            }
            if (width == Width && height == Height)
            {
                return;
            }
            ReleaseTexture();
            Allocate(width, height);
        }

        private void Allocate(int width, int height)
        {
            Width = width;
            Height = height;

            var size = new Extent3D { Width = (uint)width, Height = (uint)height, DepthOrArrayLayers = 1 };
            var desc = new TextureDescriptor
            {
                Size = size,
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = TextureDimension.Dimension2D,
                Format = _format,
                Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst,
            };
            _texture = _wgpu.DeviceCreateTexture(_device, in desc);

            var viewDesc = new TextureViewDescriptor
            {
                Format = _format,
                Dimension = TextureViewDimension.Dimension2D,
                BaseMipLevel = 0,
                MipLevelCount = 1,
                BaseArrayLayer = 0,
                ArrayLayerCount = 1,
                Aspect = TextureAspect.All,
            };
            _view = _wgpu.TextureCreateView(_texture, in viewDesc);

            var entries = stackalloc BindGroupEntry[2];
            entries[0] = new BindGroupEntry { Binding = 0, Sampler = _sampler };
            entries[1] = new BindGroupEntry { Binding = 1, TextureView = _view };
            var bgDesc = new BindGroupDescriptor { Layout = _layout, EntryCount = 2, Entries = entries };
            _bindGroup = _wgpu.DeviceCreateBindGroup(_device, in bgDesc);
        }

        private void CreateSampler()
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
            _sampler = _wgpu.DeviceCreateSampler(_device, in desc);
        }

        private void ReleaseTexture()
        {
            if (_bindGroup != null) { _wgpu.BindGroupRelease(_bindGroup); _bindGroup = null; }
            if (_view != null) { _wgpu.TextureViewRelease(_view); _view = null; }
            if (_texture != null) { _wgpu.TextureRelease(_texture); _texture = null; }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            ReleaseTexture();
            if (_sampler != null) { _wgpu.SamplerRelease(_sampler); _sampler = null; }
            GC.SuppressFinalize(this);
        }
    }
}
