using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MapsterChecker.Analyzer;

public class PropertyMappingAnalyzer
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, PropertyAnalysisResult> _analysisCache;
    private readonly HashSet<string> _currentAnalysisStack;
    private const int MaxRecursionDepth = 5;

    public PropertyMappingAnalyzer(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
        _analysisCache = new Dictionary<string, PropertyAnalysisResult>();
        _currentAnalysisStack = new HashSet<string>();
    }

    public PropertyAnalysisResult AnalyzePropertyMapping(ITypeSymbol sourceType, ITypeSymbol destinationType)
    {
        var cacheKey = $"{sourceType.ToDisplayString()}→{destinationType.ToDisplayString()}";
        
        if (_analysisCache.TryGetValue(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

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

    private PropertyAnalysisResult PerformPropertyAnalysis(
        ITypeSymbol sourceType, 
        ITypeSymbol destinationType,
        string propertyPath,
        int currentDepth)
    {
        var issues = new List<PropertyCompatibilityIssue>();
        
        if (currentDepth >= MaxRecursionDepth)
        {
            return new PropertyAnalysisResult
            {
                MaxDepthReached = true,
                Issues = issues.ToImmutableArray()
            };
        }

        if (!IsComplexType(sourceType) || !IsComplexType(destinationType))
        {
            return new PropertyAnalysisResult
            {
                Issues = issues.ToImmutableArray()
            };
        }

        var sourceProperties = GetMappableProperties(sourceType);
        var destinationProperties = GetMappableProperties(destinationType);

        foreach (var destProp in destinationProperties)
        {
            var sourceProp = FindMatchingProperty(sourceProperties, destProp);
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
            
            var directCompatibilityResult = CheckDirectPropertyCompatibility(sourceProp, destProp, currentPropertyPath);
            issues.AddRange(directCompatibilityResult);

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

    private List<PropertyCompatibilityIssue> CheckDirectPropertyCompatibility(
        IPropertySymbol sourceProperty, 
        IPropertySymbol destinationProperty,
        string propertyPath)
    {
        var issues = new List<PropertyCompatibilityIssue>();
        var typeChecker = new TypeCompatibilityChecker(_semanticModel);
        var compatibilityResult = typeChecker.CheckCompatibility(sourceProperty.Type, destinationProperty.Type);

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

    private IPropertySymbol? FindMatchingProperty(ImmutableArray<IPropertySymbol> sourceProperties, IPropertySymbol destinationProperty)
    {
        return sourceProperties.FirstOrDefault(prop => prop.Name == destinationProperty.Name);
    }

    private bool IsComplexType(ITypeSymbol type)
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

    private string CombinePropertyPath(string basePath, string propertyName)
    {
        return string.IsNullOrEmpty(basePath) ? propertyName : $"{basePath}.{propertyName}";
    }
}

public class PropertyAnalysisResult
{
    public ImmutableArray<PropertyCompatibilityIssue> Issues { get; set; } = ImmutableArray<PropertyCompatibilityIssue>.Empty;
    public bool HasCircularReference { get; set; }
    public bool MaxDepthReached { get; set; }
    public int AnalyzedDepth { get; set; }
}

public class PropertyCompatibilityIssue
{
    public string PropertyPath { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string DestinationType { get; set; } = string.Empty;
    public PropertyIssueType IssueType { get; set; }
    public DiagnosticSeverity Severity { get; set; }
    public string? Description { get; set; }
}

public enum PropertyIssueType
{
    NullabilityMismatch,
    TypeIncompatibility,
    MissingSourceProperty,
    MissingDestinationProperty
}