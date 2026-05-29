using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Graphics2D;
using Radiant.Styling;
using Radiant.UI;

namespace Radiant.Tests.Styling;

[TestClass]
public class StyleResolverTests
{
    private static readonly Vector4 Red = new(1, 0, 0, 1);
    private static readonly Vector4 Green = new(0, 1, 0, 1);
    private static readonly Vector4 Blue = new(0, 0, 1, 1);

    [TestMethod]
    public void TypeSelectorMatchesAndApplies()
    {
        var sheet = new Stylesheet().Add(Selectors.OfType<Button>(), new Style { BackgroundColor = Red });
        var button = new Button(TestFonts.Default);

        var resolved = StyleResolver.Resolve(button, sheet);

        Assert.AreEqual(Red, resolved.BackgroundColor);
    }

    [TestMethod]
    public void ClassSelectorMatchesOnlyTaggedElements()
    {
        var sheet = new Stylesheet().Add(Selectors.Class("primary"), new Style { BackgroundColor = Green });
        var tagged = new Button(TestFonts.Default);
        tagged.Classes.Add("primary");
        var plain = new Button(TestFonts.Default);

        Assert.AreEqual(Green, StyleResolver.Resolve(tagged, sheet).BackgroundColor);
        Assert.IsNull(StyleResolver.Resolve(plain, sheet).BackgroundColor);
    }

    [TestMethod]
    public void LaterEqualPriorityRuleWins()
    {
        var sheet = new Stylesheet()
            .Add(Selectors.Any(), new Style { BackgroundColor = Red })
            .Add(Selectors.Any(), new Style { BackgroundColor = Green });
        var element = new Label(TestFonts.Default);

        Assert.AreEqual(Green, StyleResolver.Resolve(element, sheet).BackgroundColor);
    }

    [TestMethod]
    public void HigherPriorityWinsRegardlessOfOrder()
    {
        var sheet = new Stylesheet()
            .Add(Selectors.Any(), new Style { BackgroundColor = Green }, priority: 10)
            .Add(Selectors.Any(), new Style { BackgroundColor = Red }, priority: 0);
        var element = new Label(TestFonts.Default);

        Assert.AreEqual(Green, StyleResolver.Resolve(element, sheet).BackgroundColor);
    }

    [TestMethod]
    public void UnsetPropertiesMergeRatherThanOverwrite()
    {
        var sheet = new Stylesheet()
            .Add(Selectors.Any(), new Style { BackgroundColor = Red, BorderWidth = 2f })
            .Add(Selectors.Any(), new Style { BackgroundColor = Green }); // leaves BorderWidth unset
        var element = new Label(TestFonts.Default);

        var resolved = StyleResolver.Resolve(element, sheet);
        Assert.AreEqual(Green, resolved.BackgroundColor); // overridden
        Assert.AreEqual(2f, resolved.BorderWidth);        // preserved from first rule
    }

    [TestMethod]
    public void InlineStyleWinsOverStylesheet()
    {
        var sheet = new Stylesheet().Add(Selectors.Any(), new Style { BackgroundColor = Red });
        var element = new Label(TestFonts.Default) { Style = new Style { BackgroundColor = Blue } };

        Assert.AreEqual(Blue, StyleResolver.Resolve(element, sheet).BackgroundColor);
    }

    [TestMethod]
    public void HoverPseudoSelectorFollowsButtonState()
    {
        var sheet = new Stylesheet()
            .Add(Selectors.OfType<Button>(), new Style { BackgroundColor = Red })
            .Add(Selectors.OfType<Button>().And(Selectors.Hovered), new Style { BackgroundColor = Green });
        var button = new HoverableButton(TestFonts.Default);

        Assert.AreEqual(Red, StyleResolver.Resolve(button, sheet).BackgroundColor);
        button.Hovering = true;
        Assert.AreEqual(Green, StyleResolver.Resolve(button, sheet).BackgroundColor);
    }

    [TestMethod]
    public void DisabledPseudoSelectorFollowsEnabledFlag()
    {
        var sheet = new Stylesheet().Add(Selectors.Disabled, new Style { TextColor = Red });
        var element = new Label(TestFonts.Default);

        Assert.IsNull(StyleResolver.Resolve(element, sheet).TextColor);
        element.Enabled = false;
        Assert.AreEqual(Red, StyleResolver.Resolve(element, sheet).TextColor);
    }

    /// <summary>Test double exposing a settable hover state without simulating mouse input.</summary>
    private sealed class HoverableButton(MsdfFont font) : Button(font)
    {
        public bool Hovering { get; set; }

        public override PseudoState CurrentPseudoState =>
            Hovering ? PseudoState.Hover : PseudoState.None;
    }
}
