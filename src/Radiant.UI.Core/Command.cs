using System;

namespace Radiant.UI.Core;

/// <summary>
/// Something the user can do, by name: registered with <see cref="CommandHooks.UseCommand"/>, it
/// answers to its shortcut, and a command palette and the menu bar list it.
/// </summary>
/// <param name="Id">What identifies it; one with the same id registered deeper in the tree takes its place.</param>
/// <param name="Title">What it's called ("Toggle dark theme").</param>
public sealed record Command(string Id, string Title)
{
    /// <summary>What it does.</summary>
    public Action? Run { get; init; }

    /// <summary>Its keyboard shortcut.</summary>
    public KeyChord? Shortcut { get; init; }

    /// <summary>The menu bar menu it's on ("File", "View"); null keeps it off the menu bar.</summary>
    public string? Menu { get; init; }

    /// <summary>
    /// The group it's listed under ("Navigation", "Theme"): a palette's heading, and on a menu, a
    /// divider between it and the previous group.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>An icon before the title.</summary>
    public string? Icon { get; init; }

    /// <summary>Other words it's found by ("dark night appearance").</summary>
    public string? Keywords { get; init; }

    /// <summary>Whether it can run now; a disabled command's shortcut does nothing.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Whether it's a setting that's on, shown ticked; null for a plain action.</summary>
    public bool? Checked { get; init; }
}
