using System.Collections.Generic;

namespace Radiant.Templates;

/// <summary>A past order, for <see cref="OrderHistory"/>.</summary>
/// <param name="Number">Its number ("WU88191111").</param>
/// <param name="Date">When it was placed, as shown.</param>
/// <param name="Total">What it came to, as shown.</param>
/// <param name="Status">Where it's got to ("Delivered", "On its way").</param>
/// <param name="Lines">What was in it.</param>
public sealed record Order(string Number, string Date, string Total, string Status, IReadOnlyList<OrderLine> Lines)
{
    /// <summary>Whether it's complete, so its status reads as done rather than in progress.</summary>
    public bool Complete { get; init; }
}
