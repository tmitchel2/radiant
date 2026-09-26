namespace Radiant.Templates;

/// <summary>What a <see cref="CheckoutForm"/> collected.</summary>
/// <param name="Email">Where to send the receipt.</param>
/// <param name="Name">Who it's for.</param>
/// <param name="Address">The street address.</param>
/// <param name="City">The town or city.</param>
/// <param name="Postcode">The postcode.</param>
/// <param name="Country">The country, as chosen.</param>
/// <param name="Delivery">The delivery option's index.</param>
public sealed record CheckoutDetails(string Email, string Name, string Address, string City, string Postcode, string Country, int Delivery);
