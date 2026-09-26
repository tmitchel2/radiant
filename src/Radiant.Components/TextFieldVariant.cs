namespace Radiant.Components;

/// <summary>How a <see cref="TextField"/> is drawn.</summary>
public enum TextFieldVariant
{
    /// <summary>A filled container with a line along the bottom: the more prominent form.</summary>
    Filled,

    /// <summary>An outlined container: for forms with many fields.</summary>
    Outlined,

    /// <summary>
    /// Just the text: no container, border or visible label (the label still names it to assistive
    /// technology), for a field inside something that frames it, like a message composer.
    /// </summary>
    Plain,
}
