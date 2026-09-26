using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class InputTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static LayoutStyle At(float x, float y, float w, float h) => new()
    {
        Position = PositionType.Absolute,
        Inset = new Edges(x, y, Dimension.Undefined, Dimension.Undefined),
        Width = w,
        Height = h,
    };

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(element);
        root.Update(Viewport);
        return root;
    }

    [TestMethod]
    public void AClickReachesTheBoxUnderThePointerAndBubbles()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            OnClick = _ => log.Add("outer"),
            Children = [new Box { Layout = At(10, 10, 50, 50), OnClick = e => log.Add($"inner {e.LocalPosition}") }],
        });

        root.PointerDown(new Vector2(20, 30));
        root.PointerUp(new Vector2(20, 30));

        CollectionAssert.AreEqual(new[] { "inner <10, 20>", "outer" }, log.Entries);
    }

    [TestMethod]
    public void AHandledEventStopsBubbling()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            OnPointerDown = _ => log.Add("outer"),
            Children = [new Box { Layout = At(0, 0, 50, 50), OnPointerDown = e => e.Handled = true }],
        });

        root.PointerDown(new Vector2(10, 10));

        Assert.AreEqual(0, log.Entries.Count);
    }

    [TestMethod]
    public void CaptureHandlersRunOnTheWayDownBeforeBubbleHandlers()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            OnPointerDownCapture = _ => log.Add("outer capture"),
            OnPointerDown = _ => log.Add("outer"),
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 50, 50),
                    OnPointerDownCapture = _ => log.Add("inner capture"),
                    OnPointerDown = _ => log.Add("inner"),
                },
            ],
        });

        root.PointerDown(new Vector2(10, 10));

        CollectionAssert.AreEqual(new[] { "outer capture", "inner capture", "inner", "outer" }, log.Entries);
    }

    [TestMethod]
    public void LaterSiblingsAreOnTop()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box { Layout = At(0, 0, 50, 50), OnPointerDown = _ => log.Add("below") },
                new Box { Layout = At(25, 25, 50, 50), OnPointerDown = _ => log.Add("above") },
            ],
        });

        root.PointerDown(new Vector2(30, 30));

        CollectionAssert.AreEqual(new[] { "above" }, log.Entries);
    }

    [TestMethod]
    public void ABoxThatIsNotHitTestVisibleLetsThePointerThrough()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box { Layout = At(0, 0, 50, 50), OnPointerDown = _ => log.Add("below") },
                new Box { Layout = At(0, 0, 50, 50), HitTestVisible = false, OnPointerDown = _ => log.Add("overlay") },
            ],
        });

        root.PointerDown(new Vector2(10, 10));

        CollectionAssert.AreEqual(new[] { "below" }, log.Entries);
    }

    [TestMethod]
    public void ClippedChildrenCannotBeHitOutsideTheClip()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 50, 50),
                    ClipContent = true,
                    Children = [new Box { Layout = At(40, 40, 50, 50), OnPointerDown = _ => log.Add("child") }],
                },
            ],
        });

        root.PointerDown(new Vector2(70, 70));
        root.PointerDown(new Vector2(45, 45));

        CollectionAssert.AreEqual(new[] { "child" }, log.Entries);
    }

    [TestMethod]
    public void TransformedBoxesAreHitWhereTheyAreDrawn()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(100, 100, 100, 20),
                    // A quarter turn about the centre (150, 110): now 20 wide, 100 tall.
                    Transform = Matrix3x2.CreateRotation(System.MathF.PI / 2),
                    OnPointerDown = e => log.Add("hit"),
                },
            ],
        });

        root.PointerDown(new Vector2(150, 70));
        root.PointerDown(new Vector2(110, 110));

        CollectionAssert.AreEqual(new[] { "hit" }, log.Entries);
    }

    [TestMethod]
    public void EnterAndLeaveFollowThePointer()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 100, 100),
                    OnPointerEnter = _ => log.Add("enter outer"),
                    OnPointerLeave = _ => log.Add("leave outer"),
                    Children =
                    [
                        new Box
                        {
                            Layout = At(10, 10, 20, 20),
                            OnPointerEnter = _ => log.Add("enter inner"),
                            OnPointerLeave = _ => log.Add("leave inner"),
                        },
                    ],
                },
            ],
        });

        root.PointerMove(new Vector2(50, 50));
        root.PointerMove(new Vector2(15, 15));
        root.PointerMove(new Vector2(300, 300));

        CollectionAssert.AreEqual(
            new[] { "enter outer", "enter inner", "leave inner", "leave outer" },
            log.Entries);
    }

    [TestMethod]
    public void APressKeepsItsMovesAndReleaseAndOnlyClicksWhereItEnds()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 50, 50),
                    OnPointerMove = e => log.Add($"move {e.LocalPosition}"),
                    OnPointerUp = _ => log.Add("up"),
                    OnClick = _ => log.Add("click"),
                },
            ],
        });

        root.PointerDown(new Vector2(10, 10));
        root.PointerMove(new Vector2(200, 10));
        root.PointerUp(new Vector2(200, 10));

        CollectionAssert.AreEqual(new[] { "move <200, 10>", "up" }, log.Entries);
    }

    [TestMethod]
    public void QuickSecondClicksCountAsDoubleClicks()
    {
        var counts = new System.Collections.Generic.List<int>();
        var now = 0.0;
        using var root = Mount(new Box { Layout = new LayoutStyle { FlexGrow = 1 }, OnClick = e => counts.Add(e.ClickCount) });
        root.Clock = () => now;

        root.PointerDown(new Vector2(10, 10));
        root.PointerUp(new Vector2(10, 10));
        now = 0.2;
        root.PointerDown(new Vector2(11, 10));
        root.PointerUp(new Vector2(11, 10));
        now = 2;
        root.PointerDown(new Vector2(11, 10));
        root.PointerUp(new Vector2(11, 10));

        CollectionAssert.AreEqual(new[] { 1, 2, 1 }, counts);
    }

    [TestMethod]
    public void PressingAFocusableBoxFocusesItWithoutARing()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Children =
            [
                new Box
                {
                    Layout = At(0, 0, 50, 50),
                    Focusable = true,
                    OnFocus = e => log.Add($"focus {e.IsFocusVisible}"),
                    Children = [new Box { Layout = At(0, 0, 10, 10) }],
                },
            ],
        });

        root.PointerDown(new Vector2(5, 5));

        CollectionAssert.AreEqual(new[] { "focus False" }, log.Entries);
        Assert.IsFalse(root.IsFocusVisible);
    }

    [TestMethod]
    public void TabMovesFocusInTreeOrderAndShowsTheRing()
    {
        var log = new Log();
        Box Field(string name, int tabIndex = 0) => new()
        {
            Focusable = true,
            TabIndex = tabIndex,
            OnFocus = _ => log.Add($"focus {name}"),
            OnBlur = _ => log.Add($"blur {name}"),
        };
        using var root = Mount(new Box { Children = [Field("a"), Field("skipped", -1), new Box { Children = [Field("b")] }] });

        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Tab);
        root.KeyDown(KeyCode.Tab, KeyModifiers.Shift);

        CollectionAssert.AreEqual(
            new[] { "focus a", "blur a", "focus b", "blur b", "focus a", "blur a", "focus b" },
            log.Entries);
        Assert.IsTrue(root.IsFocusVisible);
    }

    [TestMethod]
    public void KeysGoToTheFocusedBoxAndBubble()
    {
        var log = new Log();
        using var root = Mount(new Box
        {
            OnKeyDown = e => log.Add($"outer {e.Key}"),
            Children = [new Box { Focusable = true, OnKeyDown = e => log.Add($"inner {e.Key}"), OnTextInput = e => log.Add(e.Text) }],
        });
        root.MoveFocus(forward: true);

        root.KeyDown(KeyCode.Enter);
        root.TextInput("é");

        CollectionAssert.AreEqual(new[] { "inner Enter", "outer Enter", "é" }, log.Entries);
    }

    [TestMethod]
    public void AHandledTabDoesNotMoveFocus()
    {
        var focused = new Log();
        using var root = Mount(new Box
        {
            Children =
            [
                new Box { Focusable = true, OnKeyDown = e => e.Handled = e.Key == KeyCode.Tab, OnFocus = _ => focused.Add("a") },
                new Box { Focusable = true, OnFocus = _ => focused.Add("b") },
            ],
        });
        root.MoveFocus(forward: true);

        root.KeyDown(KeyCode.Tab);

        CollectionAssert.AreEqual(new[] { "a" }, focused.Entries);
    }

    [TestMethod]
    public void RemovingTheFocusedBoxClearsFocus()
    {
        Element Tree(bool show) => new Box { Children = [show ? new Box { Focusable = true } : null] };
        using var root = Mount(Tree(true));
        root.MoveFocus(forward: true);
        Assert.IsNotNull(root.FocusedNode);

        root.SetRoot(Tree(false));
        root.Update(Viewport);

        Assert.IsNull(root.FocusedNode);
    }

    [TestMethod]
    public void HandlersAreTheLatestBuildsEvenWhenOnlyTheyChanged()
    {
        var log = new Log();
        Element Tree(string label) => new Box { Layout = new LayoutStyle { FlexGrow = 1 }, OnPointerDown = _ => log.Add(label) };
        using var root = Mount(Tree("first"));

        root.SetRoot(Tree("second"));
        root.Update(Viewport);
        root.PointerDown(new Vector2(5, 5));

        CollectionAssert.AreEqual(new[] { "second" }, log.Entries);
        Assert.IsTrue(root.HitPath(new Vector2(5, 5)).Count > 0);
    }
}
