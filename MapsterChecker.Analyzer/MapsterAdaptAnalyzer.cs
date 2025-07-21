using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace MapsterChecker.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MapsterAdaptAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.NullableToNonNullableMapping,
            DiagnosticDescriptors.IncompatibleTypeMapping,
            DiagnosticDescriptors.MissingPropertyMapping,
            DiagnosticDescriptors.PropertyNullableToNonNullableMapping,
            DiagnosticDescriptors.PropertyIncompatibleTypeMapping,
            DiagnosticDescriptors.PropertyMissingMapping);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        if (!IsMapsterAdaptCall(invocation, context.SemanticModel))
            return;

        var adaptCallInfo = ExtractAdaptCallInfo(invocation, context.SemanticModel);
        if (adaptCallInfo == null)
            return;

        var compatibilityChecker = new TypeCompatibilityChecker(context.SemanticModel);
        var compatibilityResult = compatibilityChecker.CheckCompatibility(
            adaptCallInfo.SourceType, 
            adaptCallInfo.DestinationType);

        ReportDiagnostics(context, invocation, compatibilityResult, adaptCallInfo);
    }

    private static bool IsMapsterAdaptCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess?.Name.Identifier.ValueText != "Adapt")
            return false;

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return false;

        return IsMapsterMethod(methodSymbol);
    }

    private static bool IsMapsterMethod(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType;
        if (containingType == null)
            return false;

        if (containingType.Name == "TypeAdapterExtensions" || 
            containingType.Name == "TypeAdapter")
        {
            var namespaceName = containingType.ContainingNamespace?.ToDisplayString();
            return namespaceName == "Mapster";
        }

        return false;
    }

    private static AdaptCallInfo? ExtractAdaptCallInfo(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return null;

        var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
        if (memberAccess == null)
            return null;

        var sourceType = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
        if (sourceType == null)
            return null;

        ITypeSymbol? destinationType = null;

        if (methodSymbol.IsGenericMethod && methodSymbol.TypeArguments.Length > 0)
        {
            destinationType = methodSymbol.TypeArguments[0];
        }
        else if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var firstArgument = invocation.ArgumentList.Arguments[0];
            destinationType = semanticModel.GetTypeInfo(firstArgument.Expression).Type;
        }

        if (destinationType == null)
            return null;

        return new AdaptCallInfo(sourceType, destinationType, invocation.GetLocation());
    }

    private static void ReportDiagnostics(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        TypeCompatibilityResult compatibilityResult,
        AdaptCallInfo adaptCallInfo)
    {
        if (compatibilityResult.HasNullabilityIssue)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.NullableToNonNullableMapping,
                adaptCallInfo.Location,
                adaptCallInfo.SourceType.ToDisplayString(),
                adaptCallInfo.DestinationType.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }

        if (compatibilityResult.HasIncompatibilityIssue)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.IncompatibleTypeMapping,
                adaptCallInfo.Location,
                adaptCallInfo.SourceType.ToDisplayString(),
                adaptCallInfo.DestinationType.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }

        foreach (var propertyIssue in compatibilityResult.PropertyIssues)
        {
            var diagnosticDescriptor = GetDiagnosticDescriptorForPropertyIssue(propertyIssue.IssueType);
            if (diagnosticDescriptor == null) continue;

            var diagnostic = Diagnostic.Create(
                diagnosticDescriptor,
                adaptCallInfo.Location,
                propertyIssue.PropertyPath,
                propertyIssue.SourceType,
                propertyIssue.DestinationType);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static DiagnosticDescriptor? GetDiagnosticDescriptorForPropertyIssue(PropertyIssueType issueType)
    {
        return issueType switch
        {
            PropertyIssueType.NullabilityMismatch => DiagnosticDescriptors.PropertyNullableToNonNullableMapping,
            PropertyIssueType.TypeIncompatibility => DiagnosticDescriptors.PropertyIncompatibleTypeMapping,
            PropertyIssueType.MissingSourceProperty => DiagnosticDescriptors.PropertyMissingMapping,
            _ => null
        };
    }

    private class AdaptCallInfo
    {
        public AdaptCallInfo(ITypeSymbol sourceType, ITypeSymbol destinationType, Location location)
        {
            SourceType = sourceType;
            DestinationType = destinationType;
            Location = location;
        }

        public ITypeSymbol SourceType { get; }
        public ITypeSymbol DestinationType { get; }
        public Location Location { get; }
    }
}