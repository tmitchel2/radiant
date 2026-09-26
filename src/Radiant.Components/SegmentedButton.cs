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
        var radius = theme.Radius(CornerShapeRole.Full);
        var (selected, multi, onChange) = (Selected, MultiSelect, OnChange);
        var children = new List<Element?>();
        for (var i = 0; i < Segments.Count; i++)
        {
            var index = i;
            children.Add(new SegmentBox(Segments[i], selected.Contains(i), First: i == 0, Last: i == Segments.Count - 1, radius, () =>
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
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row },
            Children = children,
        };
    }

    /// <summary>One segment: rounded only on the group's outer ends, filled when chosen.</summary>
    private sealed record SegmentBox(Segment Segment, bool Chosen, bool First, bool Last, float Radius, Action Press) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var state = context.UseSurface().With(new SurfaceChange
            {
                Surface = Chosen ? SurfaceName.Secondary : null,
                ToggleSurfaceContainer = Chosen,
            });
            var hovered = context.UseState(false);
            var press = Press;
            var layer = hovered.Value ? theme.Theme.StateLayers.Hover : 0f;
            var corners = new Radiant.Graphics2D.CornerRadii(First ? Radius : 0f, Last ? Radius : 0f, Last ? Radius : 0f, First ? Radius : 0f);
            return ThemeContexts.Surface.Provide(state, new Box
            {
                Focusable = true,
                Semantics = new Semantics { Role = SemanticsRole.RadioButton, Label = Segment.Label, Checked = Chosen },
                Background = layer > 0f ? theme.StateLayerColor(state, layer) : Chosen ? theme.SurfaceColor(state) : null,
                BorderWidth = 1f,
                BorderColor = theme.Outline,
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
                    Height = 40,
                    MinWidth = 48,
                    // Segments share the width equally, whatever their labels.
                    FlexGrow = 1,
                    FlexBasis = 0,
                    Padding = Edges.Symmetric(12, 0),
                    ColumnGap = 8,
                    // Neighbours share their border.
                    Margin = new Edges(First ? 0 : -1, 0, 0, 0),
                },
                Children =
                [
                    Chosen ? new SurfaceIcon("check") { IconSize = 18 } : Segment.Icon is null ? null : new SurfaceIcon(Segment.Icon) { IconSize = 18 },
                    new SurfaceText(Segment.Label) { TextType = TextType.LabelLarge, MaxLines = 1 },
                ],
            });
        }
    }
}
