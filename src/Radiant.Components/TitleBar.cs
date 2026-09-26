using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// The app's own title bar, drawn where the system's was: while it's shown, the window's content
/// reaches its top edge (where the platform allows it), the system's window controls stay over the
/// bar's start, and the bar keeps clear of them. Pressing its empty space or title moves the
/// window, and a double click there zooms it, as the system's bar would; its buttons work as
/// buttons. Without a platform that allows it, it's a plain bar at the top of the window.
/// </summary>
/// <param name="Title">The window's title.</param>
public sealed record TitleBar(string Title) : Component
{
    /// <summary>Controls after the system's window controls (a sidebar toggle, back and forward).</summary>
    public IReadOnlyList<Element?> Leading { get; init; } = [];

    /// <summary>Controls at the end (search, share, a menu).</summary>
    public IReadOnlyList<Element?> Trailing { get; init; } = [];

    /// <summary>Whether the title is centred in the bar, as macOS document windows do, rather than after the leading controls.</summary>
    public bool CenterTitle { get; init; } = true;

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var chrome = context.UsePlatform().Chrome;
        var theme = context.UseTheme();
        var extended = context.UseState(chrome.ExtendsIntoTitleBar);

        // Shown, the bar takes over the system's; gone, it gives it back.
        context.UseEffect(() =>
        {
            if (!chrome.IsSupported)
            {
                return null;
            }
            chrome.ExtendIntoTitleBar(true);
            extended.Set(chrome.ExtendsIntoTitleBar);
            return () => chrome.ExtendIntoTitleBar(false);
        }, chrome);

        var drawn = extended.Value;
        var height = MathF.Max(38f, drawn ? chrome.TitleBarHeight : 0f);
        var leading = drawn ? chrome.LeadingInset : 8f;
        var trailing = drawn ? chrome.TrailingInset : 0f;
        var title = new SurfaceText(Title) { TextType = TextType.TitleSmall, MaxLines = 1, HeadingLevel = 1 };

        var bar = new Surface
        {
            SurfaceColor = SurfaceName.SurfaceContainer,
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = "Title bar" },
            Layout = new LayoutStyle
            {
                FlexDirection = FlexDirection.Row,
                AlignItems = Align.Center,
                Height = height,
                Padding = new Edges(leading, 0, trailing + 8, 0),
                ColumnGap = 8,
            },
            Children =
            [
                Slot(Leading),
                CenterTitle
                    ? new Box
                    {
                        HitTestVisible = false,
                        Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(0), AlignItems = Align.Center, JustifyContent = Justify.Center },
                        Children = [title],
                    }
                    : title,
                new Box { HitTestVisible = false, Layout = new LayoutStyle { FlexGrow = 1 } },
                Slot(Trailing),
                // The line under the bar.
                new Box
                {
                    HitTestVisible = false,
                    Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = new Edges(0, Dimension.Undefined, 0, 0), Height = 1 },
                    Background = theme.OutlineVariant,
                },
            ],
        };

        // A press anywhere in the bar that no control took (its empty space, its title) moves the
        // window, and a double click zooms it.
        return new Box
        {
            Layout = new LayoutStyle { FlexShrink = 0 },
            OnPointerDown = e =>
            {
                if (e.Button != PointerButton.Left)
                {
                    return;
                }
                if (e.ClickCount == 2)
                {
                    chrome.TitleBarDoubleClick();
                }
                else
                {
                    chrome.BeginDrag();
                }
                e.Handled = true;
            },
            Children = [bar],
        };

        // Controls take their own presses, so a press on one never starts a window drag.
        static Box Slot(IReadOnlyList<Element?> items) => new()
        {
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = 2 },
            OnPointerDown = e => e.Handled = true,
            Children = items,
        };
    }
}
