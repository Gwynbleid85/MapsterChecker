using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Linq;

namespace MapsterChecker.Analyzer;

/// <summary>
/// Discovers and parses Mapster TypeAdapterConfig configurations from source code.
/// Extracts mapping information and registers it with the MappingConfigurationRegistry.
/// </summary>
public class MapsterConfigurationDiscovery
{
    private readonly MappingConfigurationRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the MapsterConfigurationDiscovery class.
    /// </summary>
    /// <param name="registry">The registry to store discovered configurations</param>
    public MapsterConfigurationDiscovery(MappingConfigurationRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Analyzes an invocation expression to discover Mapster configuration calls.
    /// Called during syntax analysis phase to build the configuration registry.
    /// </summary>
    /// <param name="context">The syntax node analysis context</param>
    public void DiscoverConfiguration(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        if (!IsMapsterConfigurationCall(invocation, context.SemanticModel))
            return;

        var configInfo = ExtractConfigurationInfo(invocation, context.SemanticModel);
        if (configInfo != null)
        {
            ProcessConfigurationChain(invocation, configInfo, context.SemanticModel);
        }
    }

    /// <summary>
    /// Determines if the invocation is a Mapster configuration call.
    /// </summary>
    /// <param name="invocation">The invocation expression to check</param>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <returns>True if this is a TypeAdapterConfig call</returns>
    private bool IsMapsterConfigurationCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
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
    private bool IsTypeAdapterConfigMethod(IMethodSymbol methodSymbol)
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
    private bool IsMapsterFluentMethod(IMethodSymbol methodSymbol)
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

    /// <summary>
    /// Extracts configuration information from the initial TypeAdapterConfig call.
    /// </summary>
    private TypeConfigurationInfo? ExtractConfigurationInfo(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Look for TypeAdapterConfig<TSource, TDestination>.NewConfig() pattern
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is MemberAccessExpressionSyntax genericAccess)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(genericAccess);
            if (symbolInfo.Symbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var typeArgs = namedType.TypeArguments;
                if (typeArgs.Length == 2)
                {
                    return new TypeConfigurationInfo
                    {
                        SourceType = typeArgs[0],
                        DestinationType = typeArgs[1],
                        Location = invocation.GetLocation()
                    };
                }
            }
        }

        // Look for TypeAdapterConfig.GlobalSettings.NewConfig<TSource, TDestination>() pattern
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess2 &&
            memberAccess2.Name.Identifier.ValueText == "NewConfig")
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol && methodSymbol.IsGenericMethod)
            {
                var typeArgs = methodSymbol.TypeArguments;
                if (typeArgs.Length == 2)
                {
                    return new TypeConfigurationInfo
                    {
                        SourceType = typeArgs[0],
                        DestinationType = typeArgs[1],
                        Location = invocation.GetLocation()
                    };
                }
            }
        }

        // Try to extract from method chain context
        return TryExtractFromMethodChain(invocation, semanticModel);
    }

    /// <summary>
    /// Attempts to extract type information from method chain context.
    /// </summary>
    private TypeConfigurationInfo? TryExtractFromMethodChain(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol && methodSymbol.ContainingType != null)
        {
            var containingType = methodSymbol.ContainingType;
            if (containingType.IsGenericType && containingType.TypeArguments.Length == 2)
            {
                return new TypeConfigurationInfo
                {
                    SourceType = containingType.TypeArguments[0],
                    DestinationType = containingType.TypeArguments[1],
                    Location = invocation.GetLocation()
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Processes the entire configuration method chain starting from the initial call.
    /// </summary>
    private void ProcessConfigurationChain(InvocationExpressionSyntax initialCall, TypeConfigurationInfo configInfo, SemanticModel semanticModel)
    {
        // Find the complete statement that contains all chained calls
        var statement = initialCall.FirstAncestorOrSelf<StatementSyntax>();
        if (statement == null) return;

        // Process all invocations in the statement
        var invocations = statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            ProcessSingleConfigurationCall(invocation, configInfo, semanticModel);
        }
    }

    /// <summary>
    /// Processes a single configuration method call in the chain.
    /// </summary>
    private void ProcessSingleConfigurationCall(InvocationExpressionSyntax invocation, TypeConfigurationInfo configInfo, SemanticModel semanticModel)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.ValueText;

        switch (methodName)
        {
            case "Map":
                ProcessMapCall(invocation, configInfo, semanticModel);
                break;
            case "Ignore":
                ProcessIgnoreCall(invocation, configInfo, semanticModel);
                break;
            case "MapWith":
                ProcessMapWithCall(invocation, configInfo, semanticModel);
                break;
        }
    }

    /// <summary>
    /// Processes a .Map() configuration call.
    /// </summary>
    private void ProcessMapCall(InvocationExpressionSyntax invocation, TypeConfigurationInfo configInfo, SemanticModel semanticModel)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2) return;

        var destExpression = arguments[0].Expression;
        var sourceExpression = arguments[1].Expression;

        // Extract property name from destination expression
        var propertyName = ExtractPropertyName(destExpression);
        if (string.IsNullOrEmpty(propertyName)) return;

        var mappingInfo = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyMapping,
            PropertyName = propertyName,
            DestinationExpression = destExpression,
            SourceExpression = sourceExpression,
            SemanticModel = semanticModel,
            Location = invocation.GetLocation()
        };

        _registry.RegisterMapping(configInfo.SourceType, configInfo.DestinationType, mappingInfo);
    }

    /// <summary>
    /// Processes a .Ignore() configuration call.
    /// </summary>
    private void ProcessIgnoreCall(InvocationExpressionSyntax invocation, TypeConfigurationInfo configInfo, SemanticModel semanticModel)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 1) return;

        var destExpression = arguments[0].Expression;
        var propertyName = ExtractPropertyName(destExpression);
        if (string.IsNullOrEmpty(propertyName)) return;

        var mappingInfo = new CustomMappingInfo
        {
            MappingType = CustomMappingType.PropertyIgnore,
            PropertyName = propertyName,
            DestinationExpression = destExpression,
            SemanticModel = semanticModel,
            Location = invocation.GetLocation()
        };

        _registry.RegisterMapping(configInfo.SourceType, configInfo.DestinationType, mappingInfo);
    }

    /// <summary>
    /// Processes a .MapWith() configuration call.
    /// </summary>
    private void ProcessMapWithCall(InvocationExpressionSyntax invocation, TypeConfigurationInfo configInfo, SemanticModel semanticModel)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 1) return;

        var constructorExpression = arguments[0].Expression;

        var mappingInfo = new CustomMappingInfo
        {
            MappingType = CustomMappingType.ConstructorMapping,
            MappingExpression = constructorExpression,
            SemanticModel = semanticModel,
            Location = invocation.GetLocation()
        };

        _registry.RegisterMapping(configInfo.SourceType, configInfo.DestinationType, mappingInfo);
    }

    /// <summary>
    /// Extracts property name from a lambda expression like "dest => dest.PropertyName".
    /// </summary>
    private string? ExtractPropertyName(ExpressionSyntax expression)
    {
        if (expression is SimpleLambdaExpressionSyntax lambda &&
            lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Contains type information for a configuration chain.
    /// </summary>
    private class TypeConfigurationInfo
    {
        public ITypeSymbol SourceType { get; set; } = null!;
        public ITypeSymbol DestinationType { get; set; } = null!;
        public Location Location { get; set; } = null!;
    }
}