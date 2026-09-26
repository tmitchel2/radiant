namespace Radiant.Theming;

/// <summary>How a text field and its label are laid out.</summary>
public enum FieldLook
{
    /// <summary>The label sits in the field and floats up (into the outline) on focus or text; filled and outlined variants.</summary>
    FloatingLabel,

    /// <summary>The label sits above a plain bordered input; the border turns the accent colour, ringed, on focus. Both variants look the same.</summary>
    LabelAbove,
}
