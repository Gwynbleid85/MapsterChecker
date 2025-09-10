using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MapsterChecker.Analyzer;

/// <summary>
/// Roslyn diagnostic analyzer that detects compatibility issues in Mapster.Adapt method calls.
/// Analyzes both top-level type compatibility and recursive property-level compatibility.
/// </summary>
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
            DiagnosticDescriptors.PropertyMissingMapping,
            DiagnosticDescriptors.CustomMappingExpressionException,
            DiagnosticDescriptors.CustomMappingReturnTypeIncompatible,
            DiagnosticDescriptors.CustomMappingNullValue);

    /// <summary>
    /// Initializes the analyzer by registering for compilation-level analysis.
    /// Configures the analyzer to skip generated code and enable concurrent execution.
    /// Uses compilation-level context to ensure configuration discovery and analysis share state.
    /// </summary>
    /// <param name="context">The analysis context to configure</param>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        // Use compilation-level analysis to ensure proper state sharing
        context.RegisterCompilationAction(compilationContext =>
        {
            var registry = new MappingConfigurationRegistry();
            var discovery = new MapsterConfigurationDiscovery(registry);
            
            // Collect all invocations from all syntax trees first
            var allInvocations = new List<(InvocationExpressionSyntax Invocation, SemanticModel SemanticModel)>();
            
            foreach (var syntaxTree in compilationContext.Compilation.SyntaxTrees)
            {
#pragma warning disable RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer
                var semanticModel = compilationContext.Compilation.GetSemanticModel(syntaxTree);
#pragma warning restore RS1030
                var root = syntaxTree.GetRoot(compilationContext.CancellationToken);
                
                foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    allInvocations.Add((invocation, semanticModel));
                }
            }
            
            // First pass: discover all configurations across all invocations
            // Process configuration calls first to ensure registry is fully populated
            foreach (var (invocation, semanticModel) in allInvocations)
            {
                var mockContext = new SyntaxNodeAnalysisContext(
                    invocation,
                    semanticModel,
                    compilationContext.Options,
                    diagnostic => { }, // We're only discovering, not reporting diagnostics
                    _ => true,
                    compilationContext.CancellationToken);
                
                discovery.DiscoverConfiguration(mockContext);
            }
            
            // Second pass: analyze Adapt calls with the now-populated registry
            // Only process Adapt calls after all configurations have been discovered
            foreach (var (invocation, semanticModel) in allInvocations)
            {
                // Skip configuration calls in analysis phase
                if (IsMapsterConfigurationCall(invocation, semanticModel))
                    continue;
                    
                var diagnostics = new List<Diagnostic>();
                var mockContext = new SyntaxNodeAnalysisContext(
                    invocation,
                    semanticModel,
                    compilationContext.Options,
                    diagnostic => diagnostics.Add(diagnostic),
                    _ => true,
                    compilationContext.CancellationToken);
                
                AnalyzeInvocation(mockContext, registry);
                
                // Report all diagnostics found
                foreach (var diagnostic in diagnostics)
                {
                    compilationContext.ReportDiagnostic(diagnostic);
                }
            }
        });
    }

    /// <summary>
    /// Analyzes invocation expressions to detect and validate Mapster.Adapt method calls.
    /// Performs comprehensive type compatibility analysis and reports any issues found.
    /// Now includes custom configuration awareness.
    /// </summary>
    /// <param name="context">The syntax node analysis context containing the invocation to analyze</param>
    /// <param name="registry">The mapping configuration registry containing custom mappings</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, MappingConfigurationRegistry registry)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        if (!IsMapsterAdaptCall(invocation, context.SemanticModel))
            return;

        var adaptCallInfo = ExtractAdaptCallInfo(invocation, context.SemanticModel);
        if (adaptCallInfo == null)
            return;

        var compatibilityChecker = new TypeCompatibilityChecker(context.SemanticModel, registry);
        var compatibilityResult = compatibilityChecker.CheckCompatibility(
            adaptCallInfo.SourceType, 
            adaptCallInfo.DestinationType,
            adaptCallInfo.OverriddenProperties);

        ReportDiagnostics(context, invocation, compatibilityResult, adaptCallInfo);
    }

    /// <summary>
    /// Determines whether the given invocation expression is a call to Mapster's Adapt method.
    /// Checks method name, containing type, and namespace to confirm it's a Mapster call.
    /// </summary>
    /// <param name="invocation">The invocation expression to check</param>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <returns>True if this is a Mapster.Adapt method call</returns>
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

    /// <summary>
    /// Determines whether the given invocation expression is a Mapster configuration call.
    /// Used to separate configuration discovery from analysis phases.
    /// </summary>
    /// <param name="invocation">The invocation expression to check</param>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <returns>True if this is a Mapster configuration method call</returns>
    private static bool IsMapsterConfigurationCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Check for TypeAdapterConfig<TSource, TDestination>.NewConfig() pattern
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name.Identifier.ValueText == "NewConfig")
            {
                var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
                {
                    return IsTypeAdapterConfigMethod(methodSymbol);
                }
            }
        }

        // Check for fluent API calls (.Map, .Ignore, etc.)
        var symbolInfo2 = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo2.Symbol is IMethodSymbol method)
        {
            return IsMapsterFluentMethod(method);
        }

        return false;
    }

    /// <summary>
    /// Checks if the method belongs to TypeAdapterConfig.
    /// </summary>
    private static bool IsTypeAdapterConfigMethod(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType;
        if (containingType == null) return false;

        var typeName = containingType.ToDisplayString();
        return typeName.StartsWith("Mapster.TypeAdapterConfig") || 
               typeName.StartsWith("TypeAdapterConfig");
    }

    /// <summary>
    /// Checks if the method is part of Mapster's fluent configuration API.
    /// </summary>
    private static bool IsMapsterFluentMethod(IMethodSymbol methodSymbol)
    {
        var containingType = methodSymbol.ContainingType;
        if (containingType == null) return false;

        var typeName = containingType.ToDisplayString();
        var methodName = methodSymbol.Name;

        // Check for methods that are part of configuration chains
        var configMethods = new[] { "Map", "Ignore", "MapWith", "TwoWays", "BeforeMapping", "AfterMapping" };
        
        return (typeName.Contains("TypeAdapterConfig") || typeName.Contains("TypeAdapterSetter")) &&
               configMethods.Contains(methodName);
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

        // Check if this Adapt call is part of a with expression
        var overriddenProperties = ExtractWithExpressionProperties(invocation, semanticModel);

        return new AdaptCallInfo(sourceType, destinationType, invocation.GetLocation(), overriddenProperties);
    }

    /// <summary>
    /// Extracts property names that are overridden in a with expression if the Adapt call is part of one.
    /// </summary>
    /// <param name="invocation">The Adapt invocation to check</param>
    /// <param name="semanticModel">The semantic model for analysis</param>
    /// <returns>Set of property names overridden in the with expression, or empty set if not a with expression</returns>
    private static HashSet<string> ExtractWithExpressionProperties(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var overriddenProperties = new HashSet<string>();

        // Check if the parent of the invocation is a with expression
        var parent = invocation.Parent;
        if (parent is WithExpressionSyntax withExpression && withExpression.Expression == invocation)
        {
            // Extract property names from the initializer
            if (withExpression.Initializer != null)
            {
                foreach (var expression in withExpression.Initializer.Expressions)
                {
                    if (expression is AssignmentExpressionSyntax assignment)
                    {
                        // Get the property name from the left side of the assignment
                        if (assignment.Left is IdentifierNameSyntax identifier)
                        {
                            overriddenProperties.Add(identifier.Identifier.ValueText);
                        }
                    }
                }
            }
        }

        return overriddenProperties;
    }

    /// <summary>
    /// Checks if the invocation has a null-forgiving operator (!) applied to it.
    /// When present, warnings should be suppressed but errors should still be reported.
    /// </summary>
    /// <param name="invocation">The invocation expression to check</param>
    /// <returns>True if the invocation has a null-forgiving operator</returns>
    private static bool HasNullForgivingOperator(InvocationExpressionSyntax invocation)
    {
        // Check if the parent is a PostfixUnaryExpression with SuppressNullableWarning kind
        var parent = invocation.Parent;
        return parent is PostfixUnaryExpressionSyntax postfixUnary &&
               postfixUnary.Kind() == SyntaxKind.SuppressNullableWarningExpression;
    }

    private static void ReportDiagnostics(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        TypeCompatibilityResult compatibilityResult,
        AdaptCallInfo adaptCallInfo)
    {
        // Check if null-forgiving operator is present - if so, suppress warnings
        var suppressWarnings = HasNullForgivingOperator(invocation);
        if (compatibilityResult.HasNullabilityIssue && !suppressWarnings)
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
            // Errors are always reported, even with null-forgiving operator
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.IncompatibleTypeMapping,
                adaptCallInfo.Location,
                adaptCallInfo.SourceType.ToDisplayString(),
                adaptCallInfo.DestinationType.ToDisplayString(),
                compatibilityResult.IncompatibilityIssueDescription ?? "Types are not compatible");

            context.ReportDiagnostic(diagnostic);
        }

        foreach (var propertyIssue in compatibilityResult.PropertyIssues)
        {
            var diagnosticDescriptor = GetDiagnosticDescriptorForPropertyIssue(propertyIssue.IssueType);
            if (diagnosticDescriptor == null) continue;

            // Skip warnings if null-forgiving operator is present
            if (suppressWarnings && diagnosticDescriptor.DefaultSeverity == DiagnosticSeverity.Warning)
                continue;

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
            PropertyIssueType.CustomMappingDangerousExpression => DiagnosticDescriptors.CustomMappingExpressionException,
            _ => null
        };
    }

    /// <summary>
    /// Contains information about a detected Mapster.Adapt method call.
    /// Encapsulates the source type, destination type, location, and any overridden properties from a with expression.
    /// </summary>
    /// <param name="sourceType">The source type being mapped from</param>
    /// <param name="destinationType">The destination type being mapped to</param>
    /// <param name="location">The source location of the Adapt call for diagnostic reporting</param>
    /// <param name="overriddenProperties">Properties overridden in a with expression, if any</param>
    private class AdaptCallInfo(ITypeSymbol sourceType, ITypeSymbol destinationType, Location location, HashSet<string>? overriddenProperties = null)
    {
        /// <summary>
        /// Gets the source type being mapped from.
        /// </summary>
        public ITypeSymbol SourceType { get; } = sourceType;
        
        /// <summary>
        /// Gets the destination type being mapped to.
        /// </summary>
        public ITypeSymbol DestinationType { get; } = destinationType;
        
        /// <summary>
        /// Gets the source location of the Adapt call for diagnostic reporting.
        /// </summary>
        public Location Location { get; } = location;
        
        /// <summary>
        /// Gets the set of property names that are overridden in a with expression.
        /// These properties should be excluded from compatibility checking.
        /// </summary>
        public HashSet<string> OverriddenProperties { get; } = overriddenProperties ?? new HashSet<string>();
    }
}