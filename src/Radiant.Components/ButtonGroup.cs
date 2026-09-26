using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// Related actions joined into one outlined control (Bold, Italic, Underline; zoom out, fit, zoom
/// in): each is a button of its own, sharing borders with its neighbours and rounded only at the
/// group's ends. For choosing among options, use a <see cref="SegmentedButton"/>.
/// </summary>
/// <param name="Buttons">The buttons, in order.</param>
public sealed record ButtonGroup(IReadOnlyList<GroupButton> Buttons) : Component
{
    /// <summary>What assistive technology calls the group.</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var radius = theme.Radius(CornerShapeRole.Control);
        var children = new List<Element?>();
        for (var i = 0; i < Buttons.Count; i++)
        {
            children.Add(new Part(Buttons[i], i == 0, i == Buttons.Count - 1, radius) { Key = i });
        }
        return new Box
        {
            Semantics = new Semantics { Role = SemanticsRole.Group, Label = Label },
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignSelf = Align.FlexStart },
            Children = children,
        };
    }

    /// <summary>A button of the group: rounded only on the group's outer ends.</summary>
    private sealed record Part(GroupButton Button, bool First, bool Last, float Radius) : Component
    {
        public override Element? Build(BuildContext context)
        {
            var theme = context.UseTheme();
            var state = context.UseSurface();
            var hovered = context.UseState(false);
            var pressed = context.UseState(false);
            var ring = context.UseState(false);
            var button = Button;
            var disabled = button.Disabled;
            var layer = disabled ? 0f : pressed.Value ? theme.Theme.StateLayers.Pressed : hovered.Value ? theme.Theme.StateLayers.Hover : 0f;
            var content = disabled ? state with { Content = state.Content with { Opacity = Legibility.Low } } : state;
            var radius = System.MathF.Min(Radius, 20f);
            return ThemeContexts.Surface.Provide(content, new Box
            {
                Focusable = !disabled,
                Semantics = new Semantics { Role = SemanticsRole.Button, Label = button.Label, Disabled = disabled },
                Background = layer > 0f ? theme.StateLayerColor(state, layer) : null,
                BorderWidth = ring.Value ? theme.Theme.Components.Interaction.FocusRingWidth + 1f : 1f,
                BorderColor = ring.Value ? theme.Get(theme.Theme.Components.Interaction.FocusRingColor) : theme.Outline,
                CornerRadii = new Radiant.Graphics2D.CornerRadii(First ? radius : 0f, Last ? radius : 0f, Last ? radius : 0f, First ? radius : 0f),
                OnPointerEnter = _ => hovered.Set(true),
                OnPointerLeave = _ =>
                {
                    hovered.Set(false);
                    pressed.Set(false);
                },
                OnPointerDown = e => pressed.Set(!disabled && e.Button == PointerButton.Left),
                OnPointerUp = _ => pressed.Set(false),
                OnClick = e =>
                {
                    if (!disabled && e.Button == PointerButton.Left)
                    {
                        button.OnPress?.Invoke();
                        e.Handled = true;
                    }
                },
                OnKeyDown = e =>
                {
                    if (!disabled && e.Key is KeyCode.Space or KeyCode.Enter)
                    {
                        button.OnPress?.Invoke();
                        e.Handled = true;
                    }
                },
                OnFocus = e => ring.Set(e.IsFocusVisible),
                OnBlur = _ => ring.Set(false),
                Layout = new LayoutStyle
                {
                    FlexDirection = FlexDirection.Row,
                    AlignItems = Align.Center,
                    JustifyContent = Justify.Center,
                    Height = theme.Theme.Components.Button.Height + theme.DensityOffset,
                    MinWidth = 48,
                    Padding = Edges.Symmetric(button.IconOnly ? 12 : 16, 0),
                    ColumnGap = 8,
                    // Neighbours share their border.
                    Margin = new Edges(First ? 0 : -1, 0, 0, 0),
                },
                Children =
                [
                    button.Icon is null ? null : new SurfaceIcon(button.Icon) { IconSize = 18 },
                    button.IconOnly && button.Icon is not null ? null : new SurfaceText(button.Label) { TextType = TextType.LabelLarge, MaxLines = 1 },
                ],
            });
        }
    }
}
