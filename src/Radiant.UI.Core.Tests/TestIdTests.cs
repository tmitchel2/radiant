using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

/// <summary>A component that declares a part, so its root is named after it.</summary>
internal sealed partial record Card : Component
{
    [TestId] public static partial string Heading { get; }

    public override Element? Build(BuildContext context) =>
        new Box { Children = [new TextBlock("Hello") { TestId = Heading }] };
}

/// <summary>A component that is nothing but a card.</summary>
internal sealed partial record Panel : Component
{
    [TestId] public static partial string Unused { get; }

    public override Element? Build(BuildContext context) => new Card();
}

/// <summary>A component drawn in a portal.</summary>
internal sealed partial record Popup : Component
{
    [TestId] public static partial string Body { get; }

    public override Element? Build(BuildContext context) => new Portal(new Box { Children = [new TextBlock("Popped") { TestId = Body }] });
}

/// <summary>A component that draws two things, so has no single root to name.</summary>
internal sealed partial record Pair : Component
{
    [TestId] public static partial string Left { get; }

    public override Element? Build(BuildContext context) => new Fragment(new TextBlock("A") { TestId = Left }, new TextBlock("B"));
}

[TestClass]
public class TestIdTests
{
    private static SemanticsNode Tree(Element element)
    {
        using var root = new UIRoot(new Box { Children = [element] });
        root.Update(new Vector2(400, 300));
        return root.GetSemantics();
    }

    private static SemanticsNode[] All(SemanticsNode node) => [node, .. node.Children.SelectMany(All)];

    [TestMethod]
    public void DeclaredPartsAreNamedByTheirComponent()
    {
        Assert.AreEqual("Card.Heading", Card.Heading);
    }

    [TestMethod]
    public void AComponentsRootIsNamedAfterItAndHoldsItsParts()
    {
        var card = Tree(new Card()).Children.Single();

        Assert.AreEqual("Card", card.TestId);
        Assert.AreEqual(SemanticsRole.None, card.Role);
        Assert.AreEqual("Card.Heading", card.Children.Single().TestId);
    }

    [TestMethod]
    public void AnExplicitIdBeatsTheGeneratedName()
    {
        Assert.AreEqual("login", Tree(new Card { TestId = "login" }).Children.Single().TestId);
    }

    [TestMethod]
    public void TheOutermostGeneratedNameWins()
    {
        Assert.AreEqual("Panel", Tree(new Panel()).Children.Single().TestId);
    }

    [TestMethod]
    public void APortalledComponentIsAScopeToo()
    {
        var popup = All(Tree(new Popup())).Single(n => n.TestId == "Popup");

        Assert.AreEqual("Popup.Body", popup.Children.Single().TestId);
    }

    [TestMethod]
    public void AComponentWithSeveralRootsIsNotNamedButItsPartsAre()
    {
        var nodes = All(Tree(new Pair()));

        Assert.IsFalse(nodes.Any(n => n.TestId == "Pair"));
        Assert.IsTrue(nodes.Any(n => n.TestId == "Pair.Left"));
    }
}
