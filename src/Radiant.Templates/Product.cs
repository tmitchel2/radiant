using Radiant.UI.Core;

namespace Radiant.Templates;

/// <summary>An item for sale, for the ecommerce blocks.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Price">Its price, formatted.</param>
/// <param name="Picture">Its picture.</param>
public sealed record Product(string Name, string Price, ImageSource Picture)
{
    /// <summary>A detail line, such as a colour.</summary>
    public string? Detail { get; init; }

    /// <summary>Its rating out of 5.</summary>
    public double? Rating { get; init; }

    /// <summary>A badge such as "New" or "Sale".</summary>
    public string? Badge { get; init; }
}
