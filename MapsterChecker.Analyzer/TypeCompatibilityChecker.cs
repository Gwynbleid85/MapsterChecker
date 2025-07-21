using Microsoft.CodeAnalysis;
using System.Linq;

namespace MapsterChecker.Analyzer;

public class TypeCompatibilityChecker
{
    private readonly SemanticModel _semanticModel;

    public TypeCompatibilityChecker(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    public TypeCompatibilityResult CheckCompatibility(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var result = new TypeCompatibilityResult();

        CheckNullabilityCompatibility(sourceType, destinationType, result);
        CheckTypeCompatibility(sourceType, destinationType, result);

        return result;
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

    private bool HasImplicitConversion(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var conversion = _semanticModel.Compilation.HasImplicitConversion(sourceType, destinationType);
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

    private class NullabilityInfo
    {
        public NullabilityInfo(bool canBeNull, bool isExplicitlyNullable)
        {
            CanBeNull = canBeNull;
            IsExplicitlyNullable = isExplicitlyNullable;
        }

        public bool CanBeNull { get; }
        public bool IsExplicitlyNullable { get; }
    }
}

public class TypeCompatibilityResult
{
    public bool HasNullabilityIssue { get; set; }
    public bool HasIncompatibilityIssue { get; set; }
    public string? NullabilityIssueDescription { get; set; }
    public string? IncompatibilityIssueDescription { get; set; }

    public bool HasAnyIssue => HasNullabilityIssue || HasIncompatibilityIssue;
}