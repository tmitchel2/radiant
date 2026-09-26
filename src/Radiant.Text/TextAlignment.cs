namespace Radiant.Text;

/// <summary>Where lines sit horizontally within the paragraph's width.</summary>
public enum TextAlignment
{
    /// <summary>At the start of the reading direction: left for left-to-right paragraphs, right for right-to-left.</summary>
    Start,

    /// <summary>At the end of the reading direction.</summary>
    End,

    /// <summary>Always left.</summary>
    Left,

    /// <summary>Always right.</summary>
    Right,

    /// <summary>Centered.</summary>
    Center,
}
