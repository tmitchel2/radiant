using System;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics;
using Radiant.Graphics2D;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Tests.Graphics2D.Visual
{
    [TestClass]
    public class VisualRenderingTests
    {
        private static readonly Vector4 White = new(1, 1, 1, 1);
        private static readonly Vector4 Red = new(1, 0, 0, 1);
        private static readonly Vector4 Green = new(0, 1, 0, 1);
        private static readonly Vector4 Blue = new(0, 0, 1, 1);
        private static readonly Vector4 Yellow = new(1, 1, 0, 1);
        private static readonly Vector4 SemiTransparentRed = new(1, 0, 0, 0.5f);
        private static readonly Vector4 SemiTransparentBlue = new(0, 0, 1, 0.5f);

        // ==================== Rectangle Tests ====================

        [TestMethod]
        public void RectangleFilled_LeftHanded_Origin()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(10, 10, 40, 30, White);
            helper.AssertMatchesGolden("RectFilled_LH_Origin");
        }

        [TestMethod]
        public void RectangleFilled_RightHanded_Origin()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawRectangleFilled(10, 10, 40, 30, White);
            helper.AssertMatchesGolden("RectFilled_RH_Origin");
        }

        [TestMethod]
        public void RectangleFilled_LeftHanded_Center()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(44, 49, 40, 30, Green);
            helper.AssertMatchesGolden("RectFilled_LH_Center");
        }

        [TestMethod]
        public void RectangleFilled_RightHanded_Center()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawRectangleFilled(44, 49, 40, 30, Green);
            helper.AssertMatchesGolden("RectFilled_RH_Center");
        }

        [TestMethod]
        public void RectangleOutline_LeftHanded_Origin()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleOutline(10, 10, 50, 40, White);
            helper.AssertMatchesGolden("RectOutline_LH_Origin");
        }

        [TestMethod]
        public void RectangleOutline_RightHanded_Origin()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawRectangleOutline(10, 10, 50, 40, White);
            helper.AssertMatchesGolden("RectOutline_RH_Origin");
        }

        [TestMethod]
        public void RectangleFilled_LeftHanded_Corners()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(0, 0, 20, 20, Red);
            helper.Renderer.DrawRectangleFilled(108, 0, 20, 20, Green);
            helper.Renderer.DrawRectangleFilled(0, 108, 20, 20, Blue);
            helper.Renderer.DrawRectangleFilled(108, 108, 20, 20, Yellow);
            helper.AssertMatchesGolden("RectFilled_LH_Corners");
        }

        [TestMethod]
        public void RectangleFilled_RightHanded_Corners()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawRectangleFilled(0, 0, 20, 20, Red);
            helper.Renderer.DrawRectangleFilled(108, 0, 20, 20, Green);
            helper.Renderer.DrawRectangleFilled(0, 108, 20, 20, Blue);
            helper.Renderer.DrawRectangleFilled(108, 108, 20, 20, Yellow);
            helper.AssertMatchesGolden("RectFilled_RH_Corners");
        }

        // ==================== Circle Tests ====================

        [TestMethod]
        public void CircleFilled_LeftHanded_Center()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawCircleFilled(64, 64, 30, Red);
            helper.AssertMatchesGolden("CircleFilled_LH_Center");
        }

        [TestMethod]
        public void CircleFilled_RightHanded_Center()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawCircleFilled(64, 64, 30, Red);
            helper.AssertMatchesGolden("CircleFilled_RH_Center");
        }

        [TestMethod]
        public void CircleOutline_LeftHanded_Center()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawCircleOutline(64, 64, 30, Green);
            helper.AssertMatchesGolden("CircleOutline_LH_Center");
        }

        [TestMethod]
        public void CircleOutline_RightHanded_Center()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawCircleOutline(64, 64, 30, Green);
            helper.AssertMatchesGolden("CircleOutline_RH_Center");
        }

        // ==================== Ellipse Tests ====================

        [TestMethod]
        public void EllipseFilled_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawEllipseFilled(64, 64, 40, 20, Blue);
            helper.AssertMatchesGolden("EllipseFilled_LH");
        }

        [TestMethod]
        public void EllipseFilled_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawEllipseFilled(64, 64, 40, 20, Blue);
            helper.AssertMatchesGolden("EllipseFilled_RH");
        }

        [TestMethod]
        public void EllipseOutline_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawEllipseOutline(64, 64, 40, 20, Yellow);
            helper.AssertMatchesGolden("EllipseOutline_LH");
        }

        [TestMethod]
        public void EllipseOutline_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawEllipseOutline(64, 64, 40, 20, Yellow);
            helper.AssertMatchesGolden("EllipseOutline_RH");
        }

        // ==================== Polygon Tests ====================

        [TestMethod]
        public void HexagonFilled_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawPolygonFilled(64, 64, 30, 6, Red);
            helper.AssertMatchesGolden("HexagonFilled_LH");
        }

        [TestMethod]
        public void HexagonFilled_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawPolygonFilled(64, 64, 30, 6, Red);
            helper.AssertMatchesGolden("HexagonFilled_RH");
        }

        [TestMethod]
        public void HexagonOutline_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawPolygonOutline(64, 64, 30, 6, White);
            helper.AssertMatchesGolden("HexagonOutline_LH");
        }

        [TestMethod]
        public void HexagonOutline_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawPolygonOutline(64, 64, 30, 6, White);
            helper.AssertMatchesGolden("HexagonOutline_RH");
        }

        // ==================== Line and Polyline Tests ====================

        [TestMethod]
        public void DiagonalLine_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawLine(new Vector2(10, 10), new Vector2(118, 118), White);
            helper.AssertMatchesGolden("DiagonalLine_LH");
        }

        [TestMethod]
        public void DiagonalLine_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawLine(new Vector2(10, 10), new Vector2(118, 118), White);
            helper.AssertMatchesGolden("DiagonalLine_RH");
        }

        [TestMethod]
        public void ZigzagPolyline_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawPolyline(new[]
            {
                new Vector2(10, 100), new Vector2(35, 20), new Vector2(60, 100),
                new Vector2(85, 20), new Vector2(118, 100)
            }, Green);
            helper.AssertMatchesGolden("ZigzagPolyline_LH");
        }

        [TestMethod]
        public void ZigzagPolyline_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawPolyline(new[]
            {
                new Vector2(10, 100), new Vector2(35, 20), new Vector2(60, 100),
                new Vector2(85, 20), new Vector2(118, 100)
            }, Green);
            helper.AssertMatchesGolden("ZigzagPolyline_RH");
        }

        // ==================== Text Tests ====================

        [TestMethod]
        public void Text_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawText("HELLO", 10, 10, 2, White);
            helper.AssertMatchesGolden("Text_LH");
        }

        [TestMethod]
        public void Text_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            helper.Renderer.DrawText("HELLO", 10, 10, 2, White);
            helper.AssertMatchesGolden("Text_RH");
        }

        // ==================== Handedness Parity Tests ====================
        // At Z=0, both handedness modes should produce identical 2D output

        [TestMethod]
        public void Parity_Rectangle()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            lh.Renderer.DrawRectangleFilled(20, 20, 60, 40, White);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            rh.Renderer.DrawRectangleFilled(20, 20, 60, 40, White);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Circle()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            lh.Renderer.DrawCircleFilled(64, 64, 25, Red);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            rh.Renderer.DrawCircleFilled(64, 64, 25, Red);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Ellipse()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            lh.Renderer.DrawEllipseFilled(64, 64, 40, 20, Blue);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            rh.Renderer.DrawEllipseFilled(64, 64, 40, 20, Blue);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Polygon()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            lh.Renderer.DrawPolygonFilled(64, 64, 30, 6, Green);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            rh.Renderer.DrawPolygonFilled(64, 64, 30, 6, Green);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Line()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            lh.Renderer.DrawLine(new Vector2(10, 10), new Vector2(118, 118), White);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            rh.Renderer.DrawLine(new Vector2(10, 10), new Vector2(118, 118), White);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Text()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            lh.Renderer.DrawText("HI", 20, 20, 2, White);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            rh.Renderer.DrawText("HI", 20, 20, 2, White);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_CompositeScene()
        {
            using var lh = new VisualTestHelper(handedness: Handedness.LeftHanded);
            DrawCompositeScene(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(handedness: Handedness.RightHanded);
            DrawCompositeScene(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        // ==================== Coordinate System Tests ====================

        [TestMethod]
        public void Coordinate_OriginIsTopLeft()
        {
            // A small rect at (0,0) should appear in the top-left corner
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(0, 0, 10, 10, White);
            using var image = helper.Rasterize();

            // Top-left should be white
            AssertPixelIs(image, 1, 1, White);
            // Center should be black (clear color)
            AssertPixelIs(image, 64, 64, new Vector4(0, 0, 0, 1));
        }

        [TestMethod]
        public void Coordinate_YIncreasesDownward()
        {
            // Red rect at top, blue rect further down
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(50, 10, 20, 10, Red);
            helper.Renderer.DrawRectangleFilled(50, 100, 20, 10, Blue);
            using var image = helper.Rasterize();

            // Red should be near top (low Y pixel)
            AssertPixelIs(image, 60, 15, Red);
            // Blue should be near bottom (high Y pixel)
            AssertPixelIs(image, 60, 105, Blue);
        }

        [TestMethod]
        public void Coordinate_XIncreasesRightward()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(10, 50, 10, 20, Red);
            helper.Renderer.DrawRectangleFilled(100, 50, 10, 20, Green);
            using var image = helper.Rasterize();

            // Red should be on the left
            AssertPixelIs(image, 15, 60, Red);
            // Green should be on the right
            AssertPixelIs(image, 105, 60, Green);
        }

        [TestMethod]
        public void Coordinate_ViewportTopEdge()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(0, 0, 128, 5, White);
            using var image = helper.Rasterize();

            AssertPixelIs(image, 64, 2, White);
            AssertPixelIs(image, 64, 10, new Vector4(0, 0, 0, 1));
        }

        [TestMethod]
        public void Coordinate_ViewportBottomEdge()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(0, 123, 128, 5, White);
            using var image = helper.Rasterize();

            AssertPixelIs(image, 64, 125, White);
            AssertPixelIs(image, 64, 118, new Vector4(0, 0, 0, 1));
        }

        [TestMethod]
        public void Coordinate_ViewportRightEdge()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(123, 0, 5, 128, White);
            using var image = helper.Rasterize();

            AssertPixelIs(image, 125, 64, White);
            AssertPixelIs(image, 118, 64, new Vector4(0, 0, 0, 1));
        }

        // ==================== Draw Order / Overlap Tests ====================

        [TestMethod]
        public void DrawOrder_LaterDrawOverlapsEarlier()
        {
            // Painter's algorithm: last drawn is on top
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(30, 30, 40, 40, Red);
            helper.Renderer.DrawRectangleFilled(50, 50, 40, 40, Blue);
            helper.AssertMatchesGolden("DrawOrder_Overlap");
        }

        [TestMethod]
        public void DrawOrder_SemiTransparentOverlap()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(20, 30, 50, 50, SemiTransparentRed);
            helper.Renderer.DrawRectangleFilled(50, 30, 50, 50, SemiTransparentBlue);
            helper.AssertMatchesGolden("DrawOrder_SemiTransparent");
        }

        [TestMethod]
        public void DrawOrder_FilledThenOutline()
        {
            // Filled shapes are drawn before lines (matching GPU pipeline order)
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(30, 30, 60, 60, Red);
            helper.Renderer.DrawRectangleOutline(30, 30, 60, 60, White);
            helper.AssertMatchesGolden("DrawOrder_FilledThenOutline");
        }

        [TestMethod]
        public void DrawOrder_MultipleLayered()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawCircleFilled(50, 64, 30, Red);
            helper.Renderer.DrawCircleFilled(78, 64, 30, Blue);
            helper.Renderer.DrawRectangleFilled(45, 50, 38, 28, new Vector4(0, 1, 0, 0.7f));
            helper.AssertMatchesGolden("DrawOrder_MultipleLayered");
        }

        // ==================== Composite Scene Tests ====================

        [TestMethod]
        public void CompositeScene_LeftHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            DrawCompositeScene(helper.Renderer);
            helper.AssertMatchesGolden("Composite_LH");
        }

        [TestMethod]
        public void CompositeScene_RightHanded()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.RightHanded);
            DrawCompositeScene(helper.Renderer);
            helper.AssertMatchesGolden("Composite_RH");
        }

        // ==================== Sanity Check Tests ====================

        [TestMethod]
        public void Sanity_FullViewportWhiteRect()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(0, 0, 128, 128, White);
            using var image = helper.Rasterize();

            // Every pixel should be white
            for (var y = 0; y < 128; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    var pixel = image[x, y];
                    Assert.AreEqual(255, pixel.R, $"Pixel ({x},{y}) R should be 255, got {pixel.R}");
                    Assert.AreEqual(255, pixel.G, $"Pixel ({x},{y}) G should be 255, got {pixel.G}");
                    Assert.AreEqual(255, pixel.B, $"Pixel ({x},{y}) B should be 255, got {pixel.B}");
                }
            }
        }

        [TestMethod]
        public void Sanity_HalfViewport()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);
            helper.Renderer.DrawRectangleFilled(0, 0, 64, 128, White);
            using var image = helper.Rasterize();

            // Left half should be white (approximately, accounting for edge pixels)
            AssertPixelIs(image, 10, 64, White);
            // Right half should be black
            AssertPixelIs(image, 100, 64, new Vector4(0, 0, 0, 1));
        }

        [TestMethod]
        public void Sanity_VertexCounts()
        {
            using var helper = new VisualTestHelper(handedness: Handedness.LeftHanded);

            // Rectangle = 6 filled vertices (2 triangles)
            helper.Renderer.DrawRectangleFilled(0, 0, 10, 10, White);
            Assert.AreEqual(6, helper.Renderer.FilledVertices.Count);
            Assert.AreEqual(0, helper.Renderer.LineVertices.Count);

            // Outline rectangle = 8 line vertices (4 lines)
            helper.Renderer.DrawRectangleOutline(0, 0, 10, 10, White);
            Assert.AreEqual(6, helper.Renderer.FilledVertices.Count);
            Assert.AreEqual(8, helper.Renderer.LineVertices.Count);

            // Circle with 32 segments = 96 filled vertices (32 triangles * 3)
            helper.Renderer.DrawCircleFilled(50, 50, 10, Red);
            Assert.AreEqual(6 + 96, helper.Renderer.FilledVertices.Count);
        }

        // ==================== Helper Methods ====================

        private static void DrawCompositeScene(Renderer2D renderer)
        {
            // Background rect
            renderer.DrawRectangleFilled(5, 5, 118, 118, new Vector4(0.2f, 0.2f, 0.2f, 1));

            // Shapes
            renderer.DrawCircleFilled(32, 40, 15, Red);
            renderer.DrawRectangleFilled(60, 25, 30, 30, Green);
            renderer.DrawEllipseFilled(96, 40, 18, 12, Blue);
            renderer.DrawPolygonFilled(64, 90, 20, 5, Yellow);

            // Outlines
            renderer.DrawCircleOutline(32, 40, 15, White);
            renderer.DrawRectangleOutline(60, 25, 30, 30, White);

            // Lines
            renderer.DrawLine(new Vector2(5, 70), new Vector2(123, 70), new Vector4(0.5f, 0.5f, 0.5f, 1));

            // Text
            renderer.DrawText("TEST", 40, 110, 1, White);
        }

        private static void AssertPixelIs(Image<Rgba32> image, int x, int y, Vector4 expectedColor, int tolerance = 5)
        {
            var pixel = image[x, y];
            var er = (byte)Math.Clamp(expectedColor.X * 255f + 0.5f, 0, 255);
            var eg = (byte)Math.Clamp(expectedColor.Y * 255f + 0.5f, 0, 255);
            var eb = (byte)Math.Clamp(expectedColor.Z * 255f + 0.5f, 0, 255);

            Assert.IsTrue(Math.Abs(pixel.R - er) <= tolerance &&
                          Math.Abs(pixel.G - eg) <= tolerance &&
                          Math.Abs(pixel.B - eb) <= tolerance,
                $"Pixel ({x},{y}): expected ~({er},{eg},{eb}), got ({pixel.R},{pixel.G},{pixel.B})");
        }
    }
}
