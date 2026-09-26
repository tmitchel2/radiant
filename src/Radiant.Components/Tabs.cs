using System;
using System.Collections.Generic;
using System.Linq;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A row of tabs with an indicator under the selected one that slides to each new choice.
/// Controlled: shows <see cref="Selected"/> and reports presses through <see cref="OnSelect"/>.
/// With a tab focused, Left and Right choose the neighbouring tab.
/// </summary>
/// <param name="Items">The tabs.</param>
/// <param name="Selected">The chosen tab's index.</param>
/// <param name="OnSelect">Called with a tab's index when it's chosen.</param>
public sealed record Tabs(IReadOnlyList<Tab> Items, int Selected, Action<int>? OnSelect) : Component
{
    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var row = context.UseRef(new ElementRef()).Value;
        var refs = context.UseMemo(() => Enumerable.Range(0, Items.Count).Select(_ => new ElementRef()).ToArray(), Items.Count);
        var indicator = context.UseState((Left: 0f, Width: 0f));
        var motion = theme.Theme.Motion;
        // The first placement is immediate; after that the indicator slides.
        var placed = context.UseRef(false);
        var duration = motion.Reduced || !placed.Value ? TimeSpan.Zero : motion.MediumDuration;
        var left = context.UseTransition(indicator.Value.Left, duration, motion.Standard);
        var width = context.UseTransition(indicator.Value.Width, duration, motion.Standard);
        if (width > 0f)
        {
            placed.Value = true;
        }
        var selected = Selected;
        var onSelect = OnSelect;
        var count = Items.Count;

        // After layout, put the indicator under the chosen tab (the transitions then slide it there).
        context.UseEffect(() =>
        {
            if (selected >= 0 && selected < refs.Length && refs[selected].IsMounted && row.IsMounted)
            {
                var bounds = refs[selected].Bounds;
                indicator.Set((bounds.X - row.Bounds.X, bounds.Width));
            }
            return null;
        });

        var hasIcons = Items.Any(t => t.Icon is not null);
        var tabs = new List<Element?>();
        for (var i = 0; i < Items.Count; i++)
        {
            var index = i;
            var tab = Items[i];
            var chosen = i == selected;
            tabs.Add(new Box
            {
                Ref = refs[i],
                Layout = new LayoutStyle { FlexGrow = 1, FlexBasis = 0 },
                OnKeyDown = e =>
                {
                    var next = e.Key.ForDirection(rightToLeft) switch { KeyCode.Right => index + 1, KeyCode.Left => index - 1, _ => -1 };
                    if (next >= 0 && next < count)
                    {
                        onSelect?.Invoke(next);
                        refs[next].Focus();
                        e.Handled = true;
                    }
                },
                Children =
                [
                    new PressableSurface
                    {
                        ContentColor = chosen ? SurfaceName.Primary : SurfaceName.SurfaceVariant,
                        ContentOnToggle = chosen ? null : true,
                        Role = SemanticsRole.Tab,
                        Selected = chosen,
                        OnPress = () => onSelect?.Invoke(index),
                        Layout = new LayoutStyle
                        {
                            Height = hasIcons ? 64 : 48,
                            AlignItems = Align.Center,
                            JustifyContent = Justify.Center,
                            RowGap = 2,
                            Padding = Edges.Symmetric(16, 0),
                        },
                        Children =
                        [
                            tab.Icon is null ? null : new SurfaceIcon(tab.Icon) { IconFilled = chosen },
                            new SurfaceText(tab.Label) { TextType = TextType.TitleSmall, MaxLines = 1 },
                        ],
                    },
                ],
            });
        }

        return new Box
        {
            Ref = row,
            Semantics = new Semantics { Role = SemanticsRole.TabList },
            Layout = new LayoutStyle { AlignSelf = Align.Stretch },
            Children =
            [
                new Box { Layout = new LayoutStyle { FlexDirection = FlexDirection.Row }, Children = tabs },
                new Divider(),
                width <= 0f ? null : new Box
                {
                    HitTestVisible = false,
                    Layout = new LayoutStyle
                    {
                        Position = PositionType.Absolute,
                        Height = 3,
                        Width = width,
                        Inset = Edges.Physical(left, Dimension.Undefined, Dimension.Undefined, 0, rightToLeft),
                    },
                    Background = theme.Get(SurfaceName.Primary),
                    CornerRadii = Radiant.Graphics2D.CornerRadii.Top(3),
                },
            ],
        };
    }
}
