using System;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// What check boxes, switches and radio buttons share: a row holding the indicator (with the
/// hover/focus/press state layer as a circle round it) and an optional label; pressing anywhere on
/// it, or Space or Enter while it's focused, toggles it.
/// </summary>
internal sealed record SelectionControl(Func<SelectionVisualState, Element> Indicator, float IndicatorWidth) : Component
{
    public string? Label { get; init; }

    public string? AccessibleLabel { get; init; }

    public SemanticsRole Role { get; init; }

    public bool? Checked { get; init; }

    public bool Disabled { get; init; }

    public Action? OnToggle { get; init; }

    public int TabIndex { get; init; }

    public override Element? Build(BuildContext context)
    {
        var theme = context.UseTheme();
        var surface = context.UseSurface();
        var hovered = context.UseState(false);
        var pressed = context.UseState(false);
        var focusRing = context.UseState(false);
        var disabled = Disabled;
        var toggle = OnToggle;

        var layers = theme.Theme.StateLayers;
        var target = disabled ? 0f : pressed.Value ? layers.Pressed : focusRing.Value ? layers.Focus : hovered.Value ? layers.Hover : 0f;
        var motion = theme.Theme.Motion;
        var opacity = context.UseTransition(target, motion.Reduced ? TimeSpan.Zero : motion.ShortDuration, motion.Standard);
        var visual = new SelectionVisualState(hovered.Value, pressed.Value, focusRing.Value, disabled);
        // The state layer's ring: 40 px, smaller at a compact density (never under a switch's 32).
        var ringSize = MathF.Max(32f, 40f + theme.DensityOffset);

        return new Box
        {
            Focusable = !disabled,
            TabIndex = TabIndex,
            Semantics = new Semantics { Role = Role, Label = AccessibleLabel ?? Label, Checked = Checked, Disabled = disabled },
            // A check box or radio sits in its 40-wide state-layer ring, which spaces its label; a
            // switch is wider than the ring, so its label needs the space itself.
            Layout = new LayoutStyle { FlexDirection = FlexDirection.Row, AlignItems = Align.Center, ColumnGap = IndicatorWidth >= ringSize ? 12 : 4 },
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
                    toggle?.Invoke();
                    e.Handled = true;
                }
            },
            OnKeyDown = e =>
            {
                if (!disabled && e.Key is KeyCode.Space or KeyCode.Enter)
                {
                    toggle?.Invoke();
                    e.Handled = true;
                }
            },
            OnFocus = e => focusRing.Set(e.IsFocusVisible),
            OnBlur = _ => focusRing.Set(false),
            Children =
            [
                new Box
                {
                    Layout = new LayoutStyle
                    {
                        Width = MathF.Max(ringSize, IndicatorWidth),
                        Height = ringSize,
                        AlignItems = Align.Center,
                        JustifyContent = Justify.Center,
                    },
                    Children =
                    [
                        opacity <= 0f ? null : new Box
                        {
                            Layout = new LayoutStyle
                            {
                                Position = PositionType.Absolute,
                                Width = ringSize,
                                Height = ringSize,
                                Inset = new Edges((MathF.Max(ringSize, IndicatorWidth) - ringSize) / 2f, 0, Dimension.Undefined, Dimension.Undefined),
                            },
                            Background = theme.StateLayerColor(surface, opacity),
                            CornerRadii = Radiant.Graphics2D.CornerRadii.All(ringSize / 2f),
                            HitTestVisible = false,
                        },
                        Indicator(visual),
                    ],
                },
                Label is null ? null : new SurfaceText(Label) { TextType = TextType.BodyLarge, Legibility = disabled ? Legibility.Low : null },
            ],
        };
    }
}
