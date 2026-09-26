namespace Radiant.Templates;

/// <summary>A number on a <see cref="StatsGrid"/>.</summary>
/// <param name="Label">What it measures.</param>
/// <param name="Value">The figure, formatted.</param>
public sealed record Stat(string Label, string Value)
{
    /// <summary>The change since last period, as a fraction (0.12 is +12%); null for none.</summary>
    public double? Change { get; init; }

    /// <summary>An icon.</summary>
    public string? Icon { get; init; }
}
