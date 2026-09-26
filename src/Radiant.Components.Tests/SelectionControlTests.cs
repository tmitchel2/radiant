using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class SelectionControlTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), element));
        root.Update(Viewport);
        return root;
    }

    private static void Click(UIRoot root)
    {
        var node = root.GetSemantics().Children.Single();
        var centre = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(centre);
        root.PointerUp(centre);
    }

    [TestMethod]
    public void ACheckboxReportsTheValueItShouldBecome()
    {
        var changes = new List<bool>();
        using var root = Mount(new Checkbox(false, changes.Add) { Label = "Agree" });

        Click(root);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Space);

        CollectionAssert.AreEqual(new[] { true, true }, changes, "controlled: the prop didn't change, so both say true");
        var node = root.GetSemantics().Children.Single();
        Assert.AreEqual((SemanticsRole.CheckBox, "Agree", (bool?)false), (node.Role, node.Label, node.Semantics.Checked));
    }

    [TestMethod]
    public void AnIndeterminateCheckboxIsMixedToAssistiveTechnology()
    {
        using var root = Mount(new Checkbox(false, null) { Indeterminate = true });

        Assert.IsNull(root.GetSemantics().Children.Single().Semantics.Checked);
    }

    [TestMethod]
    public void ADisabledSwitchDoesNotChange()
    {
        var changes = new List<bool>();
        using var root = Mount(new Switch(true, changes.Add) { Disabled = true, Label = "Wi-Fi" });

        Click(root);

        Assert.AreEqual(0, changes.Count);
        var node = root.GetSemantics().Children.Single();
        Assert.AreEqual((SemanticsRole.Switch, true, true), (node.Role, node.Semantics.Checked, node.Semantics.Disabled));
    }

    [TestMethod]
    public void AnUnselectedRadioSelectsAndASelectedOneDoesNothing()
    {
        var selected = 0;
        using var off = Mount(new Radio(false, () => selected++));
        using var on = Mount(new Radio(true, () => selected++));

        Click(off);
        Click(on);

        Assert.AreEqual(1, selected);
    }

    [TestMethod]
    public void TheSwitchHandleSlidesWhenTurnedOn()
    {
        var state = new UI.Core.Signal<bool>(false);
        using var root = Mount(new Watcher(state));
        // Row > indicator slot > track > handle.
        float HandleX() => root.RootRenderNode.Children[0].Children[0].Children[^1].Children[^1].AbsolutePosition.X;
        var before = HandleX();

        state.Value = true;
        for (var i = 0; i < 60; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }

        Assert.AreEqual(20f - 4f, HandleX() - before, 0.5f, "centre moves 20 px while the handle grows 8 px");
    }

    private sealed record Watcher(UI.Core.Signal<bool> On) : Component
    {
        public override Element? Build(BuildContext context) => new Switch(context.Watch(On), null);
    }
}
