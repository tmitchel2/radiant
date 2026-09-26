using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Radiant.Generators;

/// <summary>
/// Generates style facets for partial records:
/// <list type="bullet">
/// <item>each property of every <c>[StyleFacet]</c> interface the record implements (unless it declares it itself);</item>
/// <item>a <c>Forward{Name}(target)</c> method for each <c>[ForwardFacets]</c>, returning the target with the
/// record's facet values copied onto it (a null value leaves the target's own).</item>
/// </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class FacetGenerator : IIncrementalGenerator
{
    private const string FacetAttribute = "Radiant.UI.Core.StyleFacetAttribute";
    private const string ForwardAttribute = "Radiant.UI.Core.ForwardFacetsAttribute";

    private static readonly SymbolDisplayFormat s_qualified = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var records = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax type && (type.BaseList is not null || type.AttributeLists.Count > 0),
                static (syntax, cancellation) => Analyze(syntax, cancellation))
            .Where(static model => model is not null)
            .Collect()
            // A partial record is declared in several places; generate it once.
            .SelectMany(static (models, _) => models.GroupBy(m => m!.FullName).Select(g => g.First()!));

        context.RegisterSourceOutput(records, static (output, model) => Emit(output, model));
    }

    private static RecordModel? Analyze(GeneratorSyntaxContext syntax, CancellationToken cancellation)
    {
        if (syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) is not INamedTypeSymbol symbol)
        {
            return null;
        }
        var facets = symbol.AllInterfaces.Where(IsFacet).ToList();
        var forwards = symbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == ForwardAttribute).ToList();
        if (facets.Count == 0 && forwards.Count == 0)
        {
            return null;
        }

        var diagnostics = new List<DiagnosticInfo>();
        var declarations = symbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(cancellation))
            .OfType<TypeDeclarationSyntax>()
            .ToList();
        if (declarations.Any(d => !d.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            diagnostics.Add(Report(Diagnostics.NotPartial, declarations[0].Identifier.GetLocation(), symbol.Name));
        }
        if (!symbol.IsRecord)
        {
            diagnostics.Add(Report(Diagnostics.NotRecord, declarations[0].Identifier.GetLocation(), symbol.Name));
        }
        if (diagnostics.Count > 0)
        {
            return Model(symbol, [], [], diagnostics);
        }

        // Facet properties the record doesn't declare itself.
        var properties = new List<FacetProperty>();
        var names = new HashSet<string>();
        foreach (var facet in facets)
        {
            foreach (var member in facet.GetMembers())
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet })
                {
                    continue;
                }
                if (member is not IPropertySymbol { SetMethod.IsInitOnly: true } property)
                {
                    diagnostics.Add(Report(Diagnostics.BadFacetMember, member.Locations.FirstOrDefault(), member.Name, facet.Name));
                    continue;
                }
                if (names.Add(property.Name) && symbol.GetMembers(property.Name).IsEmpty)
                {
                    properties.Add(new FacetProperty(property.Name, property.Type.ToDisplayString(s_qualified), facet.ToDisplayString(s_qualified)));
                }
            }
        }

        var forwarders = new List<Forwarder>();
        foreach (var attribute in forwards)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax(cancellation).GetLocation();
            if (attribute.ConstructorArguments.Length < 3
                || attribute.ConstructorArguments[0].Value is not INamedTypeSymbol target
                || attribute.ConstructorArguments[1].Value is not string name)
            {
                continue;
            }
            if (!target.IsRecord)
            {
                diagnostics.Add(Report(Diagnostics.NotRecord, location, target.Name));
                continue;
            }
            var copied = new List<ForwardedProperty>();
            foreach (var value in attribute.ConstructorArguments[2].Values)
            {
                if (value.Value is not INamedTypeSymbol facet)
                {
                    continue;
                }
                if (!IsFacet(facet))
                {
                    diagnostics.Add(Report(Diagnostics.NotAFacet, location, facet.Name));
                    continue;
                }
                var missing = false;
                foreach (var holder in new[] { symbol, target })
                {
                    if (!holder.AllInterfaces.Contains(facet, SymbolEqualityComparer.Default))
                    {
                        diagnostics.Add(Report(Diagnostics.NotImplemented, location, holder.Name, facet.Name));
                        missing = true;
                    }
                }
                if (missing)
                {
                    continue;
                }
                foreach (var property in facet.GetMembers().OfType<IPropertySymbol>())
                {
                    copied.Add(new ForwardedProperty(property.Name, IsNullable(property.Type)));
                }
            }
            forwarders.Add(new Forwarder(name, target.ToDisplayString(s_qualified), new EquatableArray<ForwardedProperty>(copied)));
        }

        return Model(symbol, properties, forwarders, diagnostics);
    }

    private static RecordModel Model(INamedTypeSymbol symbol, List<FacetProperty> properties, List<Forwarder> forwarders, List<DiagnosticInfo> diagnostics)
    {
        var containers = new List<ContainingType>();
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            containers.Insert(0, new ContainingType(Keyword(type), NameWithTypeParameters(type)));
        }
        return new RecordModel(
            symbol.ToDisplayString(s_qualified),
            symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
            new EquatableArray<ContainingType>(containers),
            Keyword(symbol),
            NameWithTypeParameters(symbol),
            new EquatableArray<FacetProperty>(properties),
            new EquatableArray<Forwarder>(forwarders),
            new EquatableArray<DiagnosticInfo>(diagnostics));
    }

    private static bool IsFacet(INamedTypeSymbol type) =>
        type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == FacetAttribute);

    private static bool IsNullable(ITypeSymbol type) =>
        type.NullableAnnotation == NullableAnnotation.Annotated
        || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static string Keyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, IsValueType: true } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };

    private static string NameWithTypeParameters(INamedTypeSymbol type) =>
        type.TypeParameters.IsEmpty ? type.Name : $"{type.Name}<{string.Join(", ", type.TypeParameters.Select(p => p.Name))}>";

    private static DiagnosticInfo Report(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
    {
        var span = location?.SourceSpan ?? default;
        return new DiagnosticInfo(descriptor.Id, location?.SourceTree?.FilePath ?? "", span.Start, span.Length,
            new EquatableArray<string>(arguments.ToImmutableArray()));
    }

    private static void Emit(SourceProductionContext output, RecordModel model)
    {
        foreach (var info in model.Diagnostics)
        {
            var location = info.FilePath.Length == 0
                ? Location.None
                : Location.Create(info.FilePath, new TextSpan(info.Start, info.Length), default);
            output.ReportDiagnostic(Diagnostic.Create(Diagnostics.ById(info.Id), location, info.Arguments.ToArray<object>()));
        }
        if (model.Properties.Count == 0 && model.Forwarders.Count == 0)
        {
            return;
        }

        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("#nullable enable");
        code.AppendLine();
        if (model.Namespace is { } ns)
        {
            code.AppendLine($"namespace {ns};");
            code.AppendLine();
        }
        var indent = "";
        foreach (var container in model.Containers)
        {
            code.AppendLine($"{indent}partial {container.Keyword} {container.Name}");
            code.AppendLine($"{indent}{{");
            indent += "    ";
        }
        code.AppendLine($"{indent}partial {model.Keyword} {model.Name}");
        code.AppendLine($"{indent}{{");
        var first = true;
        foreach (var property in model.Properties)
        {
            if (!first)
            {
                code.AppendLine();
            }
            first = false;
            code.AppendLine($"{indent}    /// <inheritdoc cref=\"{property.Facet}.{property.Name}\"/>");
            code.AppendLine($"{indent}    public {property.Type} {property.Name} {{ get; init; }}");
        }
        foreach (var forwarder in model.Forwarders)
        {
            if (!first)
            {
                code.AppendLine();
            }
            first = false;
            code.AppendLine($"{indent}    /// <summary>");
            code.AppendLine($"{indent}    /// The {forwarder.Name} part: <paramref name=\"target\"/> with this element's forwarded facets");
            code.AppendLine($"{indent}    /// copied onto it, where this element sets them.");
            code.AppendLine($"{indent}    /// </summary>");
            code.AppendLine($"{indent}    private {forwarder.Target} Forward{forwarder.Name}({forwarder.Target} target) => target with");
            code.AppendLine($"{indent}    {{");
            foreach (var property in forwarder.Properties)
            {
                var value = property.KeepTargetWhenUnset ? $"this.{property.Name} ?? target.{property.Name}" : $"this.{property.Name}";
                code.AppendLine($"{indent}        {property.Name} = {value},");
            }
            code.AppendLine($"{indent}    }};");
        }
        code.AppendLine($"{indent}}}");
        for (var i = model.Containers.Count - 1; i >= 0; i--)
        {
            indent = indent.Substring(4);
            code.AppendLine($"{indent}}}");
        }

        var hint = model.FullName.Replace("global::", "").Replace('<', '[').Replace('>', ']');
        output.AddSource($"{hint}.Facets.g.cs", code.ToString());
    }
}
