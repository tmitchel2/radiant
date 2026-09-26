namespace Radiant.Theming;

/// <summary>How text fields and the controls built on them (selects, date and number fields) are drawn.</summary>
public sealed record FieldStyle
{
    /// <summary>How the field and its label are laid out.</summary>
    public FieldLook Look { get; init; } = FieldLook.FloatingLabel;

    /// <summary>The input's height at standard density, in pixels.</summary>
    public float Height { get; init; } = 56f;

    /// <summary>The corners.</summary>
    public CornerShapeRole Shape { get; init; } = CornerShapeRole.ExtraSmall;

    /// <summary>The input text.</summary>
    public TextType Text { get; init; } = TextType.BodyLarge;

    /// <summary>With <see cref="FieldLook.LabelAbove"/>, the label's text.</summary>
    public TextType Label { get; init; } = TextType.LabelLarge;
}
