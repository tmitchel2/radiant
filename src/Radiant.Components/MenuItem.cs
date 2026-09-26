using System;

namespace Radiant.Components;

/// <summary>An entry in a <see cref="Menu"/>.</summary>
/// <param name="Text">What it says.</param>
/// <param name="OnSelect">What choosing it does (the menu closes too).</param>
public sealed record MenuItem(string Text, Action? OnSelect = null)
{
    /// <summary>A leading icon.</summary>
    public string? Icon { get; init; }

    /// <summary>A keyboard shortcut to show on the right, such as "⌘C".</summary>
    public string? Shortcut { get; init; }

    /// <summary>Whether it can't be chosen.</summary>
    public bool Disabled { get; init; }

    /// <summary>Whether a divider comes before it.</summary>
    public bool DividerBefore { get; init; }
}
