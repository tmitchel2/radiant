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
