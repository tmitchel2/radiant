using System;
using System.Collections.Generic;
using System.Globalization;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Moves between the pages of a long list: previous and next, the first and last pages, the
/// current one filled with its neighbours either side, and "…" for the pages between. Controlled:
/// shows <paramref name="Page"/> (from 1) and reports each move.
/// </summary>
/// <param name="PageCount">How many pages.</param>
/// <param name="Page">The current page, from 1.</param>
/// <param name="OnChange">Called with the page to go to.</param>
public sealed partial record Pagination(int PageCount, int Page, Action<int>? OnChange) : Component
{
    [TestId] public static partial string PageButton { get; }
    [TestId<IconButton>] public static partial string Previous { get; }
    [TestId<IconButton>] public static partial string Next { get; }

    /// <summary>How many pages to show either side of the current one.</summary>
    public int Siblings { get; init; } = 1;

    /// <summary>
    /// The pages to show, in order, with 0 standing for a gap: the first and last, the current
    /// page and its siblings, and a gap only where it hides two or more pages (one hidden page is
    /// shown instead).
    /// </summary>
    public static IReadOnlyList<int> Pages(int count, int current, int siblings)
    {
        var shown = new List<int>();
        if (count <= 0)
        {
            return shown;
        }
        current = Math.Clamp(current, 1, count);
        var from = Math.Max(2, current - siblings);
        var to = Math.Min(count - 1, current + siblings);
        shown.Add(1);
        if (from == 3)
        {
            shown.Add(2);
        }
        else if (from > 3)
        {
            shown.Add(0);
        }
        for (var page = from; page <= to; page++)
        {
            shown.Add(page);
        }
        if (to == count - 2)
        {
            shown.Add(count - 1);
        }
        else if (to < count - 2)
        {
            shown.Add(0);
        }
        if (count > 1)
        {
            shown.Add(count);
        }
        return shown;
    }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var page = Math.Clamp(Page, 1, Math.Max(1, PageCount));
        // Page buttons are a little smaller than icon buttons: 36 px by default.
        var size = context.UseTheme().Theme.Components.IconButton.Size - 4f;
        var change = OnChange;
        var children = new List<Element?>
        {
            new IconButton("chevron_left", "Previous page") { TestId = Previous, OnPress = page > 1 ? () => change?.Invoke(page - 1) : null, ShowDisabled = page <= 1 ? true : null },
        };
        foreach (var number in Pages(PageCount, page, Siblings))
        {
            if (number == 0)
            {
                children.Add(new SurfaceText("…") { Legibility = Legibility.Medium, Alignment = Radiant.Text.TextAlignment.Center, Layout = new LayoutStyle { Width = 36 } });
                continue;
            }
            var target = number;
            var current = number == page;
            children.Add(new PressableSurface
            {
                TestId = PageButton,
                SurfaceColor = current ? SurfaceName.Primary : null,
                CornerShape = CornerShapeRole.Control,
                Label = current ? $"Page {number}, current" : $"Page {number}",
                Selected = current,
                OnPress = () => change?.Invoke(target),
                Layout = new LayoutStyle { MinWidth = size, Height = size, Padding = Edges.Symmetric(6, 0), AlignItems = Align.Center, JustifyContent = Justify.Center },
                Children = [new SurfaceText(number.ToString(CultureInfo.CurrentCulture)) { TextType = TextType.LabelLarge }],
            });
        }
        children.Add(new IconButton("chevron_right", "Next page") { TestId = Next, OnPress = page < PageCount ? () => change?.Invoke(page + 1) : null, ShowDisabled = page >= PageCount ? true : null });
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = "Pagination" },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 4 },
            Children = children,
        };
    }
}
