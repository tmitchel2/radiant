using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Radiant.Generators;

/// <summary>What the generators share: names of Radiant's types, and turning symbols into text.</summary>
internal static class Shared
{
    public const string TestIdAttribute = "Radiant.UI.Core.TestIdAttribute";
    public const string GenericTestIdAttribute = "Radiant.UI.Core.TestIdAttribute`1";
    public const string TestIdsAttribute = "Radiant.UI.Core.TestIdsAttribute";
    public const string CatalogAttribute = "Radiant.UI.Core.TestIdCatalogAttribute";
    public const string RequiresTestIdAttribute = "Radiant.UI.Core.RequiresTestIdAttribute";
    public const string Component = "Radiant.UI.Core.Component";
    public const string Element = "Radiant.UI.Core.Element";

    public static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    /// <summary>Whether an attribute is <c>[TestId]</c> or <c>[TestId&lt;T&gt;]</c>.</summary>
    public static bool IsTestId(AttributeData attribute) =>
        attribute.AttributeClass is { } type
        && (Is(type, TestIdAttribute) || Is(type.OriginalDefinition, GenericTestIdAttribute));

    /// <summary>A property's <c>[TestId]</c>, if it has one.</summary>
    public static AttributeData? TestIdOf(ISymbol member) => member.GetAttributes().FirstOrDefault(IsTestId);

    /// <summary>Whether <paramref name="type"/> is the type <paramref name="metadataName"/> names.</summary>
    public static bool Is(INamedTypeSymbol type, string metadataName) =>
        type.ContainingNamespace?.ToDisplayString() + "." + type.MetadataName == metadataName;

    /// <summary>Whether <paramref name="type"/> is, or derives from, the type <paramref name="metadataName"/> names.</summary>
    public static bool DerivesFrom(ITypeSymbol? type, string metadataName)
    {
        for (var at = type as INamedTypeSymbol; at is not null; at = at.BaseType)
        {
            if (Is(at, metadataName))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A component's root test ID: its <c>[TestIds]</c> name, or its type's.</summary>
    public static string RootName(INamedTypeSymbol type) =>
        type.GetAttributes().FirstOrDefault(a => a.AttributeClass is { } c && Is(c, TestIdsAttribute)) is { } ids
        && ids.ConstructorArguments.Length == 1 && ids.ConstructorArguments[0].Value is string name
            ? name
            : type.Name;

    /// <summary>A part's test ID: its attribute's value, or <c>"&lt;root&gt;.&lt;property&gt;"</c>.</summary>
    public static string PartValue(INamedTypeSymbol type, IPropertySymbol property, AttributeData attribute) =>
        attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string value
            ? value
            : RootName(type) + "." + property.Name;

    public static string Keyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, IsValueType: true } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };

    public static string NameWithTypeParameters(INamedTypeSymbol type) =>
        type.TypeParameters.IsEmpty ? type.Name : $"{type.Name}<{string.Join(", ", type.TypeParameters.Select(p => p.Name))}>";

    /// <summary>The types a type is nested in, outermost first.</summary>
    public static EquatableArray<ContainingType> Containers(INamedTypeSymbol symbol)
    {
        var containers = ImmutableArray.CreateBuilder<ContainingType>();
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            containers.Insert(0, new ContainingType(Keyword(type), NameWithTypeParameters(type)));
        }
        return new EquatableArray<ContainingType>(containers.ToImmutable());
    }

    public static DiagnosticInfo Report(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
    {
        var span = location?.SourceSpan ?? default;
        return new DiagnosticInfo(descriptor.Id, location?.SourceTree?.FilePath ?? "", span.Start, span.Length,
            new EquatableArray<string>(arguments.ToImmutableArray()));
    }

    public static void ReportAll(SourceProductionContext output, EquatableArray<DiagnosticInfo> diagnostics)
    {
        foreach (var info in diagnostics)
        {
            var location = info.FilePath.Length == 0
                ? Location.None
                : Location.Create(info.FilePath, new TextSpan(info.Start, info.Length), default);
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.ById(info.Id), location, info.Arguments.ToArray<object>()));
        }
    }

    /// <summary>A C# string literal.</summary>
    public static string Literal(string value) => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
}
