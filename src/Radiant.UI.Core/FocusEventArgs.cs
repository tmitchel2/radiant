namespace Radiant.UI.Core;

/// <summary>A box gaining or losing keyboard focus.</summary>
public sealed class FocusEventArgs : UIEventArgs
{
    internal FocusEventArgs(bool isFocusVisible) => IsFocusVisible = isFocusVisible;

    /// <summary>
    /// Whether focus came from the keyboard, so a focus ring should show: clicking a button
    /// focuses it without a ring, tabbing to it shows one (CSS <c>:focus-visible</c>).
    /// </summary>
    public bool IsFocusVisible { get; }
}
