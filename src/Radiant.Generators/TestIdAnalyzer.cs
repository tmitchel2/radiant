using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Radiant.Generators;

/// <summary>
/// Makes test IDs mandatory on controls. Each element made of a type marked <c>[RequiresTestId]</c> (or
/// derived from one) must be given a <c>TestId</c>, in its initializer or a <c>with</c> right after
/// (RAD030), and it must be a declared part (<c>TestId = Submit</c>) or one passed on (<c>TestId = TestId</c>,
/// a parameter, a variable), never a string written out (RAD031), so no one has to know the string.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestIdAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Diagnostics.MissingTestId, Diagnostics.LiteralTestId);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Nothing to check where the attribute doesn't exist.
            if (start.Compilation.GetTypeByMetadataName(Shared.RequiresTestIdAttribute) is not { } marker)
            {
                return;
            }
            start.RegisterOperationAction(operation => Check(operation, marker), OperationKind.ObjectCreation);
        });
    }

    private static void Check(OperationAnalysisContext context, INamedTypeSymbol marker)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type is not INamedTypeSymbol type || !Requires(type, marker))
        {
            return;
        }
        var value = Assigned(creation.Initializer);
        // new X(…) with { TestId = … } sets it as well.
        if (value is null && creation.Parent is IWithOperation with && ReferenceEquals(with.Operand, creation))
        {
            value = Assigned(with.Initializer);
        }
        if (value is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.MissingTestId, creation.Syntax.GetLocation(), type.Name));
        }
        else if (!Acceptable(value))
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.LiteralTestId, value.Syntax.GetLocation(), type.Name));
        }
    }

    private static bool Requires(INamedTypeSymbol type, INamedTypeSymbol marker)
    {
        for (var at = type; at is not null; at = at.BaseType)
        {
            if (at.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, marker)))
            {
                return true;
            }
        }
        return false;
    }

    // What the initializer sets TestId to, if it does.
    private static IOperation? Assigned(IObjectOrCollectionInitializerOperation? initializer) =>
        initializer?.Initializers
            .OfType<ISimpleAssignmentOperation>()
            .FirstOrDefault(a => a.Target is IPropertyReferenceOperation { Property.Name: "TestId" })
            ?.Value;

    // A declared part, the element's own passed on, or anything decided at run time; not a string
    // written out, a constant, or null.
    private static bool Acceptable(IOperation value)
    {
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }
        switch (value)
        {
            case ILiteralOperation or IInterpolatedStringOperation or IDefaultValueOperation:
                return false;
            case IConditionalOperation conditional:
                return conditional.WhenFalse is not null && Acceptable(conditional.WhenTrue) && Acceptable(conditional.WhenFalse);
            case ICoalesceOperation coalesce:
                return Acceptable(coalesce.Value) && Acceptable(coalesce.WhenNull);
            case IPropertyReferenceOperation property when property.Property.IsStatic:
                return Shared.TestIdOf(property.Property) is not null || !value.ConstantValue.HasValue;
            case IFieldReferenceOperation field:
                return !field.Field.IsConst;
        }
        return !value.ConstantValue.HasValue;
    }
}
