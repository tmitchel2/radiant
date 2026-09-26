using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Radiant.UI.Core;

namespace Radiant.Generators.Tests;

/// <summary>Runs the generators (and analyzers) over source text, as the compiler would.</summary>
internal static class GeneratorHarness
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> s_references = new(() =>
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return platform
            .Where(path => Path.GetFileName(path) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "netstandard.dll"
                or "System.Collections.dll" or "System.Linq.dll")
            .Append(typeof(StyleFacetAttribute).Assembly.Location)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    });

    /// <summary>The generated sources, the generator's diagnostics, and the compiler's on the result.</summary>
    public static (string[] Generated, Diagnostic[] GeneratorDiagnostics, Diagnostic[] CompileErrors) Run(string source) =>
        Run(source, new FacetGenerator());

    /// <summary>
    /// The sources <paramref name="generator"/> makes of <paramref name="source"/> (compiled against
    /// <paramref name="references"/> as well as the usual), their diagnostics, and the compiler's errors on the result.
    /// </summary>
    public static (string[] Generated, Diagnostic[] GeneratorDiagnostics, Diagnostic[] CompileErrors) Run(
        string source, IIncrementalGenerator generator, params MetadataReference[] references)
    {
        var (generated, diagnostics, errors, _) = Compile(source, "Test", [generator], references);
        return (generated, diagnostics, errors);
    }

    /// <summary>Compiles <paramref name="source"/> with <paramref name="generators"/>, returning the compilation too.</summary>
    public static (string[] Generated, Diagnostic[] GeneratorDiagnostics, Diagnostic[] CompileErrors, Compilation Output) Compile(
        string source, string assemblyName, IIncrementalGenerator[] generators, params MetadataReference[] references)
    {
        var compilation = CSharpCompilation.Create(assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path: "Test.cs")],
            s_references.Value.AddRange(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(generators.Select(GeneratorExtensions.AsSourceGenerator).ToArray())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var generated = driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()).ToArray();
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        return (generated, diagnostics.ToArray(), errors, output);
    }

    /// <summary>What <paramref name="analyzer"/> reports on <paramref name="source"/>, after the test ID generator has run.</summary>
    public static Diagnostic[] Analyze(string source, Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer analyzer)
    {
        var (_, _, _, output) = Compile(source, "Test", [new TestIdGenerator()]);
        return Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzerExtensions.WithAnalyzers(output, [analyzer]).GetAnalyzerDiagnosticsAsync().Result.ToArray();
    }
}
