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
public sealed partial record Tabs(IReadOnlyList<Tab> Items, int Selected, Action<int>? OnSelect) : Component
{
    [TestId] public static partial string TabButton { get; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rightToLeft = context.UseRightToLeft();
        var theme = context.UseTheme();
        var surface = context.UseSurface();
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

        var style = theme.Theme.Components.Navigation;
        var fillChosen = theme.Theme.Components.Icons.FillChosen;
        if (style.Tabs == TabsLook.Segmented)
        {
            return Segmented(theme, surface, style, row, refs, width, left, rightToLeft);
        }
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
                        TestId = TabButton,
                        InsetFocusRing = true,
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
                            tab.Icon is null ? null : new SurfaceIcon(tab.Icon) { IconFilled = chosen && fillChosen },
                            new SurfaceText(tab.Label) { TextType = style.TabText, MaxLines = 1 },
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

    // Tabs sized to their labels in a tray, icons beside labels; the current one a raised pill
    // that slides between them.
    private Box Segmented(ResolvedTheme theme, SurfaceState surface, NavigationStyle style, ElementRef row, ElementRef[] refs, float width, float left, bool rightToLeft)
    {
        var selected = Selected;
        var onSelect = OnSelect;
        var count = Items.Count;
        const float inset = 3f;
        var tabs = new List<Element?>
        {
            width <= 0f ? null : new Box
            {
                HitTestVisible = false,
                Layout = new LayoutStyle
                {
                    Position = PositionType.Absolute,
                    Width = width,
                    Inset = Edges.Physical(left, 0, Dimension.Undefined, 0, rightToLeft),
                },
                Background = theme.SegmentPill(surface),
                BorderWidth = 1f,
                BorderColor = theme.OutlineVariant,
                CornerRadii = theme.Corners(CornerShapeRole.Small),
                Shadows = theme.Elevation(ElevationLevel.Level1),
            },
        };
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var tab = Items[i];
            var chosen = i == selected;
            tabs.Add(new Box
            {
                Ref = refs[i],
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
                        TestId = TabButton,
                        InsetFocusRing = true,
                        // The chosen tab is on the pill (drawn sliding beneath it), so its content and
                        // state layer are worked out on the pill's colour without drawing it.
                        SurfaceColor = chosen ? SurfaceName.SurfaceBright : null,
                        ShowSurface = false,
                        ContentColor = chosen ? null : SurfaceName.SurfaceVariant,
                        ContentOnToggle = chosen ? null : true,
                        CornerShape = CornerShapeRole.Small,
                        Role = SemanticsRole.Tab,
                        Selected = chosen,
                        OnPress = () => onSelect?.Invoke(index),
                        Layout = new LayoutStyle
                        {
                            FlexDirection = FlexDirection.Row,
                            Height = 30 + theme.DensityOffset / 2f,
                            AlignItems = Align.Center,
                            ColumnGap = 6,
                            Padding = Edges.Symmetric(12, 0),
                        },
                        Children =
                        [
                            tab.Icon is null ? null : new SurfaceIcon(tab.Icon) { IconSize = 16 },
                            new SurfaceText(tab.Label) { TextType = style.TabText, MaxLines = 1 },
                        ],
                    },
                ],
            });
        }
        return new Box
        {
            Background = theme.SegmentTray(surface),
            CornerRadii = theme.Corners(CornerShapeRole.Medium),
            Semantics = new Semantics { Role = SemanticsRole.TabList },
            Layout = new LayoutStyle { AlignSelf = Align.FlexStart, Padding = Edges.All(inset) },
            Children = [new Box { Ref = row, Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, ColumnGap = 2 }, Children = tabs }],
        };
    }
}
