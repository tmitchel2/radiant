using System;
using Radiant.Graphics;
using Radiant.Graphics2D;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Tests.Graphics2D.Visual
{
    internal sealed class VisualTestHelper : IDisposable
    {
        public Renderer2D Renderer { get; }
        public Camera2D Camera { get; }
        public int Width { get; }
        public int Height { get; }

        private readonly Rgba32 _clearColor;

        public VisualTestHelper(int width = 128, int height = 128,
            Handedness handedness = Handedness.LeftHanded,
            Rgba32? clearColor = null)
        {
            Width = width;
            Height = height;
            _clearColor = clearColor ?? new Rgba32(0, 0, 0, 255);

            Camera = new Camera2D(width, height, handedness);
            Renderer = new Renderer2D();
        }

        public Image<Rgba32> Rasterize()
        {
            var projection = Camera.GetProjectionMatrix();
            return SoftwareRasterizer.Render(
                Renderer.FilledVertices,
                Renderer.LineVertices,
                projection,
                Width,
                Height,
                _clearColor);
        }

        public void AssertMatchesGolden(string testName, int tolerance = 2)
        {
            using var image = Rasterize();
            GoldenImageHelper.AssertMatchesGolden(image, testName, tolerance);
        }

        public void Dispose()
        {
            // Renderer2D.Dispose touches GPU resources we don't have, so we skip it.
            // The GC will clean up the managed lists.
        }
    }
}
