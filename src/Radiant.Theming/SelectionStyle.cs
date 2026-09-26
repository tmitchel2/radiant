namespace Radiant.Theming;

/// <summary>How check boxes, radio buttons, switches, sliders and progress bars are drawn.</summary>
public sealed record SelectionStyle
{
    /// <summary>Whether hover, focus and press show as a circle round the indicator (otherwise the indicator itself changes).</summary>
    public bool Halo { get; init; } = true;

    /// <summary>A check box's size, in pixels.</summary>
    public float CheckboxSize { get; init; } = 18f;

    /// <summary>A check box's corner radius, in pixels.</summary>
    public float CheckboxRadius { get; init; } = 2f;

    /// <summary>The width of an unchecked check box's or radio button's border, in pixels.</summary>
    public float BorderWidth { get; init; } = 2f;

    /// <summary>A radio button's size, in pixels.</summary>
    public float RadioSize { get; init; } = 20f;

    /// <summary>How switches are built.</summary>
    public SwitchLook Switch { get; init; } = SwitchLook.Expressive;

    /// <summary>How a slider's thumb is drawn.</summary>
    public SliderThumb SliderThumb { get; init; } = SliderThumb.Filled;

    /// <summary>The thickness of slider and progress tracks, in pixels.</summary>
    public float TrackThickness { get; init; } = 4f;

    /// <summary>The label beside a check box, radio button or switch.</summary>
    public TextType Label { get; init; } = TextType.BodyLarge;
}
