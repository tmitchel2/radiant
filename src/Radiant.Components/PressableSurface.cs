using System;
using System.Collections.Generic;
using Radiant.Layout;
using Radiant.Theming;
using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>
/// A <see cref="Surface"/> that can be pressed: it takes focus, answers the pointer and Enter or
/// Space, and shows hover, focus and pressed states as a state layer (its content colour laid
/// over it at the theme's state-layer opacity), with a ring just outside it while it has keyboard
/// focus. Disabled (<see cref="IHasBackgroundColor.ShowDisabled"/>),
/// it fades and stops responding.
/// </summary>
public sealed partial record PressableSurface : Component, IHasBackgroundColor, IHasCornerShape, IHasElevation, IHasOutline, IHasLayout, IHasPressable
{
    /// <summary>What the surface contains.</summary>
    public IReadOnlyList<Element?> Children { get; init; } = [];

    /// <summary>
    /// Whether keyboard focus's ring is drawn just inside the edge rather than outside it: for
    /// items that fill a list, menu or table row, where a ring outside would be clipped or cover
    /// their neighbours.
    /// </summary>
    public bool InsetFocusRing { get; init; }

    /// <summary>Whether it shrinks a little while pressed, as far as the theme says (<see cref="InteractionStyle.PressScale"/>): for buttons, not rows.</summary>
    public bool ScaleOnPress { get; init; }

    /// <summary>What the surface is, for assistive technology (a button unless told otherwise).</summary>
    public SemanticsRole Role { get; init; } = SemanticsRole.Button;

    /// <summary>A name for assistive technology when the content's text isn't enough (an icon button).</summary>
    public string? Label { get; init; }

    /// <summary>Whether assistive technology should hear it's the chosen one (a tab, a destination).</summary>
    public bool Selected { get; init; }

    /// <summary>Whether it's checked, for a surface that's a check box or radio (a filter chip, a toggle button); null if it isn't one.</summary>
    public bool? Checked { get; init; }

    /// <summary>Whether what it shows or hides is showing (a disclosure, a menu button); null if it doesn't.</summary>
    public bool? Expanded { get; init; }

    /// <summary>Tab order: 0 in tree order, negative to skip it when tabbing (a roving group's other items).</summary>
    public int TabIndex { get; init; }

    /// <inheritdoc/>
    public override Element? Build(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var theme = context.UseTheme();
        var interaction = theme.Theme.Components.Interaction;
        // A theme that fades disabled controls keeps their colours and fades the whole control.
        var fade = ShowDisabled == true && interaction.Disabled == DisabledLook.Fade;
        var change = this.ToSurfaceChange();
        var state = context.UseSurface().With(fade ? change with { ShowDisabled = false } : change);
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
        var scale = context.UseTransition(ScaleOnPress && pressed.Value && !disabled ? interaction.PressScale : 1f,
            motion.Reduced ? TimeSpan.Zero : TimeSpan.FromMilliseconds(100), motion.Standard);
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

        // Keyboard focus also shows a ring just outside the control, as the theme draws it (by
        // default Material's focus indicator: 3 wide, 2 out, in the secondary colour); or just
        // inside, for items in a list.
        var (ringWidth, ringGap) = (interaction.FocusRingWidth, interaction.FocusRingGap);
        var ringOut = InsetFocusRing ? 0f : ringWidth + ringGap;
        var ring = !focusRing.Value || disabled ? null : new Box
        {
            Layout = new LayoutStyle { Position = PositionType.Absolute, Inset = Edges.All(-ringOut) },
            BorderWidth = ringWidth,
            BorderColor = theme.Get(interaction.FocusRingColor),
            CornerRadii = Radiant.Graphics2D.CornerRadii.All(radius + ringOut),
            HitTestVisible = false,
        };

        return ThemeContexts.Surface.Provide(state, SurfaceBox.For(this, theme, state) with
        {
            Opacity = fade ? interaction.DisabledOpacity : 1f,
            Transform = scale < 1f ? System.Numerics.Matrix3x2.CreateScale(scale) : null,
            Focusable = !disabled,
            TabIndex = TabIndex,
            Semantics = new Semantics { Role = Role, Label = Label, Disabled = disabled, Selected = Selected, Checked = Checked, Expanded = Expanded },
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
            Children = [stateLayer, .. Children, ring],
        });
    }
}
