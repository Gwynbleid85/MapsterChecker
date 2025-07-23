using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MapsterChecker.Analyzer;

/// <summary>
/// Analyzes property-level mapping compatibility between source and destination types.
/// Performs recursive analysis to detect nullable to non-nullable mappings, type incompatibilities,
/// and missing property mappings at the property level.
/// Now supports custom mapping configurations to override default property analysis.
/// </summary>
/// <param name="semanticModel">The semantic model used for type analysis</param>
/// <param name="configurationRegistry">The registry containing custom mapping configurations</param>
public class PropertyMappingAnalyzer(SemanticModel semanticModel, MappingConfigurationRegistry? configurationRegistry = null)
{
    private readonly Dictionary<string, PropertyAnalysisResult> _analysisCache = new();
    private readonly HashSet<string> _currentAnalysisStack = new();
    private const int MaxRecursionDepth = 5;
    
    private ITypeSymbol? _rootSourceType;
    private ITypeSymbol? _rootDestinationType;

    /// <summary>
    /// Analyzes property mapping compatibility between source and destination types.
    /// Uses caching and circular reference detection for performance and safety.
    /// </summary>
    /// <param name="sourceType">The source type to analyze</param>
    /// <param name="destinationType">The destination type to analyze</param>
    /// <returns>Analysis result containing any property compatibility issues found</returns>
    public PropertyAnalysisResult AnalyzePropertyMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Set root types for custom mapping lookups
        _rootSourceType = sourceType;
        _rootDestinationType = destinationType;
        
        var cacheKey = $"{sourceType.ToDisplayString()}→{destinationType.ToDisplayString()}";
        
        // Return cached result if available
        if (_analysisCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        // Detect circular references to prevent infinite recursion
        if (_currentAnalysisStack.Contains(cacheKey))
        {
            return new PropertyAnalysisResult
            {
                HasCircularReference = true,
                Issues = ImmutableArray<PropertyCompatibilityIssue>.Empty
            };
        }

        _currentAnalysisStack.Add(cacheKey);
        
        try
        {
            var result = PerformPropertyAnalysis(sourceType, destinationType, "", 0);
            _analysisCache[cacheKey] = result;
            return result;
        }
        finally
        {
            _currentAnalysisStack.Remove(cacheKey);
        }
    }

    /// <summary>
    /// Performs the core property analysis logic recursively.
    /// Compares properties between source and destination types and identifies compatibility issues.
    /// </summary>
    /// <param name="sourceType">The source type to analyze</param>
    /// <param name="destinationType">The destination type to analyze</param>
    /// <param name="propertyPath">The current property path for nested objects (e.g., "Address.Street")</param>
    /// <param name="currentDepth">Current recursion depth to prevent infinite recursion</param>
    /// <returns>Analysis result with any property compatibility issues found</returns>
    private PropertyAnalysisResult PerformPropertyAnalysis(
        ITypeSymbol sourceType, 
        ITypeSymbol destinationType,
        string propertyPath,
        int currentDepth)
    {
        var issues = new List<PropertyCompatibilityIssue>();
        
        // Prevent infinite recursion by limiting depth
        if (currentDepth >= MaxRecursionDepth)
        {
            return new PropertyAnalysisResult
            {
                MaxDepthReached = true,
                Issues = issues.ToImmutableArray()
            };
        }

        // Only analyze complex user-defined types, not primitives
        if (!IsComplexType(sourceType) || !IsComplexType(destinationType))
        {
            return new PropertyAnalysisResult
            {
                Issues = issues.ToImmutableArray()
            };
        }

        var sourceProperties = GetMappableProperties(sourceType);
        var destinationProperties = GetMappableProperties(destinationType);

        // Analyze each destination property to find mapping issues
        foreach (var destProp in destinationProperties)
        {
            var sourceProp = FindMatchingProperty(sourceProperties, destProp);
            
            // Check for missing source properties
            if (sourceProp == null)
            {
                issues.Add(new PropertyCompatibilityIssue
                {
                    PropertyPath = CombinePropertyPath(propertyPath, destProp.Name),
                    SourceType = "missing",
                    DestinationType = destProp.Type.ToDisplayString(),
                    IssueType = PropertyIssueType.MissingSourceProperty,
                    Severity = DiagnosticSeverity.Info
                });
                continue;
            }

            var currentPropertyPath = CombinePropertyPath(propertyPath, destProp.Name);
            
            // Check for custom property mapping first (using root types for mapping lookup)
            if (configurationRegistry != null && _rootSourceType != null && _rootDestinationType != null && 
                configurationRegistry.HasPropertyMapping(_rootSourceType, _rootDestinationType, destProp.Name))
            {
                // Property has custom mapping, validate the custom expression instead and skip default validation
                var customMapping = configurationRegistry.GetPropertyMapping(_rootSourceType, _rootDestinationType, destProp.Name);
                if (customMapping != null)
                {
                    var customValidationResult = ValidateCustomPropertyMapping(customMapping, sourceProp, destProp, currentPropertyPath);
                    issues.AddRange(customValidationResult);
                }
                // Skip the rest of the property analysis - custom mapping handles it
                continue;
            }

            // Check direct property compatibility (nullable, type compatibility)
            var directCompatibilityResult = CheckDirectPropertyCompatibility(sourceProp, destProp, currentPropertyPath);
            issues.AddRange(directCompatibilityResult);

            // Recursively analyze nested complex types
            if (IsComplexType(sourceProp.Type) && IsComplexType(destProp.Type))
            {
                var nestedResult = PerformPropertyAnalysis(
                    sourceProp.Type, 
                    destProp.Type, 
                    currentPropertyPath, 
                    currentDepth + 1);
                
                issues.AddRange(nestedResult.Issues);
            }
        }

        return new PropertyAnalysisResult
        {
            Issues = issues.ToImmutableArray()
        };
    }

    /// <summary>
    /// Checks direct compatibility between two properties (nullability and type compatibility).
    /// Uses the existing TypeCompatibilityChecker to leverage established compatibility rules.
    /// Now checks for custom property mappings first.
    /// </summary>
    /// <param name="sourceProperty">The source property to check</param>
    /// <param name="destinationProperty">The destination property to check</param>
    /// <param name="propertyPath">The property path for reporting (e.g., "Address.Street")</param>
    /// <returns>List of compatibility issues found between the properties</returns>
    private List<PropertyCompatibilityIssue> CheckDirectPropertyCompatibility(
        IPropertySymbol sourceProperty, 
        IPropertySymbol destinationProperty,
        string propertyPath)
    {
        var issues = new List<PropertyCompatibilityIssue>();
        
        var typeChecker = new TypeCompatibilityChecker(semanticModel, configurationRegistry);
        var compatibilityResult = typeChecker.CheckCompatibility(sourceProperty.Type, destinationProperty.Type);

        // Check for nullable to non-nullable issues
        if (compatibilityResult.HasNullabilityIssue)
        {
            issues.Add(new PropertyCompatibilityIssue
            {
                PropertyPath = propertyPath,
                SourceType = sourceProperty.Type.ToDisplayString(),
                DestinationType = destinationProperty.Type.ToDisplayString(),
                IssueType = PropertyIssueType.NullabilityMismatch,
                Severity = DiagnosticSeverity.Warning,
                Description = compatibilityResult.NullabilityIssueDescription
            });
        }

        // Check for fundamental type incompatibilities
        if (compatibilityResult.HasIncompatibilityIssue)
        {
            issues.Add(new PropertyCompatibilityIssue
            {
                PropertyPath = propertyPath,
                SourceType = sourceProperty.Type.ToDisplayString(),
                DestinationType = destinationProperty.Type.ToDisplayString(),
                IssueType = PropertyIssueType.TypeIncompatibility,
                Severity = DiagnosticSeverity.Error,
                Description = compatibilityResult.IncompatibilityIssueDescription
            });
        }

        return issues;
    }

    /// <summary>
    /// Validates a custom property mapping configuration and checks for potential issues.
    /// Analyzes the custom mapping expression for safety and correctness.
    /// </summary>
    /// <param name="customMapping">The custom mapping configuration to validate</param>
    /// <param name="sourceProperty">The source property being mapped from</param>
    /// <param name="destinationProperty">The destination property being mapped to</param>
    /// <param name="propertyPath">The property path for error reporting</param>
    /// <returns>List of issues found in the custom mapping</returns>
    private List<PropertyCompatibilityIssue> ValidateCustomPropertyMapping(
        CustomMappingInfo customMapping, 
        IPropertySymbol sourceProperty, 
        IPropertySymbol destinationProperty, 
        string propertyPath)
    {
        var issues = new List<PropertyCompatibilityIssue>();

        if (customMapping.MappingType == CustomMappingType.PropertyIgnore)
        {
            // Ignored properties don't need validation, they're intentionally skipped
            return issues;
        }

        if (customMapping.SourceExpression == null || customMapping.SemanticModel == null)
        {
            return issues;
        }

        // For custom mappings, only report warnings about potentially dangerous expressions
        // but don't report type incompatibility errors (since the custom mapping is intended to handle that)
        
        // Check for dangerous method calls in the custom expression
        var dangerousMethod = CheckForDangerousMethodCalls(customMapping.SourceExpression, customMapping.SemanticModel);
        if (!string.IsNullOrEmpty(dangerousMethod))
        {
            issues.Add(new PropertyCompatibilityIssue
            {
                PropertyPath = propertyPath,
                SourceType = propertyPath, // Use property path as the first parameter
                DestinationType = dangerousMethod!, // Use dangerous method as the second parameter
                IssueType = PropertyIssueType.CustomMappingDangerousExpression,
                Severity = DiagnosticSeverity.Warning,
                Description = $"Custom mapping expression uses '{dangerousMethod}' which may throw exceptions for invalid values"
            });
        }
        // Note: For custom mappings, we only check for dangerous method calls
        // We skip null reference checks since the developer has explicitly configured the mapping

        return issues;
    }

    /// <summary>
    /// Checks for potentially dangerous method calls in a custom mapping expression.
    /// </summary>
    /// <param name="expression">The expression to analyze</param>
    /// <param name="semanticModel">The semantic model for symbol resolution</param>
    /// <returns>Description of dangerous method found, or null if none</returns>
    private string? CheckForDangerousMethodCalls(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var invocations = expression.DescendantNodes().OfType<InvocationExpressionSyntax>();
        
        foreach (var invocation in invocations)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
            {
                var methodName = methodSymbol.Name;
                var containingType = methodSymbol.ContainingType?.ToDisplayString();

                // Known potentially dangerous methods that can throw exceptions
                if (methodName.Contains("Parse") && containingType != null && 
                    (containingType.StartsWith("System.") || containingType == "int" || containingType == "double"))
                {
                    return $"{containingType}.{methodName}";
                }

                if (methodName.Contains("Convert") && containingType == "System.Convert")
                {
                    return $"{containingType}.{methodName}";
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks for potential null reference issues in a custom mapping expression.
    /// </summary>
    /// <param name="expression">The expression to analyze</param>
    /// <param name="semanticModel">The semantic model for type analysis</param>
    /// <returns>Description of null reference risk found, or null if none</returns>
    private string? CheckForNullReferenceRisk(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        var memberAccesses = expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        
        foreach (var memberAccess in memberAccesses)
        {
            var typeInfo = semanticModel.GetTypeInfo(memberAccess.Expression);
            if (typeInfo.Type != null && CanBeNull(typeInfo.Type))
            {
                return memberAccess.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a type can potentially be null.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type can be null</returns>
    private bool CanBeNull(ITypeSymbol type)
    {
        return type.IsReferenceType || 
               (type is INamedTypeSymbol namedType && 
                namedType.IsGenericType && 
                namedType.ConstructedFrom.ToDisplayString() == "System.Nullable<T>");
    }

    /// <summary>
    /// Gets all public properties that can be mapped by Mapster.
    /// Filters to include only public, non-static properties with both getter and setter.
    /// </summary>
    /// <param name="type">The type to analyze for mappable properties</param>
    /// <returns>Array of properties that can be mapped</returns>
    private ImmutableArray<IPropertySymbol> GetMappableProperties(ITypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(prop => 
                prop.DeclaredAccessibility == Accessibility.Public &&
                !prop.IsStatic &&
                prop.GetMethod != null &&
                prop.SetMethod != null)
            .ToImmutableArray();
    }

    /// <summary>
    /// Finds a matching property in the source properties by name.
    /// Uses case-sensitive exact name matching (Mapster's default behavior).
    /// </summary>
    /// <param name="sourceProperties">Properties from the source type</param>
    /// <param name="destinationProperty">The destination property to find a match for</param>
    /// <returns>Matching source property or null if not found</returns>
    private IPropertySymbol? FindMatchingProperty(ImmutableArray<IPropertySymbol> sourceProperties, IPropertySymbol destinationProperty)
    {
        return sourceProperties.FirstOrDefault(prop => prop.Name == destinationProperty.Name);
    }

    /// <summary>
    /// Determines if a type is a complex user-defined type that should be analyzed recursively.
    /// Excludes primitives, system types, and other types that don't contain properties to map.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type should be analyzed for property mapping</returns>
    private bool IsComplexType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Class && 
               type.SpecialType == SpecialType.None &&
               !IsSystemType(type);
    }

    /// <summary>
    /// Determines if a type is a system type that should not be analyzed recursively.
    /// System types like DateTime, TimeSpan, etc. are treated as primitives for mapping purposes.
    /// </summary>
    /// <param name="type">The type to check</param>
    /// <returns>True if the type is a system type</returns>
    private bool IsSystemType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.StartsWith("System.") && 
               !typeName.StartsWith("System.Collections") &&
               typeName != "System.String";
    }

    /// <summary>
    /// Combines property paths for nested object analysis.
    /// Creates dot-separated paths like "Address.Street" for error reporting.
    /// </summary>
    /// <param name="basePath">The base property path (empty for root level)</param>
    /// <param name="propertyName">The property name to append</param>
    /// <returns>Combined property path for error reporting</returns>
    private string CombinePropertyPath(string basePath, string propertyName)
    {
        return string.IsNullOrEmpty(basePath) ? propertyName : $"{basePath}.{propertyName}";
    }
}

/// <summary>
/// Contains the results of property mapping analysis between source and destination types.
/// Includes any compatibility issues found and metadata about the analysis process.
/// </summary>
public class PropertyAnalysisResult
{
    /// <summary>
    /// Gets or sets the collection of property compatibility issues found during analysis.
    /// </summary>
    public ImmutableArray<PropertyCompatibilityIssue> Issues { get; set; } = ImmutableArray<PropertyCompatibilityIssue>.Empty;
    
    /// <summary>
    /// Gets or sets a value indicating whether a circular reference was detected during analysis.
    /// This prevents infinite recursion when types reference each other.
    /// </summary>
    public bool HasCircularReference { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the maximum recursion depth was reached.
    /// This prevents stack overflow when analyzing deeply nested object hierarchies.
    /// </summary>
    public bool MaxDepthReached { get; set; }
    
    /// <summary>
    /// Gets or sets the depth level reached during recursive analysis.
    /// Useful for understanding how deep the analysis went before completion or termination.
    /// </summary>
    public int AnalyzedDepth { get; set; }
}

/// <summary>
/// Represents a specific compatibility issue found between source and destination properties.
/// Contains detailed information about the issue type, affected properties, and diagnostic severity.
/// </summary>
public class PropertyCompatibilityIssue
{
    /// <summary>
    /// Gets or sets the property path where the issue was found.
    /// For nested objects, this includes the full path (e.g., "Address.Street").
    /// </summary>
    public string PropertyPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the string representation of the source property type.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the string representation of the destination property type.
    /// </summary>
    public string DestinationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the type of compatibility issue (nullability, type incompatibility, etc.).
    /// </summary>
    public PropertyIssueType IssueType { get; set; }
    
    /// <summary>
    /// Gets or sets the diagnostic severity level for this issue.
    /// Determines whether this appears as an error, warning, or info message.
    /// </summary>
    public DiagnosticSeverity Severity { get; set; }
    
    /// <summary>
    /// Gets or sets an optional detailed description of the compatibility issue.
    /// Provides additional context beyond the basic issue type.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Defines the types of property-level compatibility issues that can be detected
/// during Mapster mapping analysis.
/// </summary>
public enum PropertyIssueType
{
    /// <summary>
    /// Source property is nullable while destination property is non-nullable.
    /// This may result in null reference exceptions at runtime.
    /// </summary>
    NullabilityMismatch,
    
    /// <summary>
    /// Source and destination property types are fundamentally incompatible
    /// and cannot be automatically converted by Mapster.
    /// </summary>
    TypeIncompatibility,
    
    /// <summary>
    /// Destination property exists but has no corresponding source property.
    /// The destination property will be left with its default value.
    /// </summary>
    MissingSourceProperty,
    
    /// <summary>
    /// Source property exists but has no corresponding destination property.
    /// The source property value will be ignored during mapping.
    /// </summary>
    MissingDestinationProperty,
    
    /// <summary>
    /// Custom mapping expression contains dangerous method calls that may throw exceptions.
    /// This includes methods like int.Parse(), Convert.ToInt32(), etc.
    /// </summary>
    CustomMappingDangerousExpression
}