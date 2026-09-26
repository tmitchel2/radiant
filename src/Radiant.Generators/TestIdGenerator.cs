using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Radiant.Generators;

/// <summary>A declared part: its property and its test ID.</summary>
/// <param name="Name">The property.</param>
/// <param name="Modifiers">Its modifiers as declared, which the implementation repeats.</param>
/// <param name="Value">Its test ID.</param>
internal sealed record TestIdPart(string Name, string Modifiers, string Value);

/// <summary>A type declaring parts.</summary>
internal sealed record TestIdModel(
    string FullName,
    string? Namespace,
    EquatableArray<ContainingType> Containers,
    string Keyword,
    string Name,
    string Root,
    bool IsComponent,
    bool SameAssemblyAsElement,
    bool Catalogued,
    EquatableArray<TestIdPart> Parts,
    EquatableArray<DiagnosticInfo> Diagnostics);

/// <summary>
/// Implements the test IDs components declare (<c>[TestId] public static partial string Email { get; }</c>
/// becomes <c>"SignInForm.Email"</c>), names each such component's root after it
/// (<c>DefaultTestId</c>), and lists the components in the assembly's <c>[TestIdCatalog]</c>, which is how
/// test projects find them to make locators.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TestIdGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var types = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntax, cancellation) => Analyze(syntax, cancellation))
            .Where(static model => model is not null)
            .Collect()
            // A type with several parts, or declared in several places, is generated once.
            .Select(static (models, _) => new EquatableArray<TestIdModel>(models.GroupBy(m => m!.FullName).Select(g => g.First()!)));

        context.RegisterSourceOutput(types.SelectMany(static (models, _) => models), static (output, model) => Emit(output, model));
        context.RegisterSourceOutput(types, static (output, models) => EmitCatalog(output, models));
    }

    private static TestIdModel? Analyze(GeneratorSyntaxContext syntax, CancellationToken cancellation)
    {
        if (syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, cancellation) is not IPropertySymbol property
            || Shared.TestIdOf(property) is null)
        {
            return null;
        }
        return Model(property.ContainingType, cancellation);
    }

    internal static TestIdModel Model(INamedTypeSymbol type, CancellationToken cancellation)
    {
        var diagnostics = new List<DiagnosticInfo>();
        var declarations = type.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax(cancellation))
            .OfType<TypeDeclarationSyntax>()
            .ToList();
        if (declarations.Any(d => !d.Modifiers.Any(SyntaxKind.PartialKeyword)))
        {
            diagnostics.Add(Shared.Report(Diagnostics.TestIdTypeNotPartial, declarations[0].Identifier.GetLocation(), type.Name));
        }
        var isComponent = Shared.DerivesFrom(type, Shared.Component);
        if (!isComponent)
        {
            diagnostics.Add(Shared.Report(Diagnostics.TestIdsNotOnComponent, declarations.FirstOrDefault()?.Identifier.GetLocation(), type.Name));
        }

        var parts = new List<TestIdPart>();
        var seen = new Dictionary<string, string>();
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (Shared.TestIdOf(property) is not { } attribute)
            {
                continue;
            }
            var declaration = property.DeclaringSyntaxReferences.Select(r => r.GetSyntax(cancellation)).OfType<PropertyDeclarationSyntax>().FirstOrDefault();
            if (declaration is null)
            {
                continue;
            }
            // The declaration only: its implementation is what's generated.
            if (declaration.ExpressionBody is not null || declaration.AccessorList?.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null) == true)
            {
                continue;
            }
            var shaped = property.IsStatic
                && declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                && property.Type.SpecialType == SpecialType.System_String
                && property.SetMethod is null
                && property.GetMethod is not null;
            if (!shaped)
            {
                diagnostics.Add(Shared.Report(Diagnostics.BadTestIdMember, declaration.Identifier.GetLocation(), property.Name));
                continue;
            }
            var value = Shared.PartValue(type, property, attribute);
            if (seen.TryGetValue(value, out var other))
            {
                diagnostics.Add(Shared.Report(Diagnostics.DuplicateTestId, declaration.Identifier.GetLocation(), property.Name, value, other));
                continue;
            }
            seen[value] = property.Name;
            parts.Add(new TestIdPart(property.Name, declaration.Modifiers.ToString(), value));
        }

        return new TestIdModel(
            type.ToDisplayString(Shared.Qualified),
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            Shared.Containers(type),
            Shared.Keyword(type),
            Shared.NameWithTypeParameters(type),
            Shared.RootName(type),
            isComponent,
            // Where Element's internals can be seen (its own assembly, or a friend), the override keeps them.
            ElementAssembly(type) is { } element
                && (SymbolEqualityComparer.Default.Equals(element, type.ContainingAssembly) || element.GivesAccessTo(type.ContainingAssembly)),
            isComponent && type.TypeParameters.IsEmpty && !type.IsAbstract && diagnostics.Count == 0,
            new EquatableArray<TestIdPart>(parts.ToImmutableArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()));
    }

    private static IAssemblySymbol? ElementAssembly(INamedTypeSymbol type)
    {
        for (var at = type; at is not null; at = at.BaseType)
        {
            if (Shared.Is(at, Shared.Element))
            {
                return at.ContainingAssembly;
            }
        }
        return null;
    }

    private static void Emit(SourceProductionContext output, TestIdModel model)
    {
        Shared.ReportAll(output, model.Diagnostics);
        // A type that isn't partial can't take the rest of itself; the error says why.
        if (model.Diagnostics.Any(d => d.Id == Diagnostics.TestIdTypeNotPartial.Id) || model.Parts.Count == 0 && !model.IsComponent)
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
        foreach (var part in model.Parts)
        {
            code.AppendLine($"{indent}    /// <summary>The test ID <c>{Escape(part.Value)}</c>.</summary>");
            code.AppendLine($"{indent}    {part.Modifiers} string {part.Name} => {Shared.Literal(part.Value)};");
            code.AppendLine();
        }
        if (model.IsComponent)
        {
            code.AppendLine($"{indent}    /// <inheritdoc/>");
            // Overridden from an assembly that can't see Element's internals, a protected internal member is protected.
            var access = model.SameAssemblyAsElement ? "protected internal" : "protected";
            code.AppendLine($"{indent}    {access} override string? DefaultTestId => {Shared.Literal(model.Root)};");
        }
        code.AppendLine($"{indent}}}");
        for (var i = model.Containers.Count - 1; i >= 0; i--)
        {
            indent = indent.Substring(4);
            code.AppendLine($"{indent}}}");
        }
        output.AddSource($"{model.FullName.Replace("global::", "")}.TestIds.g.cs", code.ToString());
    }

    private static void EmitCatalog(SourceProductionContext output, EquatableArray<TestIdModel> models)
    {
        var catalogued = models.Where(m => m.Catalogued).OrderBy(m => m.FullName, System.StringComparer.Ordinal).ToList();
        if (catalogued.Count == 0)
        {
            return;
        }
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("// The components here that declare test IDs, for test projects to make locators for.");
        code.AppendLine("[assembly: global::Radiant.UI.Core.TestIdCatalog(");
        code.AppendLine(string.Join(",\n", catalogued.Select(m => $"    typeof({m.FullName})")) + ")]");
        output.AddSource("TestIdCatalog.g.cs", code.ToString());
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
