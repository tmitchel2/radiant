using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class EditCommandTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    // An app with the standard Edit menu, a text field, and a button that can take focus instead.
    private sealed record App(Signal<TextEditState> Value) : Component
    {
        public override Element? Build(BuildContext context)
        {
            context.UseStandardEditMenu();
            return new Box
            {
                Layout = new LayoutStyle { Padding = Edges.All(10), RowGap = 10, AlignItems = Align.FlexStart },
                Children =
                [
                    new TextInput(context.Watch(Value), next => Value.Value = next)
                    {
                        Label = "Name",
                        Style = new TextStyle { Size = 16 },
                        Layout = new LayoutStyle { Width = 300 },
                    },
                    new Box { Focusable = true, Semantics = new Semantics { Role = SemanticsRole.Button, Label = "Other" }, Layout = new LayoutStyle { Width = 40, Height = 20 } },
                ],
            };
        }
    }

    private static (UIRoot Root, Signal<TextEditState> Value) Mount(string text)
    {
        var value = new Signal<TextEditState>(TextEditState.From(text));
        var root = new UIRoot(new App(value));
        root.Update(Viewport);
        return (root, value);
    }

    private static void FocusField(UIRoot root)
    {
        root.PointerDown(new Vector2(305, 15));
        root.PointerUp(new Vector2(305, 15));
        root.Update(Viewport);
    }

    private static void FocusOther(UIRoot root)
    {
        var other = root.GetSemantics().Children.SelectMany(Descend).First(n => n.Label == "Other");
        root.FocusNode(other.Id);
        root.Update(Viewport);

        static System.Collections.Generic.IEnumerable<SemanticsNode> Descend(SemanticsNode n) => n.Children.SelectMany(Descend).Prepend(n);
    }

    [TestMethod]
    public void WithNothingFocusedTheEditMenuIsThereButGreyed()
    {
        var (root, _) = Mount("abc");
        using var _root = root;

        var edit = root.Commands.Commands.Where(c => c.Menu == EditCommands.Menu).ToArray();

        CollectionAssert.AreEqual(new[] { "Undo", "Redo", "Cut", "Copy", "Paste", "Select All" }, edit.Select(c => c.Title).ToArray());
        Assert.IsTrue(edit.All(c => !c.Enabled));
    }

    [TestMethod]
    public void AFocusedFieldTakesTheEditCommands()
    {
        var (root, value) = Mount("abc");
        using var _root = root;
        FocusField(root);

        Assert.IsTrue(root.Commands.Find(EditCommands.SelectAll)!.Enabled);
        Assert.IsFalse(root.Commands.Find(EditCommands.Copy)!.Enabled, "nothing is selected yet");
        Assert.IsTrue(root.Commands.Execute(EditCommands.SelectAll));
        root.Update(Viewport);
        Assert.AreEqual((0, 3), (value.Value.Selection.Start, value.Value.Selection.End));

        Assert.IsTrue(root.Commands.Execute(EditCommands.Cut));
        root.Update(Viewport);
        Assert.AreEqual("", value.Value.Text);
        Assert.IsTrue(root.Commands.Find(EditCommands.Undo)!.Enabled);
        root.Commands.Execute(EditCommands.Paste);
        root.Update(Viewport);
        root.Commands.Execute(EditCommands.Paste);
        root.Update(Viewport);
        Assert.AreEqual("abcabc", value.Value.Text);

        root.Commands.Execute(EditCommands.Undo);
        root.Update(Viewport);
        Assert.AreEqual("abc", value.Value.Text);
    }

    [TestMethod]
    public void FocusLeavingTheFieldGreysTheMenuAgain()
    {
        var (root, value) = Mount("abc");
        using var _root = root;
        FocusField(root);
        root.Commands.Execute(EditCommands.SelectAll);
        root.Update(Viewport);
        Assert.IsTrue(root.Commands.Find(EditCommands.Copy)!.Enabled);

        FocusOther(root);

        Assert.IsFalse(root.Commands.Find(EditCommands.Copy)!.Enabled);
        Assert.IsFalse(root.Commands.Execute(EditCommands.Cut));
        Assert.AreEqual("abc", value.Value.Text);
    }

    [TestMethod]
    public void AScopedCommandsShortcutGivesWayWhenFocusIsElsewhere()
    {
        var log = new Log();
        var inner = new ElementRef();
        var chord = KeyChord.Command(KeyCode.D);
        using var root = new UIRoot(new Lambda(context =>
        {
            context.UseCommand(new Command("do", "App") { Shortcut = chord, Run = () => log.Add("app") });
            return new Box
            {
                Children =
                [
                    new Lambda(scoped =>
                    {
                        scoped.UseCommand(new Command("do", "Panel") { Shortcut = chord, FocusScoped = true, Run = () => log.Add("panel") });
                        return new Box { Ref = inner, Focusable = true, Layout = new LayoutStyle { Width = 20, Height = 20 } };
                    }),
                ],
            };
        }));
        root.Update(Viewport);

        root.KeyDown(chord.Key, chord.Modifiers);
        Assert.AreEqual("App", root.Commands.Find("do")!.Title);
        inner.Focus();
        root.Update(Viewport);
        root.KeyDown(chord.Key, chord.Modifiers);

        CollectionAssert.AreEqual(new[] { "app", "panel" }, log.Entries);
        Assert.AreEqual("Panel", root.Commands.Find("do")!.Title);
    }

    [TestMethod]
    public void ADisabledCommandsShortcutGoesOnToOthers()
    {
        var log = new Log();
        var chord = KeyChord.Command(KeyCode.D);
        using var root = new UIRoot(new Lambda(context =>
        {
            context.UseShortcut(chord, () => log.Add("shortcut"));
            return new Lambda(inner =>
            {
                inner.UseCommand(new Command("do", "Do") { Shortcut = chord, Enabled = false, Run = () => log.Add("command") });
                return null;
            });
        }));
        root.Update(Viewport);

        root.KeyDown(chord.Key, chord.Modifiers);

        CollectionAssert.AreEqual(new[] { "shortcut" }, log.Entries);
    }
}
