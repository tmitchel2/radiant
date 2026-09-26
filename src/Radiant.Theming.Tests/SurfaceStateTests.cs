using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.Theming.Tests;

[TestClass]
public class SurfaceStateTests
{
    private static SurfaceState Root => SurfaceState.Default;

    [TestMethod]
    public void TheRootIsContentOnThePlainSurface()
    {
        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Surface, false, false), Root.Surface);
        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Surface, true, false, Legibility.High), Root.Content);
    }

    [TestMethod]
    public void ASurfaceFamilyPaintsTheSurfaceAndItsOnColourTheContent()
    {
        var state = Root.With(new SurfaceChange { Surface = SurfaceName.Primary });

        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Primary, false, false), state.Surface);
        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Primary, true, false, Legibility.High), state.Content);
        Assert.AreEqual(state.Content, state.ContentFocused);
    }

    [TestMethod]
    public void TogglingContainerMovesSurfaceAndContentToTheContainerPair()
    {
        var state = Root.With(new SurfaceChange { Surface = SurfaceName.Primary, ToggleSurfaceContainer = true });

        Assert.IsTrue(state.Surface.Container && !state.Surface.On);
        Assert.IsTrue(state.Content.Container && state.Content.On);
    }

    [TestMethod]
    public void ContentInAnotherFamilyIsThatFamilysColour()
    {
        var state = Root.With(new SurfaceChange { Content = SurfaceName.Primary });

        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Primary, false, false, Legibility.High), state.Content);
        Assert.AreEqual(Root.Surface, state.Surface);
    }

    [TestMethod]
    public void ContentInTheSurfacesOwnFamilyIsItsOnColour()
    {
        var state = Root.With(new SurfaceChange { Surface = SurfaceName.Secondary }).With(new SurfaceChange { Content = SurfaceName.Secondary });

        Assert.IsTrue(state.Content.On);
    }

    [TestMethod]
    public void ContentInTheOwnFamilyOfAContainerIsTheContainersOnColour()
    {
        // A text button on a primary container: onPrimaryContainer, not onPrimary (white on pale).
        var state = Root.With(new SurfaceChange { Surface = SurfaceName.Primary, ToggleSurfaceContainer = true }).With(new SurfaceChange { Content = SurfaceName.Primary });

        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Primary, true, true, Legibility.High), state.Content);
    }

    [TestMethod]
    public void LegibilitySetsOpacity()
    {
        var state = Root.With(new SurfaceChange { ContentLegibility = Legibility.Medium, SurfaceLegibility = 2f });

        Assert.AreEqual(Legibility.Medium, state.Content.Opacity);
        Assert.AreEqual(Legibility.Medium, state.ContentFocused.Opacity);
        Assert.AreEqual(1f, state.Surface.Opacity, "clamped");
    }

    [TestMethod]
    public void AnErrorOnThePlainSurfaceColoursOnlyTheContent()
    {
        var state = Root.With(new SurfaceChange { ShowError = true });

        Assert.AreEqual(Root.Surface, state.Surface);
        Assert.AreEqual(SurfaceName.Error, state.Content.Name);
        Assert.IsFalse(state.Content.On);
    }

    [TestMethod]
    public void AnErrorOnAColouredSurfaceTurnsTheWholeSurfaceToError()
    {
        var state = Root.With(new SurfaceChange { Surface = SurfaceName.Primary, ShowError = true });

        Assert.AreEqual(SurfaceName.Error, state.Surface.Name);
        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Error, true, false, Legibility.High), state.Content);
    }

    [TestMethod]
    public void DisabledOnAColouredSurfaceFadesItToTheOnSurfaceColour()
    {
        var state = Root.With(new SurfaceChange { Surface = SurfaceName.Primary, ShowDisabled = true });

        Assert.AreEqual(new SurfaceRoleState(SurfaceName.Surface, true, false, Legibility.VeryLow), state.Surface);
        Assert.AreEqual(Legibility.Low, state.Content.Opacity);
    }

    [TestMethod]
    public void DisabledOnThePlainSurfaceFadesOnlyTheContent()
    {
        var state = Root.With(new SurfaceChange { ShowDisabled = true });

        Assert.AreEqual(Root.Surface, state.Surface);
        Assert.AreEqual(Legibility.Low, state.Content.Opacity);
    }

    [TestMethod]
    public void FocusedContentCanTakeItsOwnFamily()
    {
        var state = Root.With(new SurfaceChange { ContentFocused = SurfaceName.Primary });

        Assert.AreEqual(SurfaceName.Primary, state.ContentFocused.Name);
        Assert.AreEqual(Root.Content, state.Content);
    }
}
