using System.Collections.Generic;

namespace Radiant.Components;

/// <summary>A titled group of properties in a <see cref="PropertyGrid"/>.</summary>
/// <param name="Title">The group's title ("Layout", "Appearance").</param>
/// <param name="Properties">Its properties, in order.</param>
public sealed record PropertySection(string Title, IReadOnlyList<PropertyItem> Properties)
{
    /// <summary>Whether it starts open (the default).</summary>
    public bool InitiallyOpen { get; init; } = true;
}
