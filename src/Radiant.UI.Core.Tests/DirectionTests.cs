using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Scrolling;
using Radiant.Text;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class DirectionTests
{
    private static readonly Vector2 Viewport = new(400, 300);

    private static Box Sized(ElementRef element, float width) => new() { Ref = element, Layout = new LayoutStyle { Width = width, Height = 20 } };

    [TestMethod]
    public void ARowRunsFromTheRightRightToLeft()
    {
        var (first, second) = (new ElementRef(), new ElementRef());
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children = [Sized(first, 50), Sized(second, 30)],
        }));

        root.Update(Viewport);

        Assert.AreEqual(350f, first.Bounds.X);
        Assert.AreEqual(320f, second.Bounds.X);
    }

    [TestMethod]
    public void StartPaddingAndInsetAreOnTheRightRightToLeft()
    {
        var (padded, placed) = (new ElementRef(), new ElementRef());
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = new Edges(10, 0, 0, 0) },
            Children =
            [
                Sized(padded, 50),
                new Box
                {
                    Ref = placed,
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(5, 0, Dimension.Undefined, Dimension.Undefined), Width = 40, Height = 20 },
                },
            ],
        }));

        root.Update(Viewport);

        Assert.AreEqual(340f, padded.Bounds.X);
        Assert.AreEqual(355f, placed.Bounds.X);
    }

    [TestMethod]
    public void LeftToRightIsTheDefault()
    {
        var (first, second) = (new ElementRef(), new ElementRef());
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children = [Sized(first, 50), Sized(second, 30)],
        });

        root.Update(Viewport);

        Assert.AreEqual(0f, first.Bounds.X);
        Assert.AreEqual(50f, second.Bounds.X);
    }

    [TestMethod]
    public void APointFromTheLeftIsPlacedThereRightToLeft()
    {
        var item = new ElementRef();
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new Box
        {
            Children =
            [
                new Portal(new Box
                {
                    Layout = new LayoutStyle
                    {
                        Position = PositionType.Absolute,
                        Inset = Edges.Physical(100, 100, Dimension.Undefined, Dimension.Undefined, rightToLeft: true),
                        Width = 100,
                        Height = 20,
                        FlexDirection = FlexDirection.Row,
                    },
                    Children = [Sized(item, 20)],
                }),
            ],
        }));

        root.Update(Viewport);

        // Its left edge is where it was asked for; its content reads from the right.
        Assert.AreEqual(180f, item.Bounds.X);
    }

    [TestMethod]
    public void APortalsContentTakesTheDirectionWhereThePortalIs()
    {
        var item = new ElementRef();
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new Box
        {
            Children =
            [
                new Portal(new Box
                {
                    Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, Width = 100, Height = 20 },
                    Children = [Sized(item, 20)],
                }),
            ],
        }));

        root.Update(Viewport);

        Assert.AreEqual(380f, item.Bounds.X);
    }

    [TestMethod]
    public void ComponentsReadTheDirectionInForce()
    {
        TextDirection? seen = TextDirection.LeftToRight;
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new Lambda(context => { seen = context.UseDirection(); return null; })));

        root.Update(Viewport);

        Assert.AreEqual(TextDirection.RightToLeft, seen);
    }

    [TestMethod]
    public void AHorizontalScrollAreaStartsAtTheRightAndScrollsLeftwards()
    {
        var (first, last) = (new ElementRef(), new ElementRef());
        var controller = new ScrollController(new ScrollBehaviour { Axes = ScrollAxes.Horizontal });
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new ScrollArea
        {
            Controller = controller,
            Behaviour = controller.Behaviour,
            Layout = new LayoutStyle { Width = 400, Height = 50 },
            ContentLayout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children = [Sized(first, 500), Sized(last, 500)],
        }));
        root.Update(Viewport);
        Assert.AreEqual(-100f, first.Bounds.X);

        controller.ScrollTo(new Vector2(600, 0), animated: false);
        root.Update(Viewport);

        // Scrolled to the end, the last item's left edge is the area's.
        Assert.AreEqual(0f, last.Bounds.X);
    }

    [TestMethod]
    public void TextTakesTheDirectionItIsLaidOutIn()
    {
        using var root = new UIRoot(new Directionality(TextDirection.RightToLeft, new Box
        {
            Children =
            [
                new TextBlock("Right to left"),
                new Box { Layout = new LayoutStyle { Direction = TextDirection.LeftToRight }, Children = [new TextBlock("Code")] },
            ],
        }));

        root.Update(Viewport);

        var texts = Descendants(root.RootRenderNode).OfType<TextRenderNode>().ToArray();
        Assert.AreEqual(TextDirection.RightToLeft, texts[0].LayoutAt(float.PositiveInfinity).Style.Direction);
        Assert.IsNull(texts[1].LayoutAt(float.PositiveInfinity).Style.Direction);
    }

    private static System.Collections.Generic.IEnumerable<RenderNode> Descendants(RenderNode node) => node.Children.SelectMany(Descendants).Prepend(node);
}
