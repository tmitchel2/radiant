using System;

namespace Radiant.Components;

/// <summary>Something the user can do, as a <see cref="CommandPalette"/> lists it.</summary>
/// <param name="Id">What identifies it.</param>
/// <param name="Title">What it's called ("Toggle dark theme").</param>
public sealed record Command(string Id, string Title)
{
    /// <summary>An icon before the title.</summary>
    public string? Icon { get; init; }

    /// <summary>Its keyboard shortcut, as shown ("⌘K", "Ctrl+Shift+P").</summary>
    public string? Shortcut { get; init; }

    /// <summary>The group it's listed under while nothing is typed ("Navigation", "Theme").</summary>
    public string? Group { get; init; }

    /// <summary>Other words it's found by ("dark night appearance").</summary>
    public string? Keywords { get; init; }

    /// <summary>What it does.</summary>
    public Action? Run { get; init; }
}
