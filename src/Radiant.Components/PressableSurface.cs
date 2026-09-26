using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A <see cref="Surface"/> that can be pressed: it takes focus, answers the pointer and Enter or
/// Space, and shows hover, focus and pressed states as a state layer (its content colour laid
/// over it at the theme's state-layer opacity). Disabled (<see cref="IHasBackgroundColor.ShowDisabled"/>),
/// it fades and stops responding.
/// </summary>
public sealed partial record PressableSurface : Component, IHasBackgroundColor, IHasCornerShape, IHasElevation, IHasOutline, IHasLayout, IHasPressable
{
    /// <summary>What the surface contains.</summary>
    public IReadOnlyList<Element?> Children { get; init; } = [];

    /// <summary>What the surface is, for assistive technology (a button unless told otherwise).</summary>
    public SemanticsRole Role { get; init; } = SemanticsRole.Button;

    /// <summary>A name for assistive technology when the content's text isn't enough (an icon button).</summary>
    public string? Label { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var state = context.UseSurface().With(this.ToSurfaceChange());
        var hovered = context.UseState(false);
        var pressed = context.UseState(false);
        var focusRing = context.UseState(false);
        var disabled = ShowDisabled == true;
        var onPress = OnPress;

        var layers = theme.Theme.StateLayers;
        var target = disabled ? 0f
            : pressed.Value ? layers.Pressed
            : focusRing.Value ? layers.Focus
            : hovered.Value ? layers.Hover
            : 0f;
        var motion = theme.Theme.Motion;
        var opacity = context.UseTransition(target, motion.Reduced ? TimeSpan.Zero : motion.ShortDuration, motion.Standard);
        // The layer is opaque (mixed in sRGB with the surface it covers), so it sits inside any
        // outline rather than over it.
        var border = ShowOutline == true ? OutlineWidth ?? 1f : 0f;
        var radius = theme.Radius(CornerShape ?? CornerShapeRole.None);
        var stateLayer = opacity <= 0f ? null : new Box
        {
            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(border) },
            Background = theme.StateLayerColor(state, opacity),
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(System.MathF.Max(0f, radius - border)),
            HitTestVisible = false,
        };

        return ThemeContexts.Surface.Provide(state, SurfaceBox.For(this, theme, state) with
        {
            Focusable = !disabled,
            Semantics = new Semantics { Role = Role, Label = Label, Disabled = disabled },
            OnPointerEnter = _ => hovered.Set(true),
            OnPointerLeave = _ =>
            {
                hovered.Set(false);
                pressed.Set(false);
            },
            OnPointerDown = e =>
            {
                if (!disabled && e.Button == PointerButton.Left)
                {
                    pressed.Set(true);
                }
            },
            OnPointerUp = _ => pressed.Set(false),
            OnClick = e =>
            {
                if (!disabled && e.Button == PointerButton.Left)
                {
                    onPress?.Invoke();
                    e.Handled = true;
                }
            },
            OnKeyDown = e =>
            {
                if (!disabled && e.Key is KeyCode.Enter or KeyCode.Space)
                {
                    onPress?.Invoke();
                    e.Handled = true;
                }
            },
            OnFocus = e => focusRing.Set(e.IsFocusVisible),
            OnBlur = _ => focusRing.Set(false),
            Children = [stateLayer, .. Children],
        });
    }
}
