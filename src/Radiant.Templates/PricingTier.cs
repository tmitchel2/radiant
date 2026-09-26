using System.Collections.Generic;

namespace Radiant.Templates;

/// <summary>One plan of <see cref="PricingTiers"/>.</summary>
/// <param name="Name">The plan's name.</param>
/// <param name="Price">Its price, formatted (e.g. "$12").</param>
/// <param name="Features">What it includes.</param>
public sealed record PricingTier(string Name, string Price, IReadOnlyList<string> Features)
{
    /// <summary>The billing period, e.g. "/month".</summary>
    public string Period { get; init; } = "/month";

    /// <summary>A line about who it's for.</summary>
    public string? Description { get; init; }

    /// <summary>Whether it's the plan to steer people to (it's filled and marked).</summary>
    public bool Featured { get; init; }
}
