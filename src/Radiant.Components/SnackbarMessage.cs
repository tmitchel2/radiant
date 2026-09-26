using System;

namespace Radiant.Components;

/// <summary>A brief message at the bottom of the window, with an optional action.</summary>
/// <param name="Text">What it says.</param>
public sealed record SnackbarMessage(string Text)
{
    /// <summary>The action's label, such as "Undo".</summary>
    public string? ActionLabel { get; init; }

    /// <summary>What the action does.</summary>
    public Action? OnAction { get; init; }

    /// <summary>How long it stays (4 s by default; with an action, 8 s).</summary>
    public TimeSpan? Duration { get; init; }
}
