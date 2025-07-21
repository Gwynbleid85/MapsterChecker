using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Linq;

namespace MapsterChecker.Analyzer;

/// <summary>
/// Analyzes type compatibility between source and destination types for Mapster mapping operations.
/// Performs comprehensive checks including nullability, type compatibility, and property-level analysis.
/// </summary>
/// <param name="semanticModel">The semantic model used for type analysis and compilation context</param>
public class TypeCompatibilityChecker(SemanticModel semanticModel)
{
    /// <summary>
    /// Performs comprehensive compatibility analysis between source and destination types.
    /// Includes nullability checking, type compatibility, and recursive property analysis for complex types.
    /// </summary>
    /// <param name="sourceType">The source type being mapped from</param>
    /// <param name="destinationType">The destination type being mapped to</param>
    /// <returns>Complete compatibility analysis result with any issues found</returns>
    public TypeCompatibilityResult CheckCompatibility(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var result = new TypeCompatibilityResult();

        CheckNullabilityCompatibility(sourceType, destinationType, result);
        CheckTypeCompatibility(sourceType, destinationType, result);
        
        if (ShouldPerformPropertyAnalysis(sourceType, destinationType))
        {
            var propertyAnalyzer = new PropertyMappingAnalyzer(semanticModel);
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
    /// </summary>
    /// <param name="sourceType">The source type to check</param>
    /// <param name="destinationType">The destination type to check</param>
    /// <returns>True if both types are complex and warrant property analysis</returns>
    private bool ShouldPerformPropertyAnalysis(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        return IsComplexUserDefinedType(sourceType) && IsComplexUserDefinedType(destinationType);
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

        if ((sourceTypeName == "string" && destTypeName == "System.Guid") ||
            (sourceTypeName == "System.Guid" && destTypeName == "string"))
            return true;

        if (sourceType.TypeKind == TypeKind.Enum && destinationType.TypeKind == TypeKind.Enum)
            return false;

        if (sourceType.TypeKind == TypeKind.Enum || destinationType.TypeKind == TypeKind.Enum)
            return numericTypes.Contains(sourceTypeName) || numericTypes.Contains(destTypeName);

        return false;
    }

    private bool AreReferenceTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        if (sourceType.TypeKind == TypeKind.Class && destinationType.TypeKind == TypeKind.Class)
            return true;

        if (sourceType.TypeKind == TypeKind.Interface || destinationType.TypeKind == TypeKind.Interface)
            return true;

        var sourceTypeName = sourceType.ToDisplayString();
        var destTypeName = destinationType.ToDisplayString();

        if (sourceTypeName == "string" || destTypeName == "string")
        {
            var stringCompatibleTypes = new[] { "string", "object", "System.IComparable", "System.IEnumerable" };
            return stringCompatibleTypes.Contains(sourceTypeName) || stringCompatibleTypes.Contains(destTypeName);
        }

        return false;
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