using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class ComponentTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element, ThemeController? themes = null)
    {
        var root = new UIRoot(new ThemeProvider(themes ?? new ThemeController(), element));
        root.Update(Viewport);
        return root;
    }

    private static IEnumerable<RenderNode> All(RenderNode node) => node.Children.SelectMany(All).Prepend(node);

    private static BoxRenderNode FirstBox(UIRoot root) => All(root.RootRenderNode).OfType<BoxRenderNode>().First();

    private static TextRenderNode FirstText(UIRoot root) => All(root.RootRenderNode).OfType<TextRenderNode>().First();

    private static Vector2 Centre(RenderNode node) => node.AbsolutePosition + node.Size / 2;

    // Runs frames until state-layer fades and other transitions have finished.
    private static void Settle(UIRoot root)
    {
        root.Update(Viewport);
        for (var i = 0; i < 60; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    [TestMethod]
    public void AFilledButtonIsPrimaryWithOnPrimaryText()
    {
        var theme = ResolvedTheme.Default;
        using var root = Mount(new SurfaceButton("Save"));

        Assert.AreEqual((Vector4)theme.Get(SurfaceName.Primary), FirstBox(root).Element.Background);
        var state = SurfaceState.Default.With(new SurfaceChange { Surface = SurfaceName.Primary });
        Assert.AreEqual((Vector4)theme.ContentColor(state), FirstText(root).Element.Style.Color);
        Assert.AreEqual("Save", FirstText(root).Element.Text);
    }

    [TestMethod]
    public void VariantsTakeTheirColours()
    {
        var theme = ResolvedTheme.Default;
        using var tonal = Mount(new SurfaceButton("x", ButtonVariant.Tonal));
        using var outlined = Mount(new SurfaceButton("x", ButtonVariant.Outlined));

        Assert.AreEqual((Vector4)theme.Get(SurfaceName.Secondary, container: true), FirstBox(tonal).Element.Background);
        var state = SurfaceState.Default.With(new SurfaceChange { Surface = SurfaceName.Secondary, ToggleSurfaceContainer = true });
        Assert.AreEqual((Vector4)theme.ContentColor(state), FirstText(tonal).Element.Style.Color);
        Assert.IsNull(FirstBox(outlined).Element.Background);
        Assert.AreEqual(1f, FirstBox(outlined).Element.BorderWidth);
    }

    [TestMethod]
    public void AFacetSetOnTheButtonOverridesItsVariant()
    {
        using var root = Mount(new SurfaceButton("Delete") { SurfaceColor = SurfaceName.Error });

        Assert.AreEqual((Vector4)ResolvedTheme.Default.Get(SurfaceName.Error), FirstBox(root).Element.Background);
    }

    [TestMethod]
    public void ClickingOrPressingEnterPresses()
    {
        var presses = 0;
        using var root = Mount(new SurfaceButton("Go") { OnPress = () => presses++ });
        var centre = Centre(FirstBox(root));

        root.PointerDown(centre);
        root.PointerUp(centre);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Enter);
        root.KeyDown(KeyCode.Space);

        Assert.AreEqual(3, presses);
    }

    [TestMethod]
    public void ADisabledButtonDoesNotPressOrTakeFocus()
    {
        var presses = 0;
        using var root = Mount(new SurfaceButton("Go") { OnPress = () => presses++, ShowDisabled = true });
        var centre = Centre(FirstBox(root));

        root.PointerDown(centre);
        root.PointerUp(centre);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Enter);

        Assert.AreEqual(0, presses);
        Assert.IsTrue(root.GetSemantics().Children.Single().Semantics.Disabled);
    }

    [TestMethod]
    public void HoverShowsAStateLayerOverTheSurface()
    {
        var theme = ResolvedTheme.Default;
        using var root = Mount(new SurfaceButton("Go"));
        var button = FirstBox(root);

        root.PointerMove(Centre(button));
        Settle(root);

        var layer = (BoxRenderNode)FirstBox(root).Children[0];
        var state = SurfaceState.Default.With(new SurfaceChange { Surface = SurfaceName.Primary });
        Assert.AreEqual((Vector4)theme.StateLayerColor(state, theme.Theme.StateLayers.Hover), layer.Element.Background);
        Assert.IsFalse(layer.Element.HitTestVisible);

        root.PointerMove(new Vector2(590, 390));
        Settle(root);
        Assert.IsFalse(FirstBox(root).Children.OfType<BoxRenderNode>().Any(), "the layer goes when the pointer does");
    }

    [TestMethod]
    public void AButtonIsAButtonToAssistiveTechnologyNamedByItsText()
    {
        using var root = Mount(new SurfaceButton("Save"));

        var node = root.GetSemantics().Children.Single();

        Assert.AreEqual((SemanticsRole.Button, "Save"), (node.Role, node.Label));
    }

    [TestMethod]
    public void TextOnACardIsReadableOnTheCard()
    {
        var theme = ResolvedTheme.Default;
        using var root = Mount(new Card(new SurfaceText("Hello")));

        var state = SurfaceState.Default.With(new SurfaceChange { Surface = SurfaceName.SurfaceContainerLow });
        Assert.AreEqual((Vector4)theme.ContentColor(state), FirstText(root).Element.Style.Color);
        Assert.AreEqual(2, FirstBox(root).Element.Shadows.Count, "elevation 1");
    }

    [TestMethod]
    public void ContentLegibilityIsMixedIntoTheSurfaceSoTextStaysOpaque()
    {
        using var root = Mount(new SurfaceText("Quiet") { Legibility = Legibility.Medium });

        var color = FirstText(root).Element.Style.Color;

        Assert.AreEqual(1f, color.W);
        var surface = ResolvedTheme.Default.SurfaceColor(SurfaceState.Default);
        var content = ResolvedTheme.Default.Get(SurfaceName.Surface, on: true, container: true);
        Assert.IsTrue(color.X > content.R && color.X < surface.R, "between the content and surface colours");
    }

    [TestMethod]
    public void ChangingTheThemeRecoloursComponents()
    {
        var themes = new ThemeController();
        using var root = Mount(new SurfaceButton("Go"), themes);
        var before = FirstBox(root).Element.Background;

        themes.Set(new Theme { Colors = new ThemeColors { IsDark = true } });
        root.Update(Viewport);

        Assert.AreNotEqual(before, FirstBox(root).Element.Background);
        Assert.AreEqual((Vector4)themes.Current.Value.Get(SurfaceName.Primary), FirstBox(root).Element.Background);
    }

    [TestMethod]
    public void ChangingTheShapeScaleReshapesComponents()
    {
        var themes = new ThemeController();
        using var root = Mount(new Card(new SurfaceText("x")), themes);

        themes.Set(new Theme { Shape = new ShapeScale().Scaled(0f) });
        root.Update(Viewport);

        Assert.AreEqual(0f, FirstBox(root).Element.CornerRadii.TopLeft);
    }
}
