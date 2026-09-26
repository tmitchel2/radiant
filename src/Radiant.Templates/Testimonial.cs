namespace Radiant.Templates;

/// <summary>What a customer said, for <see cref="Testimonials"/>.</summary>
/// <param name="Quote">Their words.</param>
/// <param name="Name">Who said it.</param>
/// <param name="Role">Their role and company ("CTO, Northwind").</param>
public sealed record Testimonial(string Quote, string Name, string Role);
