using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Layout;
using Radiant.Text;

namespace Radiant.UI.Core.Tests;

[TestClass]
public class LayoutTests
{
    private static RenderNode Only(UIRoot root) => root.RootRenderNode.Children.Single();

    [TestMethod]
    public void ARowPlacesChildrenSideBySide()
    {
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children =
            [
                new Box { Layout = new LayoutStyle { Width = 50, Height = 20 } },
                new Box { Layout = new LayoutStyle { Width = 30, Height = 20 } },
            ],
        });

        root.Update(new Vector2(400, 300));

        var row = Only(root);
        Assert.AreEqual(new Vector2(0, 0), row.Children[0].Position);
        Assert.AreEqual(new Vector2(50, 0), row.Children[1].Position);
        Assert.AreEqual(new Vector2(30, 20), row.Children[1].Size);
    }

    // A 40 × 20 box hugging, in a 200 × 100 parent laid out as asked.
    private static RenderNode Hugging(FlexDirection direction, Align? alignItems)
    {
        var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { FlexDirection = direction, AlignItems = alignItems, Width = 200, Height = 100 },
            Children = [new Box { Layout = new LayoutStyle { AlignSelf = Align.Hug, Width = 40, Height = 20 } }],
        });
        root.Update(new Vector2(400, 300));
        return Only(root).Children.Single();
    }

    [TestMethod]
    public void AHuggingBoxSitsAtTheStartWhereItsParentWouldStretchIt()
    {
        var inColumn = new UIRoot(new Box
        {
            Layout = new LayoutStyle { Width = 200 },
            Children = [new Box { Layout = new LayoutStyle { AlignSelf = Align.Hug, Height = 20, Padding = Edges.Symmetric(30, 0) } }],
        });
        inColumn.Update(new Vector2(400, 300));

        Assert.AreEqual(new Vector2(60, 20), Only(inColumn).Children.Single().Size, "keeps to its own width");
        Assert.AreEqual(Vector2.Zero, Only(inColumn).Children.Single().Position);
        Assert.AreEqual(Vector2.Zero, Hugging(FlexDirection.Row, null).Position, "at the top of a stretching row");
        inColumn.Dispose();
    }

    [TestMethod]
    public void AHuggingBoxFollowsItsParentsAlignmentOtherwise()
    {
        Assert.AreEqual(new Vector2(0, 40), Hugging(FlexDirection.Row, Align.Center).Position, "centred in a row");
        Assert.AreEqual(new Vector2(80, 0), Hugging(FlexDirection.Column, Align.Center).Position, "centred in a column");
        Assert.AreEqual(new Vector2(0, 80), Hugging(FlexDirection.Row, Align.FlexEnd).Position, "at a row's end");
    }

    [TestMethod]
    public void AHuggingBoxFollowsItsParentWhenTheParentChanges()
    {
        var centred = true;
        Element Tree() => new Box
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = centred ? Align.Center : null, Width = 200, Height = 100 },
            Children = [new Box { Layout = new LayoutStyle { AlignSelf = Align.Hug, Width = 40, Height = 20 } }],
        };
        using var root = new UIRoot(Tree());
        root.Update(new Vector2(400, 300));

        centred = false;
        root.SetRoot(Tree());
        root.Update(new Vector2(400, 300));

        Assert.AreEqual(Vector2.Zero, Only(root).Children.Single().Position);
    }

    [TestMethod]
    public void TheRootIsTheViewport()
    {
        using var root = new UIRoot(new Box { Layout = new LayoutStyle { FlexGrow = 1 } });

        root.Update(new Vector2(400, 300));

        Assert.AreEqual(new Vector2(400, 300), Only(root).Size);
    }

    [TestMethod]
    public void ResizingTheViewportLaysOutAgain()
    {
        using var root = new UIRoot(new Box { Layout = new LayoutStyle { FlexGrow = 1 } });
        root.Update(new Vector2(400, 300));

        root.Update(new Vector2(200, 100));

        Assert.AreEqual(new Vector2(200, 100), Only(root).Size);
    }

    [TestMethod]
    public void TextSizesItselfToItsText()
    {
        var style = new TextStyle { Size = 16 };
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { AlignItems = Align.FlexStart },
            Children = [new TextBlock("Hello") { Style = style }],
        });

        root.Update(new Vector2(400, 300));

        var expected = Paragraph.Layout("Hello", style);
        var text = Only(root).Children[0];
        Assert.AreEqual(System.MathF.Ceiling(expected.Width), text.Size.X);
        Assert.AreEqual(System.MathF.Ceiling(expected.Height), text.Size.Y);
    }

    [TestMethod]
    public void RecolouringTextKeepsItsShapingButResizingItDoesNot()
    {
        var style = new Signal<TextStyle>(new TextStyle { Size = 16, Color = new Vector4(1, 0, 0, 1) });
        using var root = new UIRoot(new Host(ctx => new TextBlock("Hello") { Style = ctx.Watch(style) }));
        root.Update(new Vector2(400, 300));
        var text = (TextRenderNode)Only(root);
        var shaped = text.LayoutAt(400);

        style.Value = style.Value with { Color = new Vector4(0, 0, 1, 1) };
        root.Update(new Vector2(400, 300));
        var recoloured = text.LayoutAt(400);
        style.Value = style.Value with { Size = 20 };
        root.Update(new Vector2(400, 300));

        Assert.AreSame(shaped, recoloured, "a theme's colour change is paint only");
        Assert.AreNotSame(shaped, text.LayoutAt(400));
    }

    private sealed record Host(System.Func<BuildContext, Element?> Body) : Component
    {
        public override Element? Build(BuildContext context) => Body(context);
    }

    [TestMethod]
    public void TextWrapsToTheWidthItIsGiven()
    {
        var style = new TextStyle { Size = 16 };
        using var root = new UIRoot(new Box
        {
            Layout = new LayoutStyle { Width = 80 },
            Children = [new TextBlock("The quick brown fox jumps over the lazy dog") { Style = style }],
        });

        root.Update(new Vector2(400, 300));

        var text = Only(root).Children[0];
        Assert.AreEqual(80, text.Size.X);
        Assert.IsTrue(text.Size.Y > 3 * style.Size, $"{text.Size.Y}");
    }

    [TestMethod]
    public void ChangedTextIsMeasuredAgain()
    {
        Element Tree(string text) => new Box
        {
            Layout = new LayoutStyle { AlignItems = Align.FlexStart },
            Children = [new TextBlock(text)],
        };
        using var root = new UIRoot(Tree("Hi"));
        root.Update(new Vector2(400, 300));
        var before = Only(root).Children[0].Size.X;

        root.SetRoot(Tree("Hello there"));
        root.Update(new Vector2(400, 300));

        Assert.IsTrue(Only(root).Children[0].Size.X > before);
    }

    [TestMethod]
    public void APropertyNoLongerSetGoesBackToItsDefault()
    {
        Element Tree(bool fixedWidth) => new Box
        {
            Children = [new Box { Layout = fixedWidth ? new LayoutStyle { Width = 50, Height = 10 } : new LayoutStyle { Height = 10 } }],
        };
        using var root = new UIRoot(Tree(true));
        root.Update(new Vector2(400, 300));

        root.SetRoot(Tree(false));
        root.Update(new Vector2(400, 300));

        Assert.AreEqual(400, Only(root).Children[0].Size.X, "stretched to the parent again");
    }
}
