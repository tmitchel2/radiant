namespace Radiant.UI.Core;

/// <summary>Arrow keys by the way things read.</summary>
public static class DirectionalKeys
{
    /// <summary>
    /// The key as it would be in a left-to-right UI: in a right-to-left one Left and Right swap, so
    /// Right always means "towards the end". Other keys are unchanged.
    /// </summary>
    /// <param name="key">The key pressed.</param>
    /// <param name="rightToLeft">Whether the UI reads right to left.</param>
    public static KeyCode ForDirection(this KeyCode key, bool rightToLeft) => !rightToLeft ? key : key switch
    {
        KeyCode.Left => KeyCode.Right,
        KeyCode.Right => KeyCode.Left,
        _ => key,
    };
}
