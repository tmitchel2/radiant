using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Radiant.Components;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Templates.Tests;

/// <summary>Mounts templates in a themed root and drives them through their semantics.</summary>
internal static class TemplateHarness
{
    public static readonly Vector2 Viewport = new(1000, 800);

    public static ImageSource Picture { get; } = ImageSource.FromBgra(1, 1, [200, 150, 100, 255]);

    public static UIRoot Mount(Element element)
    {
        var root = new UIRoot(new ThemeProvider(new ThemeController(), new SnackbarHost(new Box
        {
            Layout = new LayoutStyle { FlexGrow = 1, Padding = Edges.All(20) },
            Children = [element],
        })));
        Settle(root);
        return root;
    }

    public static void Settle(UIRoot root, int frames = 30)
    {
        root.Update(Viewport);
        for (var i = 0; i < frames; i++)
        {
            root.Advance(1 / 60.0);
            root.Update(Viewport);
        }
    }

    public static IEnumerable<SemanticsNode> All(UIRoot root) => All(root.GetSemantics());

    public static SemanticsNode Find(UIRoot root, SemanticsRole role, string label) =>
        All(root).FirstOrDefault(n => n.Role == role && n.Label == label)
        ?? throw new InvalidOperationException($"no {role} \"{label}\" in: {string.Join(", ", All(root).Select(n => $"{n.Role} {n.Label}"))}");

    public static bool Shows(UIRoot root, string text) => All(root).Any(n => n.Label == text);

    public static void Click(UIRoot root, SemanticsNode node)
    {
        var at = new Vector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
        root.PointerDown(at);
        root.PointerUp(at);
        Settle(root);
    }

    public static void Type(UIRoot root, SemanticsNode field, string text)
    {
        Click(root, field);
        root.TextInput(text);
        Settle(root);
    }

    public static SemanticsNode? TryFind(UIRoot root, SemanticsRole role, string label) =>
        All(root).FirstOrDefault(n => n.Role == role && n.Label == label);

    private static IEnumerable<SemanticsNode> All(SemanticsNode node) => node.Children.SelectMany(All).Prepend(node);
}
