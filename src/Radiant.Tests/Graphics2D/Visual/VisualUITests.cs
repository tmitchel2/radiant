using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics;
using Radiant.Graphics2D;
using Radiant.UI;
using SixLabors.ImageSharp.PixelFormats;

namespace Radiant.Tests.Graphics2D.Visual
{
    [TestClass]
    public class VisualUITests
    {
        // ==================== Label Tests ====================

        [TestMethod]
        public void Label_LeftHanded()
        {
            using var helper = new VisualTestHelper(192, 64, Handedness.LeftHanded);
            var label = new Label("HELLO", new Vector2(10, 10));
            label.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Label_LH");
        }

        [TestMethod]
        public void Label_RightHanded()
        {
            using var helper = new VisualTestHelper(192, 64, Handedness.RightHanded);
            var label = new Label("HELLO", new Vector2(10, 10));
            label.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Label_RH");
        }

        [TestMethod]
        public void Label_Scaled()
        {
            using var helper = new VisualTestHelper(192, 64, Handedness.LeftHanded);
            var label = new Label("AB", new Vector2(10, 10)) { TextScale = 2f };
            label.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Label_Scaled");
        }

        [TestMethod]
        public void Label_Invisible_RendersNothing()
        {
            using var lh = new VisualTestHelper(128, 64, Handedness.LeftHanded);
            var label = new Label("HIDDEN", new Vector2(10, 10)) { Visible = false };
            label.Draw(lh.Renderer);
            using var image = lh.Rasterize();

            // Should be entirely black (clear color)
            for (var y = 0; y < 64; y++)
                for (var x = 0; x < 128; x++)
                    Assert.AreEqual(new Rgba32(0, 0, 0, 255), image[x, y],
                        $"Pixel ({x},{y}) should be black when label is invisible");
        }

        // ==================== Button Tests ====================

        [TestMethod]
        public void Button_Idle_LeftHanded()
        {
            using var helper = new VisualTestHelper(192, 64, Handedness.LeftHanded);
            var button = new Button("OK", new Vector2(10, 10), new Vector2(80, 30));
            button.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Button_Idle_LH");
        }

        [TestMethod]
        public void Button_Idle_RightHanded()
        {
            using var helper = new VisualTestHelper(192, 64, Handedness.RightHanded);
            var button = new Button("OK", new Vector2(10, 10), new Vector2(80, 30));
            button.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Button_Idle_RH");
        }

        [TestMethod]
        public void Button_Disabled()
        {
            using var helper = new VisualTestHelper(192, 64, Handedness.LeftHanded);
            var button = new Button("SAVE", new Vector2(10, 10), new Vector2(80, 30)) { Enabled = false };
            button.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Button_Disabled");
        }

        // ==================== Slider Tests ====================

        [TestMethod]
        public void Slider_ZeroValue_LeftHanded()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.LeftHanded);
            var slider = new Slider("X", 0, 100, 0, new Vector2(10, 20), 230);
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_Zero_LH");
        }

        [TestMethod]
        public void Slider_ZeroValue_RightHanded()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.RightHanded);
            var slider = new Slider("X", 0, 100, 0, new Vector2(10, 20), 230);
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_Zero_RH");
        }

        [TestMethod]
        public void Slider_MidValue_LeftHanded()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.LeftHanded);
            var slider = new Slider("X", 0, 100, 50, new Vector2(10, 20), 230);
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_Mid_LH");
        }

        [TestMethod]
        public void Slider_MidValue_RightHanded()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.RightHanded);
            var slider = new Slider("X", 0, 100, 50, new Vector2(10, 20), 230);
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_Mid_RH");
        }

        [TestMethod]
        public void Slider_MaxValue()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.LeftHanded);
            var slider = new Slider("X", 0, 100, 100, new Vector2(10, 20), 230);
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_Max");
        }

        [TestMethod]
        public void Slider_NoLabel()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.LeftHanded);
            var slider = new Slider("", 0, 1, 0.5f, new Vector2(10, 20), 230);
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_NoLabel");
        }

        [TestMethod]
        public void Slider_Focused()
        {
            using var helper = new VisualTestHelper(256, 64, Handedness.LeftHanded);
            var slider = new Slider("V", 0, 10, 5, new Vector2(10, 20), 230) { IsFocused = true };
            slider.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Slider_Focused");
        }

        // ==================== Stacked Sliders A/B Tests ====================
        // The key test: two sliders stacked vertically to ensure correct Y handling

        [TestMethod]
        public void StackedSliders_AB_LeftHanded()
        {
            using var helper = new VisualTestHelper(256, 128, Handedness.LeftHanded);
            DrawStackedSlidersAB(helper.Renderer);
            helper.AssertMatchesGolden("UI_StackedSliders_AB_LH");
        }

        [TestMethod]
        public void StackedSliders_AB_RightHanded()
        {
            using var helper = new VisualTestHelper(256, 128, Handedness.RightHanded);
            DrawStackedSlidersAB(helper.Renderer);
            helper.AssertMatchesGolden("UI_StackedSliders_AB_RH");
        }

        [TestMethod]
        public void StackedSliders_AB_Parity()
        {
            using var lh = new VisualTestHelper(256, 128, Handedness.LeftHanded);
            DrawStackedSlidersAB(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(256, 128, Handedness.RightHanded);
            DrawStackedSlidersAB(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void StackedSliders_AB_YOrdering()
        {
            // Verify slider A is above slider B in pixel space
            // Slider A is at Y=20, Slider B at Y=60
            // Each slider has centerY = Position.Y + 12 (Size.Y/2 = 24/2)
            // So A's handle center is at pixel Y=32, B's at Y=72

            using var helper = new VisualTestHelper(256, 128, Handedness.LeftHanded);
            DrawStackedSlidersAB(helper.Renderer);
            using var image = helper.Rasterize();

            // Slider A handle is at about NormalizedValue=0.25 along the track
            // Slider B handle is at about NormalizedValue=0.75 along the track
            // Both have handle radius 8, so they appear as light circles

            // Check that there are non-black pixels at slider A's Y range (Y ~ 24-40)
            var hasPixelsAtSliderA = false;
            var hasPixelsAtSliderB = false;

            for (var x = 0; x < 256; x++)
            {
                // Slider A center Y ~ 32
                if (image[x, 32].R > 30 || image[x, 32].G > 30 || image[x, 32].B > 30)
                    hasPixelsAtSliderA = true;

                // Slider B center Y ~ 72
                if (image[x, 72].R > 30 || image[x, 72].G > 30 || image[x, 72].B > 30)
                    hasPixelsAtSliderB = true;
            }

            Assert.IsTrue(hasPixelsAtSliderA, "Slider A should have visible content at Y~32");
            Assert.IsTrue(hasPixelsAtSliderB, "Slider B should have visible content at Y~72");

            // Verify there's a gap between them (Y~50 should be mostly empty/black)
            var gapPixelCount = 0;
            for (var x = 0; x < 256; x++)
            {
                var pixel = image[x, 52];
                if (pixel.R < 10 && pixel.G < 10 && pixel.B < 10)
                    gapPixelCount++;
            }

            Assert.IsTrue(gapPixelCount > 200,
                $"Expected mostly black gap between sliders at Y=52, but found only {gapPixelCount}/256 black pixels");
        }

        [TestMethod]
        public void StackedSliders_AB_DifferentValues()
        {
            // Slider A at 25% should have its handle/fill to the left
            // Slider B at 75% should have its handle/fill further right
            using var helper = new VisualTestHelper(256, 128, Handedness.LeftHanded);
            DrawStackedSlidersAB(helper.Renderer);
            using var image = helper.Rasterize();

            // The handle is a filled circle (HandleRadius=8) at the normalized position
            // For the fill color (Primary blue: 0.26, 0.59, 0.98), look for blue-ish pixels
            // Slider A (25%): fill extends ~25% of track width
            // Slider B (75%): fill extends ~75% of track width

            // Label "A" takes labelWidth = 1*8+8 = 16 pixels from Position.X=10
            // So track starts at X = 10 + 16 = 26
            // valueWidth = 50, HandleRadius = 8
            // trackWidth = 230 - 16 - 50 - 8 = 156
            // Slider A handle at X = 26 + 0.25*156 = 65
            // Slider B handle at X = 26 + 0.75*156 = 143

            // Sample at slider A's Y (centerY=32): far right of track should not have fill
            var sliderAFarRight = image[150, 32]; // Should not have blue fill
            // Sample at slider B's Y (centerY=72): far right should have fill
            var sliderBFarRight = image[140, 72]; // Should have blue fill

            // Slider A at X=150 is past the 25% fill (fill only reaches ~X=65)
            // The track (gray 0.3) should be there but not the blue fill
            // Slider B at X=140 is within the 75% fill (fill reaches ~X=143)

            // Blue fill color is (0.26, 0.59, 0.98) => R~66, G~150, B~250
            Assert.IsTrue(sliderBFarRight.B > sliderAFarRight.B + 30,
                $"Slider B (75%) should have more blue at X=140 than Slider A at X=150. " +
                $"A=({sliderAFarRight.R},{sliderAFarRight.G},{sliderAFarRight.B}), " +
                $"B=({sliderBFarRight.R},{sliderBFarRight.G},{sliderBFarRight.B})");
        }

        // ==================== Panel Tests ====================

        [TestMethod]
        public void Panel_Empty_LeftHanded()
        {
            using var helper = new VisualTestHelper(192, 128, Handedness.LeftHanded);
            var panel = new Panel(new Vector2(10, 10), new Vector2(160, 100))
            {
                Title = "TEST"
            };
            panel.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Panel_Empty_LH");
        }

        [TestMethod]
        public void Panel_Empty_RightHanded()
        {
            using var helper = new VisualTestHelper(192, 128, Handedness.RightHanded);
            var panel = new Panel(new Vector2(10, 10), new Vector2(160, 100))
            {
                Title = "TEST"
            };
            panel.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Panel_Empty_RH");
        }

        [TestMethod]
        public void Panel_NoBorder()
        {
            using var helper = new VisualTestHelper(192, 128, Handedness.LeftHanded);
            var panel = new Panel(new Vector2(10, 10), new Vector2(160, 100))
            {
                DrawBorder = false,
                Title = "NO BORDER"
            };
            panel.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Panel_NoBorder");
        }

        [TestMethod]
        public void Panel_NoBackground()
        {
            using var helper = new VisualTestHelper(192, 128, Handedness.LeftHanded);
            var panel = new Panel(new Vector2(10, 10), new Vector2(160, 100))
            {
                DrawBackground = false,
                Title = "NO BG"
            };
            panel.Draw(helper.Renderer);
            helper.AssertMatchesGolden("UI_Panel_NoBg");
        }

        // ==================== Panel with Sliders A/B Tests ====================

        [TestMethod]
        public void PanelWithSlidersAB_LeftHanded()
        {
            using var helper = new VisualTestHelper(280, 160, Handedness.LeftHanded);
            DrawPanelWithSlidersAB(helper.Renderer);
            helper.AssertMatchesGolden("UI_PanelSliders_AB_LH");
        }

        [TestMethod]
        public void PanelWithSlidersAB_RightHanded()
        {
            using var helper = new VisualTestHelper(280, 160, Handedness.RightHanded);
            DrawPanelWithSlidersAB(helper.Renderer);
            helper.AssertMatchesGolden("UI_PanelSliders_AB_RH");
        }

        [TestMethod]
        public void PanelWithSlidersAB_Parity()
        {
            using var lh = new VisualTestHelper(280, 160, Handedness.LeftHanded);
            DrawPanelWithSlidersAB(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(280, 160, Handedness.RightHanded);
            DrawPanelWithSlidersAB(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void PanelWithSlidersAB_YOrdering()
        {
            // Panel at (10,10), padding=8, title "SLIDERS" with scale 1.2
            // AddSliderGroup starts at startY=40
            // groupLabel "VALUES" at Y=40, then currentY += 20 => 60
            // Slider A at Y=60, center at Y=72
            // Slider B at Y=90 (60+30), center at Y=102

            using var helper = new VisualTestHelper(280, 160, Handedness.LeftHanded);
            DrawPanelWithSlidersAB(helper.Renderer);
            using var image = helper.Rasterize();

            var hasContentAtA = false;
            var hasContentAtB = false;

            for (var x = 0; x < 280; x++)
            {
                if (image[x, 72].R > 50 || image[x, 72].G > 50 || image[x, 72].B > 50)
                    hasContentAtA = true;
                if (image[x, 102].R > 50 || image[x, 102].G > 50 || image[x, 102].B > 50)
                    hasContentAtB = true;
            }

            Assert.IsTrue(hasContentAtA, "Slider A should have visible content at Y~72");
            Assert.IsTrue(hasContentAtB, "Slider B should have visible content at Y~102");
        }

        // ==================== UIManager Tests ====================

        [TestMethod]
        public void UIManager_MultipleElements_LeftHanded()
        {
            using var helper = new VisualTestHelper(256, 128, Handedness.LeftHanded);
            DrawUIManagerScene(helper.Renderer);
            helper.AssertMatchesGolden("UI_Manager_LH");
        }

        [TestMethod]
        public void UIManager_MultipleElements_RightHanded()
        {
            using var helper = new VisualTestHelper(256, 128, Handedness.RightHanded);
            DrawUIManagerScene(helper.Renderer);
            helper.AssertMatchesGolden("UI_Manager_RH");
        }

        [TestMethod]
        public void UIManager_Parity()
        {
            using var lh = new VisualTestHelper(256, 128, Handedness.LeftHanded);
            DrawUIManagerScene(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(256, 128, Handedness.RightHanded);
            DrawUIManagerScene(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        // ==================== Composite UI Scene Tests ====================

        [TestMethod]
        public void CompositeUI_LeftHanded()
        {
            using var helper = new VisualTestHelper(300, 200, Handedness.LeftHanded);
            DrawCompositeUIScene(helper.Renderer);
            helper.AssertMatchesGolden("UI_Composite_LH");
        }

        [TestMethod]
        public void CompositeUI_RightHanded()
        {
            using var helper = new VisualTestHelper(300, 200, Handedness.RightHanded);
            DrawCompositeUIScene(helper.Renderer);
            helper.AssertMatchesGolden("UI_Composite_RH");
        }

        [TestMethod]
        public void CompositeUI_Parity()
        {
            using var lh = new VisualTestHelper(300, 200, Handedness.LeftHanded);
            DrawCompositeUIScene(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(300, 200, Handedness.RightHanded);
            DrawCompositeUIScene(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        // ==================== Handedness Parity Tests ====================

        [TestMethod]
        public void Parity_Label()
        {
            using var lh = new VisualTestHelper(192, 64, Handedness.LeftHanded);
            new Label("TEST", new Vector2(10, 10)).Draw(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(192, 64, Handedness.RightHanded);
            new Label("TEST", new Vector2(10, 10)).Draw(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Button()
        {
            using var lh = new VisualTestHelper(192, 64, Handedness.LeftHanded);
            new Button("GO", new Vector2(10, 10), new Vector2(80, 30)).Draw(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(192, 64, Handedness.RightHanded);
            new Button("GO", new Vector2(10, 10), new Vector2(80, 30)).Draw(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Slider()
        {
            using var lh = new VisualTestHelper(256, 64, Handedness.LeftHanded);
            new Slider("V", 0, 100, 50, new Vector2(10, 20), 230).Draw(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(256, 64, Handedness.RightHanded);
            new Slider("V", 0, 100, 50, new Vector2(10, 20), 230).Draw(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        [TestMethod]
        public void Parity_Panel()
        {
            using var lh = new VisualTestHelper(192, 128, Handedness.LeftHanded);
            new Panel(new Vector2(10, 10), new Vector2(160, 100)) { Title = "P" }.Draw(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(192, 128, Handedness.RightHanded);
            new Panel(new Vector2(10, 10), new Vector2(160, 100)) { Title = "P" }.Draw(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        // ==================== Three Stacked Sliders Test ====================

        [TestMethod]
        public void ThreeStackedSliders_LeftHanded()
        {
            using var helper = new VisualTestHelper(256, 160, Handedness.LeftHanded);
            DrawThreeStackedSliders(helper.Renderer);
            helper.AssertMatchesGolden("UI_ThreeSliders_LH");
        }

        [TestMethod]
        public void ThreeStackedSliders_RightHanded()
        {
            using var helper = new VisualTestHelper(256, 160, Handedness.RightHanded);
            DrawThreeStackedSliders(helper.Renderer);
            helper.AssertMatchesGolden("UI_ThreeSliders_RH");
        }

        [TestMethod]
        public void ThreeStackedSliders_Parity()
        {
            using var lh = new VisualTestHelper(256, 160, Handedness.LeftHanded);
            DrawThreeStackedSliders(lh.Renderer);
            using var lhImage = lh.Rasterize();

            using var rh = new VisualTestHelper(256, 160, Handedness.RightHanded);
            DrawThreeStackedSliders(rh.Renderer);
            using var rhImage = rh.Rasterize();

            GoldenImageHelper.AssertImagesIdentical(lhImage, rhImage);
        }

        // ==================== Helper Methods ====================

        private static void DrawStackedSlidersAB(Renderer2D renderer)
        {
            // Slider A at top with 25% value
            var sliderA = new Slider("A", 0, 100, 25, new Vector2(10, 20), 230);
            sliderA.Draw(renderer);

            // Slider B below with 75% value
            var sliderB = new Slider("B", 0, 100, 75, new Vector2(10, 60), 230);
            sliderB.Draw(renderer);
        }

        private static void DrawPanelWithSlidersAB(Renderer2D renderer)
        {
            var panel = new Panel(new Vector2(10, 10), new Vector2(260, 140))
            {
                Title = "SLIDERS"
            };

            var sliderA = new Slider();
            var sliderB = new Slider();

            panel.AddSliderGroup("VALUES", 40, 260,
                ("A", 0, 100, 30, sliderA),
                ("B", 0, 100, 70, sliderB));

            panel.Draw(renderer);
        }

        private static void DrawThreeStackedSliders(Renderer2D renderer)
        {
            // R/G/B color sliders stacked vertically
            var sliderR = new Slider("R", 0, 255, 200, new Vector2(10, 20), 230)
            {
                FillColor = new Vector4(1, 0.2f, 0.2f, 1)
            };
            var sliderG = new Slider("G", 0, 255, 128, new Vector2(10, 60), 230)
            {
                FillColor = new Vector4(0.2f, 1, 0.2f, 1)
            };
            var sliderB = new Slider("B", 0, 255, 50, new Vector2(10, 100), 230)
            {
                FillColor = new Vector4(0.2f, 0.2f, 1, 1)
            };

            sliderR.Draw(renderer);
            sliderG.Draw(renderer);
            sliderB.Draw(renderer);
        }

        private static void DrawUIManagerScene(Renderer2D renderer)
        {
            var manager = new UIManager();
            manager.Add(new Label("STATUS", new Vector2(10, 10)));
            manager.Add(new Button("RESET", new Vector2(10, 30), new Vector2(60, 24)));
            manager.Add(new Slider("X", 0, 100, 50, new Vector2(10, 65), 230));
            manager.Add(new Slider("Y", 0, 100, 25, new Vector2(10, 95), 230));
            manager.Draw(renderer);
        }

        private static void DrawCompositeUIScene(Renderer2D renderer)
        {
            // Panel with title, two stacked sliders, a button, and a label
            var panel = new Panel(new Vector2(10, 10), new Vector2(280, 180))
            {
                Title = "CONTROLS"
            };

            var sliderA = new Slider();
            var sliderB = new Slider();

            panel.AddSliderGroup("PARAMS", 40, 280,
                ("A", 0, 1, 0.3f, sliderA),
                ("B", 0, 1, 0.8f, sliderB));

            panel.Add(new Button("APPLY", new Vector2(20, 120), new Vector2(80, 28)));
            panel.Add(new Label("READY", new Vector2(110, 126)));

            panel.Draw(renderer);
        }
    }
}
