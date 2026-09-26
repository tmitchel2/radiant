using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Platform;

namespace Radiant.UI.Core.Tests;

/// <summary>The UI's side of the platform: cursors from hover, text input clients, the platform context.</summary>
[TestClass]
public class PlatformTests
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

    // A link (pointing hand) holding a text span (I-beam), beside a splitter (resize), in a box
    // that sets nothing.
    private static Box Scene(CursorShape? spanCursor = CursorShape.IBeam) => new()
    {
        Layout = new LayoutStyle { FlexGrow = 1 },
        Children =
        [
            new Box
            {
                Layout = At(0, 0, 100, 100),
                Cursor = CursorShape.PointingHand,
                Children = [new Box { Layout = At(10, 10, 20, 20), Cursor = spanCursor }],
            },
            new Box { Layout = At(200, 0, 10, 100), Cursor = CursorShape.ResizeLeftRight },
        ],
    };

    [TestMethod]
    public void TheCursorIsTheDeepestHoveredBoxsShape()
    {
        using var root = Mount(Scene());
        Assert.AreEqual(CursorShape.Arrow, root.Cursor);

        root.PointerMove(new Vector2(50, 50));
        Assert.AreEqual(CursorShape.PointingHand, root.Cursor);

        root.PointerMove(new Vector2(15, 15));
        Assert.AreEqual(CursorShape.IBeam, root.Cursor);

        root.PointerMove(new Vector2(205, 50));
        Assert.AreEqual(CursorShape.ResizeLeftRight, root.Cursor);

        root.PointerMove(new Vector2(300, 200));
        Assert.AreEqual(CursorShape.Arrow, root.Cursor);
    }

    [TestMethod]
    public void ABoxWithoutAShapeTakesItsParents()
    {
        using var root = Mount(Scene(spanCursor: null));

        root.PointerMove(new Vector2(15, 15));

        Assert.AreEqual(CursorShape.PointingHand, root.Cursor);
    }

    [TestMethod]
    public void APressKeepsItsCursorWhereverThePointerGoes()
    {
        using var root = Mount(Scene());
        root.PointerMove(new Vector2(205, 50));
        root.PointerDown(new Vector2(205, 50));

        // Dragging the splitter off itself keeps the resize cursor...
        root.PointerMove(new Vector2(300, 200));
        Assert.AreEqual(CursorShape.ResizeLeftRight, root.Cursor);

        // ...until the button is released.
        root.PointerUp(new Vector2(300, 200));
        Assert.AreEqual(CursorShape.Arrow, root.Cursor);
    }

    [TestMethod]
    public void CursorChangedIsRaisedOnlyForChanges()
    {
        using var root = Mount(Scene());
        var shapes = new List<CursorShape>();
        root.CursorChanged += shapes.Add;

        root.PointerMove(new Vector2(50, 50));
        root.PointerMove(new Vector2(60, 60));
        root.PointerMove(new Vector2(15, 15));
        root.PointerMove(new Vector2(300, 200));

        CollectionAssert.AreEqual(new[] { CursorShape.PointingHand, CursorShape.IBeam, CursorShape.Arrow }, shapes);
    }

    [TestMethod]
    public void AHoveredBoxChangingItsShapeChangesTheCursor()
    {
        var busy = new Signal<bool>(false);
        using var root = Mount(new Lambda(context => new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1 },
            Cursor = context.Watch(busy) ? CursorShape.NotAllowed : CursorShape.PointingHand,
        }));
        root.PointerMove(new Vector2(10, 10));
        Assert.AreEqual(CursorShape.PointingHand, root.Cursor);

        busy.Value = true;
        root.Update(Viewport);

        Assert.AreEqual(CursorShape.NotAllowed, root.Cursor);
    }

    [TestMethod]
    public void SettingTheTextInputClientRaisesAChange()
    {
        using var root = Mount(new Box());
        var client = new FakeClient();
        var changes = new List<ITextInputClient?>();
        root.TextInputClientChanged += changes.Add;

        root.TextInputClient = client;
        root.TextInputClient = client;
        root.TextInputClient = null;

        CollectionAssert.AreEqual(new ITextInputClient?[] { client, null }, changes);
    }

    [TestMethod]
    public void TheBindingShowsTheCursorThroughThePlatform()
    {
        using var root = Mount(Scene());
        var platform = new HeadlessPlatform();
        using var binding = new PlatformBinding(root);
        root.PointerMove(new Vector2(50, 50));

        // Attaching brings the platform up to date...
        binding.Attach(platform);
        Assert.AreEqual(CursorShape.PointingHand, platform.Cursors.Current);

        // ...and changes follow, once each.
        root.PointerMove(new Vector2(15, 15));
        root.PointerMove(new Vector2(16, 16));
        Assert.AreEqual(CursorShape.IBeam, platform.Cursors.Current);
        Assert.AreEqual(2, platform.Cursors.ShowCount);
    }

    [TestMethod]
    public void TheBindingFocusesTheClientInThePlatformsTextInput()
    {
        using var root = Mount(new Box());
        var platform = new HeadlessPlatform();
        using var binding = new PlatformBinding(root);
        binding.Attach(platform);
        var client = new FakeClient();

        root.TextInputClient = client;
        Assert.AreSame(client, platform.TextInput.Client);

        // Composition reaches the client from the platform.
        platform.TextInput.Compose("你", 1);
        platform.TextInput.Type("你好");
        CollectionAssert.AreEqual(new[] { "marked:你:1:0", "insert:你好" }, client.Calls);

        root.TextInputClient = null;
        Assert.IsNull(platform.TextInput.Client);
    }

    [TestMethod]
    public void AClientSetBeforeTheWindowOpensIsFocusedOnAttach()
    {
        using var root = Mount(new Box());
        var client = new FakeClient();
        root.TextInputClient = client;
        var platform = new HeadlessPlatform();
        using var binding = new PlatformBinding(root);

        binding.Attach(platform);

        Assert.AreSame(client, platform.TextInput.Client);
    }

    [TestMethod]
    public void AMovingCaretMovesTheCandidateWindow()
    {
        using var root = Mount(new Box());
        var platform = new HeadlessPlatform();
        using var binding = new PlatformBinding(root);
        binding.Attach(platform);
        var client = new FakeClient { CaretRect = new RectangleF(10, 10, 1, 16) };
        root.TextInputClient = client;

        binding.AfterUpdate();
        Assert.AreEqual(0, platform.TextInput.CaretInvalidations, "the caret hasn't moved since focus");

        client.CaretRect = new RectangleF(18, 10, 1, 16);
        binding.AfterUpdate();
        binding.AfterUpdate();
        Assert.AreEqual(1, platform.TextInput.CaretInvalidations);
    }

    [TestMethod]
    public void DisposingTheBindingUnfocusesAndDisposesThePlatform()
    {
        using var root = Mount(Scene());
        var platform = new CountingPlatform();
        var binding = new PlatformBinding(root);
        binding.Attach(platform);
        root.TextInputClient = new FakeClient();

        binding.Dispose();
        root.PointerMove(new Vector2(50, 50));

        Assert.IsNull(platform.TextInput.Client);
        Assert.AreEqual(1, platform.Disposals);
        Assert.AreEqual(CursorShape.Arrow, platform.Cursors.Current, "no longer forwarded");
    }

    [TestMethod]
    public void ComponentsReadThePlatformFromContext()
    {
        IPlatform? seen = null;
        var platform = new HeadlessPlatform();
        using var root = Mount(PlatformContext.Platform.Provide(platform, new Lambda(context =>
        {
            seen = context.UsePlatform();
            return null;
        })));

        Assert.AreSame(platform, seen);
    }

    [TestMethod]
    public void WithoutAProviderThePlatformIsHeadless()
    {
        IPlatform? seen = null;
        using var root = Mount(new Lambda(context =>
        {
            seen = context.UsePlatform();
            return null;
        }));

        Assert.IsInstanceOfType<HeadlessPlatform>(seen);
    }

    private sealed class FakeClient : ITextInputClient
    {
        public List<string> Calls { get; } = [];

        public RectangleF CaretRect { get; set; }

        public void InsertText(string text) => Calls.Add($"insert:{text}");

        public void SetMarkedText(string text, int selectionStart, int selectionLength) =>
            Calls.Add($"marked:{text}:{selectionStart}:{selectionLength}");

        public void UnmarkText() => Calls.Add("unmark");
    }

    private sealed class CountingPlatform : IPlatform
    {
        private readonly HeadlessPlatform _inner = new();

        public int Disposals { get; private set; }

        public string Name => "counting";

        public IClipboard Clipboard => _inner.Clipboard;

        public HeadlessCursorService Cursors => _inner.Cursors;

        ICursorService IPlatform.Cursors => Cursors;

        public IAppearance Appearance => _inner.Appearance;

        public IFileDialogs Dialogs => _inner.Dialogs;

        public HeadlessTextInput TextInput => _inner.TextInput;

        ITextInput IPlatform.TextInput => TextInput;

        public IWindowChrome Chrome => _inner.Chrome;

        public void Dispose() => Disposals++;
    }
}
