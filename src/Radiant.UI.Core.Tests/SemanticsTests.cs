using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class SemanticsTests
{
    [TestMethod]
    public void ControlsAreNamedByTheirTextAndPlainBoxesDisappear()
    {
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Box { Children = [new TextBlock("Title")] },
                new Box
                {
                    Focusable = true,
                    Semantics = new Semantics { Role = SemanticsRole.Button },
                    Children = [new Box { Children = [new TextBlock("Save")] }],
                },
                new Box { Semantics = new Semantics { Role = SemanticsRole.CheckBox, Label = "Remember me", Checked = true } },
            ],
        });
        root.Update(new Vector2(400, 300));

        var tree = root.GetSemantics();

        Assert.AreEqual(3, tree.Children.Count, tree.ToString());
        Assert.AreEqual((SemanticsRole.Text, "Title"), (tree.Children[0].Role, tree.Children[0].Label));
        Assert.AreEqual((SemanticsRole.Button, "Save"), (tree.Children[1].Role, tree.Children[1].Label));
        Assert.AreEqual(0, tree.Children[1].Children.Count, "the button's text is its name, not a child");
        Assert.IsTrue(tree.Children[1].IsFocusable);
        Assert.AreEqual(true, tree.Children[2].Semantics.Checked);
        Assert.AreEqual("Remember me", tree.Children[2].Label);
    }

    [TestMethod]
    public void NodesKeepTheirIdsFromOneTreeToTheNext()
    {
        using var root = new UIRoot(new Box { Children = [new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Save" } }] });
        root.Update(new Vector2(400, 300));

        var first = root.GetSemantics().Children.Single().Id;
        root.Update(new Vector2(500, 300));

        Assert.AreNotEqual(0, first);
        Assert.AreEqual(first, root.GetSemantics().Children.Single().Id);
    }

    [TestMethod]
    public void PressingANodeClicksItAndFocusesIt()
    {
        var clicks = 0;
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Save" }, Layout = new Radiant.Layout.LayoutStyle { Width = 50, Height = 20 }, OnClick = _ => clicks++ },
                // Something drawn over it doesn't take the press.
                new Box { Layout = new Radiant.Layout.LayoutStyle { Position = Radiant.Layout.PositionType.Absolute, Width = 100, Height = 100 } },
            ],
        });
        root.Update(new Vector2(400, 300));
        var save = root.GetSemantics().Children.Single(n => n.Label == "Save");

        var pressed = root.Press(save.Id);
        root.Update(new Vector2(400, 300));

        Assert.IsTrue(pressed);
        Assert.AreEqual(1, clicks);
        Assert.AreEqual(save.Id, root.FocusedId);
        Assert.IsFalse(root.Press(987654), "no such node");
    }

    [TestMethod]
    public void FocusingANodeMovesKeyboardFocusThere()
    {
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "One" } },
                new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Two" } },
                new Box { Semantics = new Semantics { Role = SemanticsRole.Text, Label = "Not focusable" } },
            ],
        });
        root.Update(new Vector2(400, 300));
        var nodes = root.GetSemantics().Children;

        Assert.IsTrue(root.FocusNode(nodes[1].Id));
        Assert.IsFalse(root.FocusNode(nodes[2].Id));
        root.Update(new Vector2(400, 300));
        Assert.IsTrue(root.GetSemantics().Children[1].IsFocused);
    }

    [TestMethod]
    public void TextCanBeAHeading()
    {
        using var root = new UIRoot(new Box { Children = [new TextBlock("Settings") { HeadingLevel = 1 }, new TextBlock("Body")] });
        root.Update(new Vector2(400, 300));

        var nodes = root.GetSemantics().Children;

        Assert.AreEqual((SemanticsRole.Heading, 1, "Settings"), (nodes[0].Role, nodes[0].Semantics.HeadingLevel, nodes[0].Label));
        Assert.AreEqual(SemanticsRole.Text, nodes[1].Role);
    }

    [TestMethod]
    public void TheFocusedNodeIsMarked()
    {
        using var root = new UIRoot(new Box { Children = [new Box { Focusable = true }, new Box { Focusable = true }] });
        root.Update(new Vector2(400, 300));
        root.MoveFocus(forward: true);
        root.MoveFocus(forward: true);

        var tree = root.GetSemantics();

        CollectionAssert.AreEqual(new[] { false, true }, tree.Children.Select(c => c.IsFocused).ToArray());
    }
}
