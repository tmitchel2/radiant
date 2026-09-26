
namespace Radiant.UI.Core;

/// <summary>A key pressed or released, sent to the focused box and its ancestors.</summary>
public sealed class KeyEventArgs : UIEventArgs
{
    internal KeyEventArgs(KeyCode key, KeyModifiers modifiers, bool isRepeat)
    {
        Key = key;
        Modifiers = modifiers;
        IsRepeat = isRepeat;
    }

    /// <summary>The key.</summary>
    public KeyCode Key { get; }

    /// <summary>The modifier keys held.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>Whether this is the key repeating while held down.</summary>
    public bool IsRepeat { get; }
}
