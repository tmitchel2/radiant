using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Scrolling;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class InspectionTests
{
    private static readonly Vector2 Window = new(400, 300);

    private static LayoutStyle At(float x, float y, float width, float height) => new()
    {
        Position = PositionType.Absolute,
        Inset = new Edges(x, y, Dimension.Undefined, Dimension.Undefined),
        Width = width,
        Height = height,
    };

    private static UINode Node(UIRoot root, string testId) =>
        root.GetSemantics() is var tree && Find(tree, testId) is { } found
            ? root.FindNode(found.Id)!
            : throw new AssertFailedException($"No @{testId} in\n{tree}");

    private static SemanticsNode? Find(SemanticsNode node, string testId) =>
        node.TestId == testId ? node : node.Children.Select(child => Find(child, testId)).FirstOrDefault(found => found is not null);

    [TestMethod]
    public void ANodeKnowsWhereItIsAndWhatItIs()
    {
        using var root = new UIRoot(new Box
        {
            Children = [new Box { TestId = "card", Layout = At(10, 20, 100, 50), Children = [new TextBlock("Hello") { TestId = "greeting" }] }],
        });
        root.Update(Window);

        var card = Node(root, "card");
        var greeting = Node(root, "greeting");

        Assert.AreEqual(UINodeKind.Box, card.Kind);
        Assert.AreEqual(new System.Drawing.RectangleF(10, 20, 100, 50), card.Bounds);
        Assert.AreEqual(UINodeKind.Text, greeting.Kind);
        Assert.AreEqual("Hello", greeting.Text);
        Assert.IsTrue(card.IsAncestorOf(greeting));
        Assert.AreEqual(card, greeting.Parent);
        Assert.AreEqual(1f, card.VisibleRatio);
    }

    [TestMethod]
    public void WhatAClipCutsOffCannotBeSeen()
    {
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Box
                {
                    ClipContent = true,
                    Layout = At(0, 0, 100, 100),
                    Children = [new Box { TestId = "half", Layout = At(50, 0, 100, 100) }, new Box { TestId = "out", Layout = At(200, 0, 10, 10) }],
                },
                new Box { TestId = "offscreen", Layout = At(390, 0, 20, 20) },
            ],
        });
        root.Update(Window);

        Assert.AreEqual(new System.Drawing.RectangleF(50, 0, 50, 100), Node(root, "half").VisibleBounds);
        Assert.AreEqual(0.5f, Node(root, "half").VisibleRatio, 0.001f);
        Assert.IsNull(Node(root, "out").VisibleBounds);
        Assert.AreEqual(0.5f, Node(root, "offscreen").VisibleRatio, 0.001f, "the window cuts it too");
    }

    [TestMethod]
    public void TransparentNodesCannotBeSeen()
    {
        using var root = new UIRoot(new Box
        {
            Children = [new Box { Opacity = 0f, Layout = At(0, 0, 50, 50), Children = [new Box { TestId = "ghost", Layout = At(0, 0, 10, 10) }] }],
        });
        root.Update(Window);

        Assert.AreEqual(0f, Node(root, "ghost").EffectiveOpacity);
        Assert.IsNull(Node(root, "ghost").VisibleBounds);
    }

    [TestMethod]
    public void BoundsFollowTransforms()
    {
        using var root = new UIRoot(new Box
        {
            Children = [new Box { Layout = At(100, 100, 40, 20), Transform = Matrix3x2.CreateScale(2), TransformOrigin = Vector2.Zero, Children = [new Box { TestId = "inner", Layout = At(10, 5, 10, 5) }] }],
        });
        root.Update(Window);

        var inner = Node(root, "inner");

        Assert.AreEqual(new System.Drawing.RectangleF(120, 110, 20, 10), inner.Bounds);
        Assert.AreEqual(new Vector2(125, 112.5f), inner.ToRoot(new Vector2(2.5f, 1.25f)));
        Assert.AreEqual(new Vector2(2.5f, 1.25f), inner.ToLocal(new Vector2(125, 112.5f)));
    }

    [TestMethod]
    public void HitTestingSaysWhatIsOnTop()
    {
        using var root = new UIRoot(new Box
        {
            Children =
            [
                new Box { TestId = "button", Layout = At(0, 0, 50, 50) },
                new Portal(new Box { TestId = "scrim", Layout = At(0, 0, 400, 300) }),
            ],
        });
        root.Update(Window);

        var hits = root.HitTest(new Vector2(25, 25));

        Assert.AreEqual(Node(root, "scrim"), hits[0]);
        Assert.IsFalse(hits.Contains(Node(root, "button")), "the scrim covers it");
        Assert.AreEqual(UINodeKind.Root, hits[^1].Kind);
    }

    [TestMethod]
    public void ScrollAreasShowWhereTheyAreScrolled()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(new ScrollArea
        {
            TestId = "list",
            Controller = controller,
            Layout = new LayoutStyle { Width = 200, Height = 100 },
            Children = [.. Enumerable.Range(0, 10).Select(i => (Element?)new Box { TestId = $"row{i}", Layout = new LayoutStyle { Height = 20 } })],
        });
        root.Update(Window);

        var list = Node(root, "list");
        var scroll = list.Scroll!;

        Assert.AreEqual(UINodeKind.Scroll, list.Kind);
        Assert.AreEqual(SemanticsRole.ScrollArea, root.GetSemantics().Children.Single().Role);
        Assert.AreEqual(new Vector2(0, 100), scroll.MaxOffset);
        Assert.IsTrue(scroll.CanScrollVertical);
        Assert.IsNull(Node(root, "row7").VisibleBounds);
    }

    [TestMethod]
    public void ScrollingIntoViewBringsANodeIn()
    {
        var controller = new ScrollController(new ScrollBehaviour());
        using var root = new UIRoot(new ScrollArea
        {
            Controller = controller,
            Layout = new LayoutStyle { Width = 200, Height = 100 },
            Children = [.. Enumerable.Range(0, 10).Select(i => (Element?)new Box { TestId = $"row{i}", Layout = new LayoutStyle { Height = 20 } })],
        });
        root.Update(Window);

        Assert.IsTrue(root.ScrollIntoView(Node(root, "row7").Id));

        Assert.AreEqual(new Vector2(0, 60), controller.Offset, "as little as brings it to the bottom edge");
        Assert.AreEqual(1f, Node(root, "row7").VisibleRatio);
        Assert.IsFalse(root.ScrollIntoView(987654));
    }

    [TestMethod]
    public void AComponentsTestIdNamesWhatItDraws()
    {
        var card = new Lambda(_ => new Box { TestId = "inner", Semantics = new Semantics { Role = SemanticsRole.Group } }) { TestId = "outer" };
        using var root = new UIRoot(new Box { Children = [card, new Box { Semantics = new Semantics { Role = SemanticsRole.Button, TestId = "declared" } }] });
        root.Update(Window);

        var tree = root.GetSemantics();

        Assert.AreEqual("outer", tree.Children[0].TestId, "the outermost, where the component is used, wins");
        Assert.AreEqual("declared", tree.Children[1].TestId);
    }

    [TestMethod]
    public void AComponentWithSeveralChildrenDoesNotNameThem()
    {
        var pair = new Lambda(_ => new Fragment(new Box { Focusable = true }, new Box { Focusable = true })) { TestId = "pair" };
        using var root = new UIRoot(new Box { Children = [pair] });
        root.Update(Window);

        Assert.IsTrue(root.GetSemantics().Children.All(c => c.TestId is null));
    }

    [TestMethod]
    public void ABoxNamedOnlyForTestsIsInTheTreeWithNoRoleOrLabel()
    {
        using var root = new UIRoot(new Box { Children = [new Box { TestId = "panel", Children = [new TextBlock("Hi")] }] });
        root.Update(Window);

        var panel = root.GetSemantics().Children.Single();

        Assert.AreEqual(SemanticsRole.None, panel.Role);
        Assert.IsNull(panel.Label);
        Assert.AreEqual("Hi", panel.Children.Single().Label);
    }

    [TestMethod]
    public void IdleWaitsForAnimationsButNotSpinnersOrTimers()
    {
        using var root = new UIRoot(new Box());
        Assert.IsFalse(root.IsIdle, "not mounted");
        root.Update(Window);
        Assert.IsTrue(root.IsIdle);

        using (root.AddTicker(_ => { }, TickerKind.Continuous, "spinner"))
        using (root.AddTicker(_ => { }, TickerKind.Timer, "tooltip"))
        {
            Assert.IsTrue(root.IsIdle);
            Assert.IsTrue(root.NeedsUpdate, "they still get frames");
            CollectionAssert.AreEqual(new[] { "spinner" }, root.RunningTickers(TickerKind.Continuous).ToArray());
        }

        var fade = root.AddTicker(_ => { }, TickerKind.Animation, "fade");
        Assert.IsFalse(root.IsIdle);
        CollectionAssert.Contains(root.BusyReasons().ToArray(), "animation: fade");
        fade.Dispose();
        Assert.IsTrue(root.IsIdle);
    }

    [TestMethod]
    public async Task BusyWorkKeepsTheUIFromIdle()
    {
        using var root = new UIRoot(new Box());
        root.Update(Window);
        var frames = 0;
        root.FrameRequested = () => frames++;
        var finish = new TaskCompletionSource();

        root.TrackBusy(finish.Task, "loading orders");
        Assert.IsFalse(root.IsIdle);
        CollectionAssert.Contains(root.BusyReasons().ToArray(), "busy: loading orders");

        finish.SetResult();
        await Task.Yield();

        Assert.IsTrue(root.IsIdle);
        Assert.IsTrue(frames > 0, "finishing asks for a frame");
        using (root.BeginBusy("saving"))
        {
            Assert.IsFalse(root.IsIdle);
        }
        Assert.IsTrue(root.IsIdle);
    }

    [TestMethod]
    public void InputIsReportedWithWhatItReached()
    {
        using var root = new UIRoot(new Box { Children = [new Box { TestId = "target", Focusable = true, Layout = At(0, 0, 50, 50) }] });
        root.Update(Window);
        var target = Node(root, "target").Id;
        var events = new System.Collections.Generic.List<UIInputEvent>();
        root.InputReceived += events.Add;

        root.PointerDown(new Vector2(10, 10));
        root.PointerUp(new Vector2(10, 10));
        root.TextInput("x");
        root.KeyDown(KeyCode.A, KeyModifiers.Shift);
        root.Wheel(new Vector2(300, 200), new Vector2(0, 40));

        CollectionAssert.AreEqual(
            new[] { UIInputType.PointerDown, UIInputType.PointerUp, UIInputType.Text, UIInputType.KeyDown, UIInputType.Wheel },
            events.Select(e => e.Type).ToArray());
        Assert.AreEqual(target, events[0].TargetId);
        Assert.AreEqual(target, events[2].TargetId, "text goes to the focused node");
        Assert.AreEqual("x", events[2].Text);
        Assert.AreEqual(KeyModifiers.Shift, events[3].Modifiers);
        Assert.AreNotEqual(target, events[4].TargetId);
    }

    [TestMethod]
    [DataRow("Tab", KeyCode.Tab, KeyModifiers.None)]
    [DataRow("ctrl+shift+s", KeyCode.S, KeyModifiers.Control | KeyModifiers.Shift)]
    [DataRow("Alt+F4", KeyCode.F4, KeyModifiers.Alt)]
    [DataRow("Esc", KeyCode.Escape, KeyModifiers.None)]
    [DataRow("Super+/", KeyCode.Slash, KeyModifiers.Super)]
    [DataRow("Shift+7", KeyCode.Number7, KeyModifiers.Shift)]
    [DataRow("PageDown", KeyCode.PageDown, KeyModifiers.None)]
    public void ChordsParse(string text, KeyCode key, KeyModifiers modifiers)
    {
        Assert.IsTrue(KeyChord.TryParse(text, out var chord));
        Assert.AreEqual(new KeyChord(key, modifiers), chord);
        Assert.IsTrue(KeyChord.TryParse(chord.ToInvariantString(), out var again));
        Assert.AreEqual(chord, again);
    }

    [TestMethod]
    public void CmdIsTheCommandModifier()
    {
        Assert.IsTrue(KeyChord.TryParse("Cmd+S", out var chord));
        Assert.AreEqual(KeyChord.Command(KeyCode.S), chord);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Hyper+S")]
    [DataRow("Ctrl+")]
    [DataRow("Ctrl+Banana")]
    public void NonChordsDoNot(string text) => Assert.IsFalse(KeyChord.TryParse(text, out _));
}
