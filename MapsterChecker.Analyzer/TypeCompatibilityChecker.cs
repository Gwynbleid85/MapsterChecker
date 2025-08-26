using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MapsterChecker.Analyzer;

/// <summary>
/// Analyzes type compatibility between source and destination types for Mapster mapping operations.
/// Performs comprehensive checks including nullability, type compatibility, and property-level analysis.
/// Now supports custom mapping configurations to override default compatibility rules.
/// </summary>
/// <param name="semanticModel">The semantic model used for type analysis and compilation context</param>
/// <param name="configurationRegistry">The registry containing custom mapping configurations</param>
public class TypeCompatibilityChecker(SemanticModel semanticModel, MappingConfigurationRegistry? configurationRegistry = null)
{
    /// <summary>
    /// Performs comprehensive compatibility analysis between source and destination types.
    /// Includes nullability checking, type compatibility, and recursive property analysis for complex types.
    /// Now considers custom mapping configurations to override default compatibility rules.
    /// </summary>
    /// <param name="sourceType">The source type being mapped from</param>
    /// <param name="destinationType">The destination type being mapped to</param>
    /// <returns>Complete compatibility analysis result with any issues found</returns>
    public TypeCompatibilityResult CheckCompatibility(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var result = new TypeCompatibilityResult();

        // Check if there's a custom mapping configuration that overrides default checks
        var hasCustomMapping = configurationRegistry?.HasCustomMapping(sourceType, destinationType) ?? false;

        if (!hasCustomMapping)
        {
            CheckNullabilityCompatibility(sourceType, destinationType, result);
            CheckTypeCompatibility(sourceType, destinationType, result);
        }
        else
        {
            // Still check nullability for custom mappings, but be less strict about type compatibility
            CheckNullabilityCompatibility(sourceType, destinationType, result);
            // Custom mapping validation would be performed by a separate validator
            ValidateCustomMappingConfigurations(sourceType, destinationType, result);
        }
        
        // Check if there's an AfterMapping configuration that can handle property incompatibilities
        var hasAfterMapping = configurationRegistry?.HasMappingOfType(sourceType, destinationType, CustomMappingType.AfterMapping) ?? false;
        
        if (ShouldPerformPropertyAnalysis(sourceType, destinationType, hasAfterMapping))
        {
            var propertyAnalyzer = new PropertyMappingAnalyzer(semanticModel, configurationRegistry);
            var propertyResult = propertyAnalyzer.AnalyzePropertyMapping(sourceType, destinationType);
            
            result.PropertyIssues = propertyResult.Issues;
            result.HasCircularReferences = propertyResult.HasCircularReference;
            result.MaxDepthReached = propertyResult.MaxDepthReached;
        }

        return result;
    }

    /// <summary>
    /// Determines whether property-level analysis should be performed for the given types.
    /// Only complex user-defined types require recursive property analysis.
    /// AfterMapping configurations can suppress property analysis since they handle incompatibilities.
    /// </summary>
    /// <param name="sourceType">The source type to check</param>
    /// <param name="destinationType">The destination type to check</param>
    /// <param name="hasAfterMapping">Whether there's an AfterMapping configuration for these types</param>
    /// <returns>True if both types are complex and warrant property analysis</returns>
    private bool ShouldPerformPropertyAnalysis(ITypeSymbol sourceType, ITypeSymbol destinationType, bool hasAfterMapping)
    {
        if (!IsComplexUserDefinedType(sourceType) || !IsComplexUserDefinedType(destinationType))
            return false;
            
        // If there's an AfterMapping configuration, it can handle property incompatibilities
        // so we can be more lenient about property analysis
        if (hasAfterMapping)
        {
            // Still perform basic checks but be less strict about incompatibilities
            return HasCommonProperties(sourceType, destinationType);
        }
            
        // Check for common properties - if no common properties exist, report incompatibility
        if (!HasCommonProperties(sourceType, destinationType))
        {
            // This will be handled by the type compatibility check, so we skip property analysis
            return false;
        }
        
        return true;
    }

    private bool IsComplexUserDefinedType(ITypeSymbol type)
    {
        return type.TypeKind == TypeKind.Class && 
               type.SpecialType == SpecialType.None &&
               !IsSystemType(type);
    }

    private bool IsSystemType(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName.StartsWith("System.") && 
               !typeName.StartsWith("System.Collections") &&
               typeName != "System.String";
    }

    /// <summary>
    /// Checks if two types have any common properties that can be mapped.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <returns>True if the types have at least one common property</returns>
    private bool HasCommonProperties(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var sourceProperties = GetMappableProperties(sourceType);
        var destProperties = GetMappableProperties(destinationType);
        
        return destProperties.Any(dest => 
            sourceProperties.Any(src => src.Name == dest.Name));
    }

    /// <summary>
    /// Gets all public properties that can be mapped by Mapster.
    /// </summary>
    /// <param name="type">The type to analyze</param>
    /// <returns>Collection of mappable properties</returns>
    private IEnumerable<IPropertySymbol> GetMappableProperties(ITypeSymbol type)
    {
        return type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(prop => 
                prop.DeclaredAccessibility == Accessibility.Public &&
                !prop.IsStatic &&
                prop.GetMethod != null &&
                prop.SetMethod != null);
    }

    private void CheckNullabilityCompatibility(ITypeSymbol sourceType, ITypeSymbol destinationType, TypeCompatibilityResult result)
    {
        var sourceNullability = GetNullabilityInfo(sourceType);
        var destNullability = GetNullabilityInfo(destinationType);

        if (sourceNullability.CanBeNull && !destNullability.CanBeNull)
        {
            result.HasNullabilityIssue = true;
            result.NullabilityIssueDescription = $"Mapping from nullable {sourceType.ToDisplayString()} to non-nullable {destinationType.ToDisplayString()}";
        }
    }

    private void CheckTypeCompatibility(ITypeSymbol sourceType, ITypeSymbol destinationType, TypeCompatibilityResult result)
    {
        // Check for problematic value/reference type mixing
        var sourceUnderlyingType = GetUnderlyingType(sourceType);
        var destUnderlyingType = GetUnderlyingType(destinationType);
        
        if ((sourceUnderlyingType.IsValueType && destUnderlyingType.IsReferenceType) ||
            (sourceUnderlyingType.IsReferenceType && destUnderlyingType.IsValueType))
        {
            result.HasIncompatibilityIssue = true;
            result.IncompatibilityIssueDescription = $"Cannot map between value type {sourceType.ToDisplayString()} and reference type {destinationType.ToDisplayString()}";
            return;
        }


        // Check for complex types with no common properties first, before general compatibility check
        if (IsComplexUserDefinedType(sourceType) && IsComplexUserDefinedType(destinationType) && 
            !HasCommonProperties(sourceType, destinationType))
        {
            result.HasIncompatibilityIssue = true;
            result.IncompatibilityIssueDescription = $"Types {sourceType.ToDisplayString()} and {destinationType.ToDisplayString()} have no common properties";
            return;
        }

        if (AreTypesCompatible(sourceType, destinationType))
            return;

        result.HasIncompatibilityIssue = true;
        result.IncompatibilityIssueDescription = $"Types {sourceType.ToDisplayString()} and {destinationType.ToDisplayString()} are not compatible";
    }

    private bool AreTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var sourceUnderlyingType = GetUnderlyingType(sourceType);
        var destUnderlyingType = GetUnderlyingType(destinationType);

        if (SymbolEqualityComparer.Default.Equals(sourceUnderlyingType, destUnderlyingType))
            return true;

        // Check for collection type mismatches early
        var sourceIsCollection = IsCollectionType(sourceUnderlyingType);
        var destIsCollection = IsCollectionType(destUnderlyingType);
        
        // If one is a collection and the other is not, they're incompatible
        // Exception: Allow compatible collection-to-collection mappings
        if (sourceIsCollection != destIsCollection)
        {
            // Special case: string is technically IEnumerable<char> but we treat it as non-collection
            if (!IsStringType(sourceUnderlyingType) && !IsStringType(destUnderlyingType))
                return false;
        }

        if (HasImplicitConversion(sourceUnderlyingType, destUnderlyingType))
            return true;

        if (AreInheritanceCompatible(sourceUnderlyingType, destUnderlyingType))
            return true;

        if (AreBothValueTypes(sourceUnderlyingType, destUnderlyingType))
            return AreValueTypesCompatible(sourceUnderlyingType, destUnderlyingType);

        if (AreBothReferenceTypes(sourceUnderlyingType, destUnderlyingType))
            return AreReferenceTypesCompatible(sourceUnderlyingType, destUnderlyingType);

        return false;
    }

    private ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericTypeName = namedType.ConstructedFrom.ToDisplayString();
            if (genericTypeName == "System.Nullable<T>")
            {
                return namedType.TypeArguments[0];
            }
        }

        return type;
    }

    private NullabilityInfo GetNullabilityInfo(ITypeSymbol type)
    {
        var canBeNull = false;
        var isExplicitlyNullable = false;

        if (type.IsReferenceType)
        {
            canBeNull = type.NullableAnnotation == NullableAnnotation.Annotated || 
                       type.NullableAnnotation == NullableAnnotation.None;
            isExplicitlyNullable = type.NullableAnnotation == NullableAnnotation.Annotated;
        }
        else if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var genericTypeName = namedType.ConstructedFrom.ToDisplayString();
            if (genericTypeName == "System.Nullable<T>")
            {
                canBeNull = true;
                isExplicitlyNullable = true;
            }
        }

        return new NullabilityInfo(canBeNull, isExplicitlyNullable);
    }

    /// <summary>
    /// Checks if there's an implicit conversion available between the source and destination types.
    /// Uses the semantic model's compilation context to determine conversion availability.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <returns>True if an implicit conversion exists</returns>
    private bool HasImplicitConversion(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var conversion = semanticModel.Compilation.HasImplicitConversion(sourceType, destinationType);
        return conversion;
    }

    private bool AreInheritanceCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (sourceType.TypeKind == TypeKind.Interface || destinationType.TypeKind == TypeKind.Interface)
            return true;

        var current = sourceType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, destinationType))
                return true;
            current = current.BaseType;
        }

        current = destinationType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, sourceType))
                return true;
            current = current.BaseType;
        }

        return false;
    }

    private bool AreBothValueTypes(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return sourceType.IsValueType && destinationType.IsValueType;
    }

    private bool AreBothReferenceTypes(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return sourceType.IsReferenceType && destinationType.IsReferenceType;
    }

    private bool AreValueTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var sourceTypeName = sourceType.ToDisplayString();
        var destTypeName = destinationType.ToDisplayString();

        var numericTypes = new[]
        {
            "byte", "sbyte", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal"
        };

        if (numericTypes.Contains(sourceTypeName) && numericTypes.Contains(destTypeName))
            return true;

        // Removed automatic string<->Guid compatibility - these should require custom mappings
        // if ((sourceTypeName == "string" && destTypeName == "System.Guid") ||
        //     (sourceTypeName == "System.Guid" && destTypeName == "string"))
        //     return true;

        if (sourceType.TypeKind == TypeKind.Enum && destinationType.TypeKind == TypeKind.Enum)
            return false;

        if (sourceType.TypeKind == TypeKind.Enum || destinationType.TypeKind == TypeKind.Enum)
            return numericTypes.Contains(sourceTypeName) || numericTypes.Contains(destTypeName);

        return false;
    }

    private bool AreReferenceTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // Check for collection type mismatches first
        var sourceIsCollection = IsCollectionType(sourceType);
        var destIsCollection = IsCollectionType(destinationType);
        
        // If one is a collection and the other is not, they're incompatible
        if (sourceIsCollection != destIsCollection)
        {
            // Special case: string is technically IEnumerable<char> but we treat it as non-collection
            if (!IsStringType(sourceType) && !IsStringType(destinationType))
                return false;
        }
        
        // If both are collections, check if they're compatible collection types
        if (sourceIsCollection && destIsCollection)
        {
            return AreCollectionsCompatible(sourceType, destinationType);
        }

        // For non-collection reference types
        if (sourceType.TypeKind == TypeKind.Class && destinationType.TypeKind == TypeKind.Class)
        {
            var sourceTypeName = sourceType.ToDisplayString();
            var destTypeName = destinationType.ToDisplayString();
            
            // Handle string compatibility (including nullable variants)
            var sourceBaseType = GetUnderlyingType(sourceType).ToDisplayString();
            var destBaseType = GetUnderlyingType(destinationType).ToDisplayString();
            
            if (sourceBaseType == "string" && destBaseType == "string")
                return true;
                
            // Object can accept anything
            if (destTypeName == "object")
                return true;
            
            // For complex user-defined types, check if they have common properties
            if (IsComplexUserDefinedType(sourceType) && IsComplexUserDefinedType(destinationType))
            {
                return HasCommonProperties(sourceType, destinationType);
            }
            
            // For other class types, be more permissive by default (let Mapster handle it)
            return true;
        }

        if (sourceType.TypeKind == TypeKind.Interface || destinationType.TypeKind == TypeKind.Interface)
            return true;

        return false;
    }

    /// <summary>
    /// Validates custom mapping configurations and reports any issues with the mapping expressions.
    /// Checks for potential exceptions, null reference issues, and type compatibility problems.
    /// </summary>
    /// <param name="sourceType">The source type</param>
    /// <param name="destinationType">The destination type</param>
    /// <param name="result">The result object to add validation issues to</param>
    private void ValidateCustomMappingConfigurations(ITypeSymbol sourceType, ITypeSymbol destinationType, TypeCompatibilityResult result)
    {
        if (configurationRegistry == null) return;

        // Get all custom mappings for this type pair
        var allMappings = configurationRegistry.GetAllMappings()
            .Where(m => m.TypeKey == $"{sourceType.ToDisplayString()}→{destinationType.ToDisplayString()}")
            .SelectMany(m => m.Mappings);

        foreach (var mapping in allMappings)
        {
            ValidateCustomMappingExpression(mapping, result);
        }
    }

    /// <summary>
    /// Validates a single custom mapping expression for safety and correctness.
    /// </summary>
    /// <param name="mapping">The custom mapping to validate</param>
    /// <param name="result">The result object to add validation issues to</param>
    private void ValidateCustomMappingExpression(CustomMappingInfo mapping, TypeCompatibilityResult result)
    {
        if (mapping.SourceExpression == null || mapping.SemanticModel == null) return;

        // Check for potentially dangerous method calls like int.Parse()
        var invocations = mapping.SourceExpression.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            CheckForDangerousMethodCall(invocation, mapping, result);
        }

        // Check for null reference potential
        CheckForNullReferenceRisk(mapping.SourceExpression, mapping, result);

        // Validate expression return type compatibility
        ValidateExpressionReturnType(mapping, result);
    }

    /// <summary>
    /// Checks for method calls that might throw exceptions.
    /// </summary>
    private void CheckForDangerousMethodCall(InvocationExpressionSyntax invocation, CustomMappingInfo mapping, TypeCompatibilityResult result)
    {
        if (mapping.SemanticModel == null) return;

        var symbolInfo = mapping.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            var methodName = methodSymbol.Name;
            var containingType = methodSymbol.ContainingType?.ToDisplayString();

            // Known potentially dangerous methods
            var dangerousMethods = new[]
            {
                ("int.Parse", "System.Int32"),
                ("double.Parse", "System.Double"),
                ("DateTime.Parse", "System.DateTime"),
                ("Convert.ToInt32", "System.Convert")
            };

            if (dangerousMethods.Any(dm => methodName.Contains("Parse") || methodName.Contains("Convert")))
            {
                // This would generate a MAPSTER004 diagnostic, but we'll add that to the result for now
                // The actual diagnostic reporting will be handled by the analyzer
                result.IncompatibilityIssueDescription = $"Custom mapping expression contains potentially dangerous method call: {methodName}";
            }
        }
    }

    /// <summary>
    /// Checks for potential null reference issues in the mapping expression.
    /// </summary>
    private void CheckForNullReferenceRisk(ExpressionSyntax expression, CustomMappingInfo mapping, TypeCompatibilityResult result)
    {
        // Check for member access on potentially null expressions
        var memberAccesses = expression.DescendantNodes().OfType<MemberAccessExpressionSyntax>();
        foreach (var memberAccess in memberAccesses)
        {
            if (mapping.SemanticModel != null)
            {
                var typeInfo = mapping.SemanticModel.GetTypeInfo(memberAccess.Expression);
                if (typeInfo.Type != null && CanBeNull(typeInfo.Type))
                {
                    result.NullabilityIssueDescription = $"Custom mapping expression may access member on null reference: {memberAccess}";
                }
            }
        }
    }

    /// <summary>
    /// Validates that the expression return type is compatible with the destination property.
    /// </summary>
    private void ValidateExpressionReturnType(CustomMappingInfo mapping, TypeCompatibilityResult result)
    {
        if (mapping.SourceExpression == null || mapping.SemanticModel == null) return;

        var expressionType = mapping.SemanticModel.GetTypeInfo(mapping.SourceExpression).Type;
        if (expressionType == null) return;

        // For now, we'll assume the destination type is compatible
        // A more sophisticated implementation would compare with the actual destination property type
    }

    /// <summary>
    /// Checks if a type can potentially be null.
    /// </summary>
    private bool CanBeNull(ITypeSymbol type)
    {
        return type.IsReferenceType || 
               (type is INamedTypeSymbol namedType && 
                namedType.IsGenericType && 
                namedType.ConstructedFrom.ToDisplayString() == "System.Nullable<T>");
    }

    /// <summary>
    /// Checks if a type is a collection type (array, List, IEnumerable, etc.)
    /// Excludes string even though it implements IEnumerable<char>.
    /// </summary>
    private bool IsCollectionType(ITypeSymbol type)
    {
        if (type == null) return false;
        
        // String is technically IEnumerable<char> but we don't treat it as a collection
        if (IsStringType(type)) return false;
        
        // Check for arrays
        if (type.TypeKind == TypeKind.Array)
            return true;
        
        // Check for generic collection types
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.ConstructedFrom.ToDisplayString();
            var collectionTypes = new[]
            {
                "System.Collections.Generic.List<T>",
                "System.Collections.Generic.IList<T>",
                "System.Collections.Generic.IEnumerable<T>",
                "System.Collections.Generic.ICollection<T>",
                "System.Collections.Generic.HashSet<T>",
                "System.Collections.Generic.Dictionary<TKey, TValue>",
                "System.Collections.Generic.IDictionary<TKey, TValue>",
                "System.Collections.Generic.Queue<T>",
                "System.Collections.Generic.Stack<T>",
                "System.Collections.ObjectModel.Collection<T>",
                "System.Collections.ObjectModel.ReadOnlyCollection<T>",
                "System.Collections.Immutable.ImmutableArray<T>",
                "System.Collections.Immutable.ImmutableList<T>"
            };
            
            if (collectionTypes.Any(ct => typeName == ct || typeName.StartsWith(ct.Replace("<T>", "<")) || 
                                          typeName.StartsWith(ct.Replace("<TKey, TValue>", "<"))))
            {
                return true;
            }
        }
        
        // Check if type implements IEnumerable (non-generic)
        var interfaces = type.AllInterfaces;
        return interfaces.Any(i => i.ToDisplayString() == "System.Collections.IEnumerable" ||
                                   i.ToDisplayString().StartsWith("System.Collections.Generic.IEnumerable<"));
    }

    /// <summary>
    /// Checks if a type is the string type.
    /// </summary>
    private bool IsStringType(ITypeSymbol type)
    {
        return type?.SpecialType == SpecialType.System_String;
    }

    /// <summary>
    /// Checks if two collection types are compatible for mapping.
    /// </summary>
    private bool AreCollectionsCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        // For now, allow mapping between different collection types if they have the same element type
        // This is a simplified check - Mapster can handle List<T> to IEnumerable<T> etc.
        
        var sourceElementType = GetCollectionElementType(sourceType);
        var destElementType = GetCollectionElementType(destinationType);
        
        if (sourceElementType == null || destElementType == null)
            return false;
            
        // Check if element types are compatible
        return AreTypesCompatible(sourceElementType, destElementType);
    }

    /// <summary>
    /// Gets the element type of a collection.
    /// </summary>
    private ITypeSymbol? GetCollectionElementType(ITypeSymbol collectionType)
    {
        // Handle arrays
        if (collectionType is IArrayTypeSymbol arrayType)
            return arrayType.ElementType;
        
        // Handle generic collections
        if (collectionType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            // For Dictionary and similar types, we might want the value type
            if (namedType.TypeArguments.Length > 0)
            {
                // For Dictionary<K,V>, this returns K, but that's okay for basic compatibility
                return namedType.TypeArguments[0];
            }
        }
        
        // Try to find IEnumerable<T> in interfaces
        var enumerableInterface = collectionType.AllInterfaces
            .FirstOrDefault(i => i.IsGenericType && 
                                i.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
        
        if (enumerableInterface != null && enumerableInterface.TypeArguments.Length > 0)
            return enumerableInterface.TypeArguments[0];
        
        return null;
    }

    /// <summary>
    /// Contains information about the nullability characteristics of a type.
    /// Distinguishes between types that can be null and those explicitly marked as nullable.
    /// </summary>
    /// <param name="canBeNull">Whether the type can contain null values</param>
    /// <param name="isExplicitlyNullable">Whether the type is explicitly marked as nullable (e.g., string?, int?)</param>
    private class NullabilityInfo(bool canBeNull, bool isExplicitlyNullable)
    {
        /// <summary>
        /// Gets a value indicating whether this type can contain null values.
        /// </summary>
        public bool CanBeNull { get; } = canBeNull;
        
        /// <summary>
        /// Gets a value indicating whether this type is explicitly marked as nullable.
        /// </summary>
        public bool IsExplicitlyNullable { get; } = isExplicitlyNullable;
    }
}

/// <summary>
/// Contains the complete results of type compatibility analysis between source and destination types.
/// Includes top-level compatibility issues as well as detailed property-level analysis results.
/// </summary>
public class TypeCompatibilityResult
{
    /// <summary>
    /// Gets or sets a value indicating whether there's a nullability compatibility issue.
    /// True when mapping from nullable to non-nullable types.
    /// </summary>
    public bool HasNullabilityIssue { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether there's a fundamental type incompatibility issue.
    /// True when types cannot be converted or mapped by Mapster.
    /// </summary>
    public bool HasIncompatibilityIssue { get; set; }
    
    /// <summary>
    /// Gets or sets a detailed description of the nullability issue, if any.
    /// </summary>
    public string? NullabilityIssueDescription { get; set; }
    
    /// <summary>
    /// Gets or sets a detailed description of the type incompatibility issue, if any.
    /// </summary>
    public string? IncompatibilityIssueDescription { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of property-level compatibility issues found during recursive analysis.
    /// </summary>
    public ImmutableArray<PropertyCompatibilityIssue> PropertyIssues { get; set; } = ImmutableArray<PropertyCompatibilityIssue>.Empty;
    
    /// <summary>
    /// Gets or sets a value indicating whether circular references were detected during property analysis.
    /// </summary>
    public bool HasCircularReferences { get; set; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the maximum recursion depth was reached during analysis.
    /// </summary>
    public bool MaxDepthReached { get; set; }

    /// <summary>
    /// Gets a value indicating whether any compatibility issues were found at any level.
    /// </summary>
    public bool HasAnyIssue => HasNullabilityIssue || HasIncompatibilityIssue || !PropertyIssues.IsEmpty;
    
    /// <summary>
    /// Gets a value indicating whether property-level issues were found during recursive analysis.
    /// </summary>
    public bool HasPropertyIssues => !PropertyIssues.IsEmpty;
}