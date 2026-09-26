namespace Radiant.Platform;

/// <summary>
/// A menu item's keyboard shortcut, as the platform's menu shows it and answers to it.
/// </summary>
/// <param name="Key">
/// The key: a single character for a printable key ("k", ",", "1"), otherwise its name ("Enter",
/// "Escape", "Tab", "Space", "Backspace", "Delete", "Up", "Down", "Left", "Right", "Home", "End",
/// "PageUp", "PageDown", "F1" … "F12").
/// </param>
/// <param name="Modifiers">The keys held with it.</param>
public sealed record MenuShortcut(string Key, MenuModifiers Modifiers);
