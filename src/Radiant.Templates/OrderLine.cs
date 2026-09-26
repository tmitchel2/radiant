namespace Radiant.Templates;

/// <summary>An item in an <see cref="Order"/>.</summary>
/// <param name="Name">What was bought.</param>
/// <param name="Quantity">How many.</param>
/// <param name="Price">What they cost, as shown.</param>
public sealed record OrderLine(string Name, int Quantity, string Price);
