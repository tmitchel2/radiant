namespace Radiant.Templates;

/// <summary>A way to deliver, for <see cref="CheckoutForm"/>.</summary>
/// <param name="Name">What it's called ("Standard").</param>
/// <param name="Detail">How long it takes ("4–10 business days").</param>
/// <param name="Price">What it costs, as shown.</param>
public sealed record DeliveryOption(string Name, string Detail, string Price);
