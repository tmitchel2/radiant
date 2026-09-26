using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

/// <summary>A colour a component can be given, and pass on.</summary>
[StyleFacet]
public interface IHasFill
{
    /// <summary>The fill colour.</summary>
    Vector4? Fill { get; init; }
}

/// <summary>A part that shows its fill.</summary>
public sealed partial record Swatch : Component, IHasFill
{
    public override Element? Build(BuildContext context) => new Box { Background = Fill };
}

/// <summary>A component that passes its fill to its swatch.</summary>
[ForwardFacets(typeof(Swatch), "Swatch", typeof(IHasFill))]
public sealed partial record Palette : Component, IHasFill
{
    public Vector4? SwatchDefault { get; init; }

    public override Element? Build(BuildContext context) => ForwardSwatch(new Swatch { Fill = SwatchDefault });
}

[TestClass]
public class FacetTests
{
    private static Vector4? SwatchBackground(Element element)
    {
        using var root = new UIRoot(element);
        root.Update(new Vector2(100, 100));
        return ((BoxRenderNode)root.RootRenderNode.Children.Single()).Element.Background;
    }

    [TestMethod]
    public void AForwardedFacetReachesThePart()
    {
        Assert.AreEqual(new Vector4(1, 0, 0, 1), SwatchBackground(new Palette { Fill = new Vector4(1, 0, 0, 1) }));
    }

    [TestMethod]
    public void AnUnsetFacetLeavesThePartsOwnValue()
    {
        Assert.AreEqual(new Vector4(0, 0, 1, 1), SwatchBackground(new Palette { SwatchDefault = new Vector4(0, 0, 1, 1) }));
    }
}
