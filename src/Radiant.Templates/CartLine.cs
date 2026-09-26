namespace Radiant.Templates;

/// <summary>A line of a <see cref="CartSummary"/>.</summary>
/// <param name="Product">What's in the cart.</param>
/// <param name="Quantity">How many.</param>
/// <param name="UnitPrice">The price of one.</param>
public sealed record CartLine(Product Product, int Quantity, decimal UnitPrice);
