using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Radiant.Generators;

/// <summary>A part as its locator has it.</summary>
/// <param name="Method">The method's name.</param>
/// <param name="Property">The component's property holding its test ID, fully qualified.</param>
/// <param name="Locator">The part's own locator, if it's a component that has one.</param>
internal sealed record LocatorPart(string Method, string Property, string? Locator);

/// <summary>A component a locator is made for.</summary>
internal sealed record LocatorModel(string Component, string Root, string Locator, string Entry, EquatableArray<LocatorPart> Parts);

/// <summary>Every locator for a compilation, and what was noticed making them.</summary>
internal sealed record LocatorsModel(EquatableArray<LocatorModel> Locators, EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>
/// In a test project (one that references <c>Radiant.UI.Driver</c>), makes a typed locator for each
/// component that declares parts: those in referenced assemblies' <c>[TestIdCatalog]</c>, and the project's
/// own. <c>Driver.SignInForm()</c> finds the component by its root's test ID, <c>.Email()</c> its part within
/// it; each part refers to the component's own property, so the ID is never written twice.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LocatorGenerator : IIncrementalGenerator
{
    private const string ComponentLocator = "Radiant.UI.Driver.ComponentLocator`1";
    private const string AppDriver = "Radiant.UI.Driver.AppDriver";
    private const string Namespace = "Radiant.UI.Driver.Locators";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var model = context.CompilationProvider.Select(static (compilation, cancellation) => Model(compilation, cancellation));
        context.RegisterSourceOutput(model, static (output, model) => Emit(output, model));
    }

    private static LocatorsModel Model(Compilation compilation, System.Threading.CancellationToken cancellation)
    {
        var empty = new LocatorsModel(new EquatableArray<LocatorModel>(ImmutableArray<LocatorModel>.Empty), new EquatableArray<DiagnosticInfo>(ImmutableArray<DiagnosticInfo>.Empty));
        if (compilation.GetTypeByMetadataName(ComponentLocator) is not { } baseLocator || compilation.GetTypeByMetadataName(AppDriver) is not { } driver)
        {
            return empty;
        }

        var components = new List<INamedTypeSymbol>();
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            foreach (var catalog in assembly.GetAttributes().Where(a => a.AttributeClass is { } c && Shared.Is(c, Shared.CatalogAttribute)))
            {
                foreach (var argument in catalog.ConstructorArguments.SelectMany(a => a.Kind == TypedConstantKind.Array ? a.Values : [a]))
                {
                    if (argument.Value is INamedTypeSymbol type)
                    {
                        components.Add(type);
                    }
                }
            }
        }
        // The project's own components, which its catalogue (made alongside) can't be read for yet.
        components.AddRange(OwnComponents(compilation.Assembly.GlobalNamespace, cancellation));
        components = [.. components
            .Where(c => compilation.IsSymbolAccessibleWithin(c, compilation.Assembly))
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .OrderBy(c => c.ToDisplayString(), System.StringComparer.Ordinal)];

        var diagnostics = new List<DiagnosticInfo>();
        // Names a locator mustn't take: what the driver and locators already have.
        var taken = new HashSet<string>(Members(driver).Concat(Members(baseLocator)));
        var partTaken = new HashSet<string>(Members(baseLocator)) { "Create", "Part" };
        var names = new Dictionary<INamedTypeSymbol, (string Locator, string Entry)>(SymbolEqualityComparer.Default);
        var seen = new Dictionary<string, INamedTypeSymbol>();
        foreach (var component in components)
        {
            var name = component.Name;
            if (seen.TryGetValue(name, out var first))
            {
                var qualified = component.ContainingNamespace.ToDisplayString().Replace('.', '_') + "_" + name;
                diagnostics.Add(Shared.Report(Diagnostics.LocatorNameClash, null, first.ToDisplayString(), component.ToDisplayString(), qualified));
                name = qualified;
            }
            else
            {
                seen[name] = component;
            }
            var entry = name;
            if (taken.Contains(entry))
            {
                entry = name + "Component";
                diagnostics.Add(Shared.Report(Diagnostics.LocatorMemberClash, null, name, entry));
            }
            names[component] = (name + "Locator", entry);
        }

        var locators = new List<LocatorModel>();
        foreach (var component in components)
        {
            var parts = new List<LocatorPart>();
            foreach (var property in component.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic || Shared.TestIdOf(property) is not { } attribute || !compilation.IsSymbolAccessibleWithin(property, compilation.Assembly))
                {
                    continue;
                }
                var partType = attribute.AttributeClass is { IsGenericType: true } generic ? generic.TypeArguments[0] as INamedTypeSymbol : null;
                var partLocator = partType is not null && names.TryGetValue(partType, out var found) ? found.Locator : null;
                var method = partTaken.Contains(property.Name) ? property.Name + "Part" : property.Name;
                parts.Add(new LocatorPart(method, component.ToDisplayString(Shared.Qualified) + "." + property.Name, partLocator));
            }
            locators.Add(new LocatorModel(
                component.ToDisplayString(Shared.Qualified),
                Shared.RootName(component),
                names[component].Locator,
                names[component].Entry,
                new EquatableArray<LocatorPart>(parts.ToImmutableArray())));
        }
        return new LocatorsModel(new EquatableArray<LocatorModel>(locators.ToImmutableArray()), new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()));
    }

    private static IEnumerable<INamedTypeSymbol> OwnComponents(INamespaceSymbol ns, System.Threading.CancellationToken cancellation)
    {
        foreach (var member in ns.GetMembers())
        {
            cancellation.ThrowIfCancellationRequested();
            if (member is INamespaceSymbol inner)
            {
                foreach (var type in OwnComponents(inner, cancellation))
                {
                    yield return type;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                foreach (var found in WithNested(type))
                {
                    yield return found;
                }
            }
        }

        static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type)
        {
            if (type.TypeParameters.IsEmpty && !type.IsAbstract && Shared.DerivesFrom(type, Shared.Component)
                && type.GetMembers().OfType<IPropertySymbol>().Any(p => p.IsStatic && Shared.TestIdOf(p) is not null))
            {
                yield return type;
            }
            foreach (var nested in type.GetTypeMembers().SelectMany(WithNested))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<string> Members(INamedTypeSymbol type)
    {
        for (var at = type; at is not null; at = at.BaseType)
        {
            foreach (var member in at.GetMembers())
            {
                yield return member.Name;
            }
        }
    }

    private static void Emit(SourceProductionContext output, LocatorsModel model)
    {
        Shared.ReportAll(output, model.Diagnostics);
        if (model.Locators.Count == 0)
        {
            return;
        }
        const string Selector = "global::Radiant.Host.AgentControlProtocol.Selector";
        const string Driver = "global::Radiant.UI.Driver.AppDriver";
        foreach (var locator in model.Locators)
        {
            var code = new StringBuilder();
            code.AppendLine("// <auto-generated/>");
            code.AppendLine("#nullable enable");
            code.AppendLine();
            code.AppendLine($"namespace {Namespace};");
            code.AppendLine();
            code.AppendLine($"/// <summary>A <see cref=\"{locator.Component}\"/> and its parts.</summary>");
            code.AppendLine($"internal sealed class {locator.Locator} : global::Radiant.UI.Driver.ComponentLocator<{locator.Locator}>");
            code.AppendLine("{");
            code.AppendLine($"    /// <summary>The <see cref=\"{locator.Component}\"/>s <paramref name=\"selector\"/> means, their parts looked for within <paramref name=\"partScope\"/>.</summary>");
            code.AppendLine($"    public {locator.Locator}({Driver} driver, {Selector} selector, {Selector}? partScope) : base(driver, selector, partScope)");
            code.AppendLine("    {");
            code.AppendLine("    }");
            code.AppendLine();
            code.AppendLine("    /// <inheritdoc/>");
            code.AppendLine($"    protected override {locator.Locator} Create({Selector} selector, {Selector}? partScope) => new(Driver, selector, partScope);");
            foreach (var part in locator.Parts)
            {
                code.AppendLine();
                code.AppendLine($"    /// <summary>Its <see cref=\"{part.Property}\"/> part.</summary>");
                code.AppendLine(part.Locator is { } partLocator
                    ? $"    public {partLocator} {part.Method}() => Part({part.Property}, static (driver, selector, scope) => new {partLocator}(driver, selector, scope));"
                    : $"    public global::Radiant.UI.Driver.Locator {part.Method}() => Part({part.Property});");
            }
            code.AppendLine("}");
            output.AddSource($"{locator.Locator}.g.cs", code.ToString());
        }

        var entries = new StringBuilder();
        entries.AppendLine("// <auto-generated/>");
        entries.AppendLine("#nullable enable");
        entries.AppendLine();
        entries.AppendLine("namespace Radiant.UI.Driver;");
        entries.AppendLine();
        entries.AppendLine("/// <summary>Where each component's locator starts: anywhere in the app, or within a scope.</summary>");
        entries.AppendLine("internal static partial class RadiantLocators");
        entries.AppendLine("{");
        foreach (var locator in model.Locators)
        {
            var type = $"global::{Namespace}.{locator.Locator}";
            var root = Shared.Literal(locator.Root);
            entries.AppendLine($"    /// <summary>The <see cref=\"{locator.Component}\"/>s in the app.</summary>");
            entries.AppendLine($"    public static {type} {locator.Entry}(this {Driver} driver) => new(driver, new {Selector} {{ TestId = {root} }}, null);");
            entries.AppendLine();
            entries.AppendLine($"    /// <summary>The <see cref=\"{locator.Component}\"/>s inside <paramref name=\"scope\"/>.</summary>");
            entries.AppendLine($"    public static {type} {locator.Entry}(this global::Radiant.UI.Driver.Locator scope) => new(scope.Driver, new {Selector} {{ TestId = {root}, Within = scope.Selector }}, scope.Selector);");
            entries.AppendLine();
        }
        entries.AppendLine("}");
        output.AddSource("RadiantLocators.g.cs", entries.ToString());
    }
}
