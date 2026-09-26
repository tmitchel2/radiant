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

    /// <summary>
    /// Reads a chord written as modifiers and a key joined by <c>+</c>, ignoring case:
    /// <c>"Cmd+Shift+S"</c>, <c>"Ctrl+Enter"</c>, <c>"Tab"</c>, <c>"F5"</c>. <c>Cmd</c> (or <c>Mod</c>) is
    /// the platform's <see cref="CommandModifier"/>; <c>Super</c>, <c>Meta</c> and <c>Win</c> are
    /// always Super; <c>Option</c> is Alt. A key is a letter, a digit, a character such as <c>/</c>, or a
    /// <see cref="KeyCode"/> name, with <c>Esc</c>, <c>Return</c>, <c>Del</c>, <c>PgUp</c> and
    /// <c>PgDn</c> as well.
    /// </summary>
    public static bool TryParse(string? text, out KeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var parts = text.Trim().Split('+');
        // "Cmd++" is Cmd and the plus key's character, which is Equal on most layouts.
        if (text.EndsWith("++", StringComparison.Ordinal))
        {
            parts = [.. parts[..^2], "="];
        }
        var modifiers = KeyModifiers.None;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].Trim().ToUpperInvariant())
            {
                case "CMD" or "COMMAND" or "MOD" or "⌘" when OperatingSystem.IsMacOS():
                    modifiers |= KeyModifiers.Super;
                    break;
                case "CMD" or "COMMAND" or "MOD" or "⌘":
                    modifiers |= CommandModifier;
                    break;
                case "CTRL" or "CONTROL" or "⌃":
                    modifiers |= KeyModifiers.Control;
                    break;
                case "ALT" or "OPT" or "OPTION" or "⌥":
                    modifiers |= KeyModifiers.Alt;
                    break;
                case "SHIFT" or "⇧":
                    modifiers |= KeyModifiers.Shift;
                    break;
                case "SUPER" or "META" or "WIN":
                    modifiers |= KeyModifiers.Super;
                    break;
                default:
                    return false;
            }
        }
        if (!TryParseKey(parts[^1].Trim(), out var key))
        {
            return false;
        }
        chord = new KeyChord(key, modifiers);
        return true;
    }

    private static bool TryParseKey(string name, out KeyCode key)
    {
        key = KeyCode.Unknown;
        if (name.Length == 1)
        {
            var c = char.ToUpperInvariant(name[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9' || Enum.IsDefined((KeyCode)c) && c is > ' ' and < 'a')
            {
                key = (KeyCode)c;
                return true;
            }
        }
        switch (name.ToUpperInvariant())
        {
            case "ESC":
                key = KeyCode.Escape;
                return true;
            case "RETURN":
                key = KeyCode.Enter;
                return true;
            case "DEL":
                key = KeyCode.Delete;
                return true;
            case "PGUP":
                key = KeyCode.PageUp;
                return true;
            case "PGDN":
                key = KeyCode.PageDown;
                return true;
            case "BACKSPACE" or "BKSP":
                key = KeyCode.Backspace;
                return true;
        }
        return Enum.TryParse(name, ignoreCase: true, out key) && Enum.IsDefined(key) && key != KeyCode.Unknown;
    }

    /// <summary>The chord written as <see cref="TryParse"/> reads it, the same on every platform: <c>"Ctrl+Shift+S"</c>.</summary>
    public string ToInvariantString()
    {
        var text = new StringBuilder();
        if ((Modifiers & KeyModifiers.Control) != 0)
        {
            text.Append("Ctrl+");
        }
        if ((Modifiers & KeyModifiers.Alt) != 0)
        {
            text.Append("Alt+");
        }
        if ((Modifiers & KeyModifiers.Shift) != 0)
        {
            text.Append("Shift+");
        }
        if ((Modifiers & KeyModifiers.Super) != 0)
        {
            text.Append("Super+");
        }
        text.Append(Key is >= KeyCode.A and <= KeyCode.Z or >= KeyCode.Number0 and <= KeyCode.Number9 ? ((char)Key).ToString() : Key.ToString());
        return text.ToString();
    }

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
