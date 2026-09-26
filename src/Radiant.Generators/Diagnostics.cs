using Microsoft.CodeAnalysis;

namespace Radiant.Generators;

/// <summary>What the generators and analyzers report.</summary>
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

    private const string TestIds = "Radiant.TestIds";

    public static readonly DiagnosticDescriptor BadTestIdMember = new(
        "RAD010", "A test ID must be a static partial string property",
        "'{0}' must be declared 'static partial string {0} {{ get; }}' so its test ID can be generated", TestIds,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestIdTypeNotPartial = new(
        "RAD011", "A type declaring test IDs must be partial",
        "'{0}' must be declared partial so its test IDs can be generated", TestIds,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateTestId = new(
        "RAD012", "Two parts share a test ID",
        "'{0}' has the test ID '{1}', which '{2}' already has", TestIds,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TestIdsNotOnComponent = new(
        "RAD013", "Test IDs are declared on components",
        "'{0}' declares test IDs but isn't a Component: parts are declared by the component that builds them", TestIds,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LocatorNameClash = new(
        "RAD020", "Two components share a name",
        "'{0}' and '{1}' have the same name, so the locator for '{1}' is '{2}'", TestIds,
        DiagnosticSeverity.Info, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor LocatorMemberClash = new(
        "RAD021", "A locator's name is taken",
        "'{0}' would hide a member of the driver or a locator, so it's '{1}'", TestIds,
        DiagnosticSeverity.Info, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingTestId = new(
        "RAD030", "A control needs a test ID",
        "'{0}' must be given a TestId from a declared [TestId] part (or one passed on to it)", TestIds,
        DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "Controls marked [RequiresTestId] must be findable by tests and agents: declare a [TestId] part on the component that builds it and use it.");

    public static readonly DiagnosticDescriptor LiteralTestId = new(
        "RAD031", "A test ID must be declared, not written out",
        "'{0}' is given a TestId that's a string; use a declared [TestId] part instead, so tests needn't know the string", TestIds,
        DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static DiagnosticDescriptor ById(string id) => id switch
    {
        "RAD002" => NotImplemented,
        "RAD004" => NotPartial,
        "RAD005" => NotRecord,
        "RAD006" => BadFacetMember,
        "RAD010" => BadTestIdMember,
        "RAD011" => TestIdTypeNotPartial,
        "RAD012" => DuplicateTestId,
        "RAD013" => TestIdsNotOnComponent,
        "RAD020" => LocatorNameClash,
        "RAD021" => LocatorMemberClash,
        "RAD030" => MissingTestId,
        "RAD031" => LiteralTestId,
        _ => NotAFacet,
    };
}
