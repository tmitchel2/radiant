namespace Radiant.Components;

/// <summary>One segment of a <see cref="SegmentedButton"/>.</summary>
/// <param name="Label">What it says.</param>
public sealed record Segment(string Label)
{
    /// <summary>An icon, shown when it isn't chosen (a chosen segment shows a tick).</summary>
    public string? Icon { get; init; }
}
