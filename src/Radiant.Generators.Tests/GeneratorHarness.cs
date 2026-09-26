using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Radiant.UI.Core;

namespace Radiant.Generators.Tests;

/// <summary>Runs the facet generator over source text, as the compiler would.</summary>
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
    public static (string[] Generated, Diagnostic[] GeneratorDiagnostics, Diagnostic[] CompileErrors) Run(string source)
    {
        var compilation = CSharpCompilation.Create("Test",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest), path: "Test.cs")],
            s_references.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(new FacetGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var generated = driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()).ToArray();
        var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        return (generated, diagnostics.ToArray(), errors);
    }
}
