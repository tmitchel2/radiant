using Microsoft.CodeAnalysis;

namespace Radiant.Generators;

/// <summary>What the facet generator reports.</summary>
internal static class Diagnostics
{
    private const string Category = "Radiant.Facets";

    public static readonly DiagnosticDescriptor NotImplemented = new(
        "RAD002", "Forwarded facet not implemented",
        "'{0}' does not implement the facet '{1}', so it cannot be forwarded to it", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Both the component and the part a facet is forwarded to must implement the facet.");

    public static readonly DiagnosticDescriptor NotPartial = new(
        "RAD004", "Facet record must be partial",
        "'{0}' must be declared partial so its facet properties and forwarders can be generated", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotRecord = new(
        "RAD005", "Facets need records",
        "'{0}' must be a record: facets are init properties and forwarding copies with 'with'", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor BadFacetMember = new(
        "RAD006", "Facet members must be init properties",
        "'{0}' in facet '{1}' must be a property with 'get; init;'", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotAFacet = new(
        "RAD007", "Forwarded type is not a facet",
        "'{0}' is not marked [StyleFacet], so it cannot be forwarded", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static DiagnosticDescriptor ById(string id) => id switch
    {
        "RAD002" => NotImplemented,
        "RAD004" => NotPartial,
        "RAD005" => NotRecord,
        "RAD006" => BadFacetMember,
        _ => NotAFacet,
    };
}
