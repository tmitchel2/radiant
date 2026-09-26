using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Where a page is in its hierarchy: links to each level above it, separated by chevrons, then the
/// current page in plain text. Past <see cref="MaxVisible"/> steps, the middle ones fold into a
/// "…" button that shows them.
/// </summary>
/// <param name="Crumbs">The steps, from the top down; the last is the current page.</param>
public sealed record Breadcrumb(IReadOnlyList<Crumb> Crumbs) : Component
{
    /// <summary>The most steps shown before the middle ones fold away.</summary>
    public int MaxVisible { get; init; } = 4;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var expanded = context.UseState(false);
        // Folded: the first, "…", and as many from the end as fit in what's left.
        var fold = !expanded.Value && Crumbs.Count > MaxVisible;
        var tail = Math.Max(1, MaxVisible - 2);
        var children = new List<Element?>();
        for (var i = 0; i < Crumbs.Count; i++)
        {
            if (fold && i == 1)
            {
                children.Add(Separator());
                children.Add(new IconButton("more_horiz", "Show all steps") { OnPress = () => expanded.Set(true), Layout = new LayoutStyle { Width = 28, Height = 28 } });
                i = Crumbs.Count - tail - 1;
                continue;
            }
            if (i > 0)
            {
                children.Add(Separator());
            }
            var crumb = Crumbs[i];
            var current = i == Crumbs.Count - 1;
            Element label = current || crumb.OnPress is null
                ? new SurfaceText(crumb.Label) { TextType = TextType.LabelLarge, MaxLines = 1 }
                : new Link(crumb.Label, crumb.OnPress) { TextType = TextType.LabelLarge };
            children.Add(crumb.Icon is null ? label : new Box
            {
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4 },
                Children = [new SurfaceIcon(crumb.Icon) { IconSize = 16, Legibility = Legibility.Medium }, label],
            });
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = "Breadcrumb" },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4, FlexWrap = FlexWrap.Wrap },
            Children = children,
        };

        static Element Separator() => new SurfaceIcon("chevron_right") { IconSize = 16, Legibility = Legibility.Medium };
    }
}
