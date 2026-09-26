namespace Radiant.Components;

/// <summary>One tab of <see cref="Tabs"/>.</summary>
/// <param name="Label">What it says.</param>
public sealed record Tab(string Label)
{
    /// <summary>An icon above the label.</summary>
    public string? Icon { get; init; }
}
