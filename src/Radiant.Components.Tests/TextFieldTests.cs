using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components.Tests;

[TestClass]
public class TextFieldTests
{
    private static readonly Vector2 Viewport = new(600, 400);

    private static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new Box { Layout = new Radiant.Layout.LayoutStyle { Padding = Radiant.Layout.Edges.All(20), AlignItems = Radiant.Layout.Align.FlexStart }, Children = [element] }));
        Settle(root);
        return root;
    }

    private static void Settle(UIRoot root)
    {
        root.Update(Viewport);
        for (var i = 0; i < 30; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    private static IEnumerable<RenderNode> All(RenderNode node) => node.Children.SelectMany(All).Prepend(node);

    private static TextBlock LabelOf(UIRoot root, string label) =>
        All(root.RootRenderNode).OfType<TextRenderNode>().Select(t => t.Element).First(t => t.Text == label);

    private static SemanticsNode Field(UIRoot root) => root.GetSemantics().Children.Single(n => n.Role == SemanticsRole.TextField);

    [TestMethod]
    public void TheLabelFloatsAndShrinksWhenTheFieldIsFocused()
    {
        using var root = Mount(new TextField("Name"));
        var resting = LabelOf(root, "Name").Style.Size;

        var field = Field(root);
        root.PointerDown(new Vector2(field.Bounds.X + 30, field.Bounds.Y + 20));
        root.PointerUp(new Vector2(field.Bounds.X + 30, field.Bounds.Y + 20));
        Settle(root);

        Assert.AreEqual(16f, resting);
        Assert.AreEqual(12f, LabelOf(root, "Name").Style.Size, 0.01f);
        Assert.IsTrue(Field(root).IsFocused);
    }

    [TestMethod]
    public void AnUncontrolledFieldKeepsWhatIsTyped()
    {
        using var root = Mount(new TextField("Name") { InitialText = "Ada" });
        var field = Field(root);
        root.PointerDown(new Vector2(field.Bounds.Right - 5, field.Bounds.Y + 30));
        root.PointerUp(new Vector2(field.Bounds.Right - 5, field.Bounds.Y + 30));
        Settle(root);

        root.TextInput("m");
        Settle(root);

        Assert.AreEqual("Adam", Field(root).Semantics.Value);
    }

    [TestMethod]
    public void AControlledFieldReportsEditsAndShowsWhatItIsGiven()
    {
        var changes = new List<string>();
        using var root = Mount(new TextField("Name") { Value = TextEditState.From("fixed"), OnChange = s => changes.Add(s.Text) });
        var field = Field(root);
        root.PointerDown(new Vector2(field.Bounds.Right - 5, field.Bounds.Y + 30));
        root.PointerUp(new Vector2(field.Bounds.Right - 5, field.Bounds.Y + 30));
        Settle(root);

        root.TextInput("!");
        Settle(root);

        CollectionAssert.AreEqual(new[] { "fixed!" }, changes);
        Assert.AreEqual("fixed", Field(root).Semantics.Value);
    }

    [TestMethod]
    public void AnErrorReplacesTheSupportingTextInTheErrorColour()
    {
        using var root = Mount(new TextField("Code") { SupportingText = "Six digits", Error = "Expired", MaxLength = 6, InitialText = "123" });
        var texts = All(root.RootRenderNode).OfType<TextRenderNode>().Select(t => t.Element).ToList();

        var error = texts.Single(t => t.Text == "Expired");
        Assert.IsFalse(texts.Any(t => t.Text == "Six digits"));
        Assert.AreEqual((Vector4)ResolvedTheme.Default.Get(SurfaceName.Error), error.Style.Color);
        Assert.IsTrue(texts.Any(t => t.Text == "3/6"));
    }

    [TestMethod]
    public void PressingTheLeadingIconFocusesTheInput()
    {
        using var root = Mount(new TextField("Email") { LeadingIcon = "mail", Variant = TextFieldVariant.Outlined });
        var field = Field(root);

        root.PointerDown(new Vector2(field.Bounds.X - 20, field.Bounds.Y + 20));
        root.PointerUp(new Vector2(field.Bounds.X - 20, field.Bounds.Y + 20));
        Settle(root);

        Assert.IsTrue(Field(root).IsFocused);
    }
}
