using System;

namespace Radiant.Components;

/// <summary>A step of a <see cref="Breadcrumb"/>.</summary>
/// <param name="Label">What it's called.</param>
/// <param name="OnPress">Going there; null for the current page (the last).</param>
public sealed record Crumb(string Label, Action? OnPress = null)
{
    /// <summary>An icon before the label (a home icon on the first).</summary>
    public string? Icon { get; init; }
}
