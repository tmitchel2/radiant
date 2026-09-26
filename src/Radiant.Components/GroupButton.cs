using System;

namespace Radiant.Components;

/// <summary>One button of a <see cref="ButtonGroup"/>.</summary>
/// <param name="Label">What it says (and what assistive technology calls it).</param>
/// <param name="OnPress">What pressing it does.</param>
public sealed record GroupButton(string Label, Action? OnPress)
{
    /// <summary>An icon before the label; with <see cref="IconOnly"/>, instead of it.</summary>
    public string? Icon { get; init; }

    /// <summary>Whether only the icon shows (the label still names it).</summary>
    public bool IconOnly { get; init; }

    /// <summary>Whether it can't be pressed.</summary>
    public bool Disabled { get; init; }
}
