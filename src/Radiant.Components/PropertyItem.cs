using Radiant.UI.Core;

namespace Radiant.Components;

/// <summary>A property in a <see cref="PropertyGrid"/>: its name and the control that edits it.</summary>
/// <param name="Name">What it's called.</param>
/// <param name="Editor">The control that shows and changes it.</param>
public sealed record PropertyItem(string Name, Element? Editor)
{
    /// <summary>A line about it, under its name.</summary>
    public string? Description { get; init; }
}
