using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A row of joined, outlined segments to choose one option (or several) from, the chosen ones
/// filled with a tick. Controlled: shows <see cref="Selected"/> and reports each change.
/// </summary>
/// <param name="Segments">The options.</param>
/// <param name="Selected">The chosen options' indices.</param>
/// <param name="OnChange">Called with the new set of chosen indices.</param>
[RequiresTestId]
public sealed record SegmentedButton(IReadOnlyList<Segment> Segments, IReadOnlySet<int> Selected, Action<IReadOnlySet<int>>? OnChange) : Component
{
    /// <summary>Whether several can be chosen (otherwise choosing one unchooses the rest).</summary>
    public bool MultiSelect { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var segmented = theme.Theme.Components.Navigation.Tabs == TabsLook.Segmented;
        var radius = segmented ? theme.Radius(CornerShapeRole.Small) : System.MathF.Min(theme.Radius(CornerShapeRole.Control), 20f);
        var (selected, multi, onChange) = (Selected, MultiSelect, OnChange);
        // Joined segments share the width equally, but none narrower than the widest label needs
        // (with the tick a chosen one shows, its gap, padding and borders), so a group given no
        // width fits its labels rather than cutting them.
        var widest = 48f;
        if (!segmented)
        {
            var label = theme.Text(TextType.LabelLarge);
            foreach (var segment in Segments)
            {
                var text = Radiant.Text.Paragraph.Layout(segment.Label, label, fonts: context.Root.Fonts).LongestLine;
                widest = System.MathF.Max(widest, System.MathF.Ceiling(text) + 12f + 18f + 8f + 12f + 2f);
            }
        }
        var children = new List<Element?>();
        for (var i = 0; i < Segments.Count; i++)
        {
            var index = i;
            children.Add(new SegmentBox(Segments[i], selected.Contains(i), First: i == 0, Last: i == Segments.Count - 1, radius, segmented, widest, () =>
            {
                var next = new HashSet<int>(multi ? selected : []);
                if (multi && next.Contains(index))
                {
                    next.Remove(index);
                }
                else
                {
                    next.Add(index);
                }
                onChange?.Invoke(next);
            }));
        }
        // Segmented: the segments sit apart in a tray, sized to their labels.
        return segmented
            ? new Box
            {
                Background = theme.SegmentTray(surface),
                CornerRadii = theme.Corners(CornerShapeRole.Medium),
                Semantics = new Semantics { Role = SemanticsRole.Group },
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignSelf = Align.FlexStart, Padding = Edges.All(3), ColumnGap = 2 },
                Children = children,
            }
            : new Box
            {
                Semantics = new Semantics { Role = SemanticsRole.Group },
                Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
                Children = children,
            };
    }

    /// <summary>
    /// One segment: rounded only on the group's outer ends and filled when chosen; or, segmented,
    /// rounded all round and raised when chosen.
    /// </summary>
    private sealed record SegmentBox(Segment Segment, bool Chosen, bool First, bool Last, float Radius, bool Segmented, float MinWidth, Action Press) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var outer = context.UseSurface();
            var state = outer.With(Segmented
                ? new SurfaceChange { Surface = Chosen ? SurfaceName.SurfaceBright : null, Content = Chosen ? null : SurfaceName.SurfaceVariant, ToggleContentOn = !Chosen }
                : new SurfaceChange { Surface = Chosen ? SurfaceName.Secondary : null, ToggleSurfaceContainer = Chosen });
            var hovered = context.UseState(false);
            var press = Press;
            var layer = hovered.Value ? theme.Theme.StateLayers.Hover : 0f;
            var corners = Segmented
                ? Radiant.Graphics2D.CornerRadii.All(Radius)
                : new Radiant.Graphics2D.CornerRadii(First ? Radius : 0f, Last ? Radius : 0f, Last ? Radius : 0f, First ? Radius : 0f);
            return ThemeContexts.Surface.Provide(state, new Box
            {
                Focusable = true,
                Semantics = new Semantics { Role = SemanticsRole.RadioButton, Label = Segment.Label, Checked = Chosen },
                Background = layer > 0f ? theme.StateLayerColor(state, layer)
                    : !Chosen ? null
                    : Segmented ? theme.SegmentPill(outer) : theme.SurfaceColor(state),
                BorderWidth = Segmented && !Chosen ? 0f : 1f,
                BorderColor = Segmented ? theme.OutlineVariant : theme.Outline,
                Shadows = Segmented && Chosen ? theme.Elevation(ElevationLevel.Level1) : [],
                CornerRadii = corners,
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ => hovered.Set(false),
                OnClick = e =>
                {
                    press();
                    e.Handled = true;
                },
                OnKeyDown = e =>
                {
                    if (e.Key is KeyCode.Space or KeyCode.Enter)
                    {
                        press();
                        e.Handled = true;
                    }
                },
                Layout = new LayoutStyle
                {
                    FlexDirection = FlexDirection.Row,
                    AlignItems = Align.Center,
                    JustifyContent = Justify.Center,
                    Height = Segmented ? 30 + theme.DensityOffset / 2f : 40,
                    MinWidth = MinWidth,
                    // Segments share the width equally, whatever their labels; segmented ones start from their labels.
                    FlexGrow = 1,
                    FlexBasis = Segmented ? Dimension.Auto : 0,
                    Padding = Edges.Symmetric(12, 0),
                    ColumnGap = Segmented ? 6 : 8,
                    // Neighbours share their border.
                    Margin = new Edges(First || Segmented ? 0 : -1, 0, 0, 0),
                },
                Children =
                [
                    Chosen && !Segmented ? new SurfaceIcon("check") { IconSize = 18 } : Segment.Icon is null ? null : new SurfaceIcon(Segment.Icon) { IconSize = Segmented ? 16 : 18 },
                    new SurfaceText(Segment.Label) { TextType = TextType.LabelLarge, MaxLines = 1 },
                ],
            });
        }
    }
}
