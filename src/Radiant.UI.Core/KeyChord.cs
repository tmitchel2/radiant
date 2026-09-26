using System;
using System.Text;

namespace Radiant.UI.Core;

/// <summary>A key with the modifiers held for it: a keyboard shortcut.</summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifiers that must be held (exactly these).</param>
public readonly record struct KeyChord(KeyCode Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    /// <summary>The platform's command modifier: ⌘ on macOS, Ctrl elsewhere.</summary>
    public static KeyModifiers CommandModifier => OperatingSystem.IsMacOS() ? KeyModifiers.Super : KeyModifiers.Control;

    /// <summary>The key with the platform's command modifier (⌘K on macOS, Ctrl+K elsewhere).</summary>
    public static KeyChord Command(KeyCode key, KeyModifiers also = KeyModifiers.None) => new(key, CommandModifier | also);

    /// <summary>Whether a key press is this chord.</summary>
    public bool Matches(KeyCode key, KeyModifiers modifiers) => key == Key && modifiers == Modifiers;

    /// <summary>The chord as shown in menus: "⌘⇧P" on macOS, "Ctrl+Shift+P" elsewhere.</summary>
    public override string ToString()
    {
        var mac = OperatingSystem.IsMacOS();
        var held = Modifiers;
        var text = new StringBuilder();
        void Add(KeyModifiers modifier, string macSymbol, string name)
        {
            if ((held & modifier) != 0)
            {
                text.Append(mac ? macSymbol : name + "+");
            }
        }
        Add(KeyModifiers.Control, "⌃", "Ctrl");
        Add(KeyModifiers.Alt, "⌥", "Alt");
        Add(KeyModifiers.Shift, "⇧", "Shift");
        Add(KeyModifiers.Super, "⌘", "Win");
        text.Append(KeyName(Key));
        return text.ToString();
    }

    /// <summary>
    /// The chord as a platform menu takes it. The platform's command modifier (⌘ on macOS, Ctrl
    /// elsewhere) is the menu's command key.
    /// </summary>
    public Radiant.Platform.MenuShortcut ToMenuShortcut()
    {
        var held = Radiant.Platform.MenuModifiers.None;
        if ((Modifiers & CommandModifier) != 0)
        {
            held |= Radiant.Platform.MenuModifiers.Command;
        }
        if ((Modifiers & KeyModifiers.Control) != 0 && CommandModifier != KeyModifiers.Control)
        {
            held |= Radiant.Platform.MenuModifiers.Control;
        }
        if ((Modifiers & KeyModifiers.Alt) != 0)
        {
            held |= Radiant.Platform.MenuModifiers.Alt;
        }
        if ((Modifiers & KeyModifiers.Shift) != 0)
        {
            held |= Radiant.Platform.MenuModifiers.Shift;
        }
        var key = Key switch
        {
            KeyCode.Space => "Space",
            >= KeyCode.A and <= KeyCode.Z => ((char)(Key + 32)).ToString(),
            >= KeyCode.Space and <= KeyCode.GraveAccent => ((char)Key).ToString(),
            >= KeyCode.F1 and <= KeyCode.F12 => "F" + (Key - KeyCode.F1 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => Key.ToString(),
        };
        return new Radiant.Platform.MenuShortcut(key, held);
    }

    private static string KeyName(KeyCode key)
    {
        var mac = OperatingSystem.IsMacOS();
        return key switch
        {
            >= KeyCode.A and <= KeyCode.Z => ((char)key).ToString(),
            >= KeyCode.Number0 and <= KeyCode.Number9 => ((char)key).ToString(),
            // Punctuation shows as the character it types.
            KeyCode.Comma or KeyCode.Minus or KeyCode.Period or KeyCode.Slash or KeyCode.Semicolon or KeyCode.Equal
                or KeyCode.Apostrophe or KeyCode.LeftBracket or KeyCode.BackSlash or KeyCode.RightBracket or KeyCode.GraveAccent => ((char)key).ToString(),
            KeyCode.Enter => mac ? "↩" : "Enter",
            KeyCode.Escape => mac ? "⎋" : "Esc",
            KeyCode.Space => "Space",
            KeyCode.Tab => mac ? "⇥" : "Tab",
            KeyCode.Backspace => mac ? "⌫" : "Backspace",
            KeyCode.Delete => mac ? "⌦" : "Del",
            KeyCode.Up => "↑",
            KeyCode.Down => "↓",
            KeyCode.Left => "←",
            KeyCode.Right => "→",
            KeyCode.Home => mac ? "↖" : "Home",
            KeyCode.End => mac ? "↘" : "End",
            KeyCode.PageUp => mac ? "⇞" : "PgUp",
            KeyCode.PageDown => mac ? "⇟" : "PgDn",
            _ => key.ToString(),
        };
    }
}
