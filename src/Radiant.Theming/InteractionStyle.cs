namespace Radiant.Theming;

/// <summary>How every control shows keyboard focus and being disabled.</summary>
public sealed record InteractionStyle
{
    /// <summary>The width of the ring round a control with keyboard focus, in pixels.</summary>
    public float FocusRingWidth { get; init; } = 3f;

    /// <summary>The gap between a control and its focus ring, in pixels.</summary>
    public float FocusRingGap { get; init; } = 2f;

    /// <summary>The family whose colour the focus ring is drawn in.</summary>
    public SurfaceName FocusRingColor { get; init; } = SurfaceName.Secondary;

    /// <summary>How a disabled control looks.</summary>
    public DisabledLook Disabled { get; init; } = DisabledLook.Recolor;

    /// <summary>With <see cref="DisabledLook.Fade"/>, the opacity a disabled control is drawn at.</summary>
    public float DisabledOpacity { get; init; } = 0.5f;
}
