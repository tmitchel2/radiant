using System;

namespace Radiant.UI.Core;

/// <summary>
/// The standard Edit commands: their ids, and an Edit menu an app can put on its menu bar. A
/// focused <see cref="TextInput"/> registers the same ids, focus-scoped, so while it has focus they
/// act on it; with nothing that can take them focused, the menu's items are greyed out.
/// </summary>
public static class EditCommands
{
    /// <summary>The menu they're on.</summary>
    public const string Menu = "Edit";

    /// <summary>Undo the last change.</summary>
    public const string Undo = "edit.undo";

    /// <summary>Redo what was undone.</summary>
    public const string Redo = "edit.redo";

    /// <summary>Cut the selection to the clipboard.</summary>
    public const string Cut = "edit.cut";

    /// <summary>Copy the selection to the clipboard.</summary>
    public const string Copy = "edit.copy";

    /// <summary>Paste from the clipboard.</summary>
    public const string Paste = "edit.paste";

    /// <summary>Select everything.</summary>
    public const string SelectAll = "edit.selectAll";

    /// <summary>
    /// The Edit command with <paramref name="id"/>: its title, shortcut and place on the menu, and
    /// <paramref name="run"/> as what it does.
    /// </summary>
    public static Command Create(string id, Action? run, bool enabled = true)
    {
        var (title, shortcut, group) = id switch
        {
            Undo => ("Undo", KeyChord.Command(KeyCode.Z), "History"),
            // Redo is ⇧⌘Z on macOS and Ctrl+Y elsewhere.
            Redo => ("Redo", OperatingSystem.IsMacOS() ? KeyChord.Command(KeyCode.Z, KeyModifiers.Shift) : KeyChord.Command(KeyCode.Y), "History"),
            Cut => ("Cut", KeyChord.Command(KeyCode.X), "Clipboard"),
            Copy => ("Copy", KeyChord.Command(KeyCode.C), "Clipboard"),
            Paste => ("Paste", KeyChord.Command(KeyCode.V), "Clipboard"),
            SelectAll => ("Select All", KeyChord.Command(KeyCode.A), "Selection"),
            _ => throw new ArgumentException($"'{id}' isn't an Edit command.", nameof(id)),
        };
        return new Command(id, title) { Menu = Menu, Group = group, Shortcut = shortcut, Enabled = enabled, Run = run };
    }

    /// <summary>
    /// Puts the Edit menu on the menu bar while this component is mounted: Undo, Redo, Cut, Copy,
    /// Paste and Select All, greyed out until something that can take them (a focused text input)
    /// has focus. Put it near the top of the app, beside its other commands.
    /// </summary>
    public static void UseStandardEditMenu(this BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var id in (ReadOnlySpan<string>)[Undo, Redo, Cut, Copy, Paste, SelectAll])
        {
            context.UseCommand(Create(id, null, enabled: false));
        }
    }
}
