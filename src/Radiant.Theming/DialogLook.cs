namespace Radiant.Theming;

/// <summary>How a dialog lays out its title, text and actions.</summary>
public enum DialogLook
{
    /// <summary>An optional icon above a headline, the text, then the actions at the end.</summary>
    Headline,

    /// <summary>A title row with a close button (when the dialog can be dismissed), the text, then the actions at the end.</summary>
    Card,
}
