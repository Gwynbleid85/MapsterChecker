# Mapster Analyzer Deep Dive

## Overview

The Mapster Analyzer is a Roslyn-based diagnostic analyzer that performs static analysis of Mapster mapping operations in C# code. It detects potential runtime issues at compile time, such as nullable-to-non-nullable mappings, type incompatibilities, and missing property mappings.

## Architecture Overview

The analyzer follows a multi-component architecture:

```
┌─────────────────────────────────────────────────────────────────────┐
│                        MapsterAdaptAnalyzer                         │
│                    (Main Analyzer Entry Point)                     │
└─────────────────┬───────────────────────────────────────────────────┘
                  │
                  ├─── Discovers Configurations ──┐
                  │                                 │
                  └─── Analyzes Adapt Calls ───────┼─────────────────┐
                                                    │                 │
    ┌───────────────────────────────────────────────▼───────────────┐ │
    │            MapsterConfigurationDiscovery                      │ │
    │        (Discovers custom mapping configs)                     │ │
    └─────────────────────┬─────────────────────────────────────────┘ │
                          │                                           │
                          ▼                                           │
    ┌─────────────────────────────────────────────────────────────────┐ │
    │              MappingConfigurationRegistry                       │ │
    │           (Stores discovered configurations)                    │ │
    └─────────────────────┬─────────────────────────────────────────┘ │
                          │                                           │
                          └─── Provides Config Info ──────────────────┤
                                                                      │
    ┌─────────────────────────────────────────────────────────────────▼┐
    │                TypeCompatibilityChecker                          │
    │              (Performs type analysis)                            │
    └─────────────────────┬─────────────────────────────────────────┘
                          │
                          ▼
    ┌─────────────────────────────────────────────────────────────────┐
    │               PropertyMappingAnalyzer                           │
    │           (Analyzes property-level mappings)                    │
    └─────────────────────────────────────────────────────────────────┘
```

## Component Details

### 1. MapsterAdaptAnalyzer (Main Entry Point)

**Location**: `MapsterAdaptAnalyzer.cs`
**Purpose**: Main Roslyn analyzer that orchestrates the entire analysis process

#### Key Responsibilities:
- **Registration**: Registers with Roslyn's analysis pipeline via `Initialize()`
- **Two-Phase Analysis**: Implements a discovery phase followed by an analysis phase
- **Diagnostic Reporting**: Reports all found issues to the compiler

#### Analysis Flow:

```csharp
public override void Initialize(AnalysisContext context)
{
    // Phase 1: Collect all invocations from all syntax trees
    var allInvocations = CollectAllInvocations(compilation);
    
    // Phase 2: Discovery - find all custom configurations
    foreach (var (invocation, semanticModel) in allInvocations)
    {
        discovery.DiscoverConfiguration(invocation, semanticModel);
    }
    
    // Phase 3: Analysis - analyze Adapt calls with populated registry
    foreach (var (invocation, semanticModel) in allInvocations)
    {
        if (!IsMapsterConfigurationCall(invocation))
            AnalyzeInvocation(invocation, semanticModel, registry);
    }
}
```

#### Critical Design Decisions:

1. **Compilation-Level Analysis**: Uses `RegisterCompilationAction` instead of `RegisterSyntaxNodeAction` to ensure all configurations are discovered before any analysis
2. **Two-Phase Processing**: Separates configuration discovery from analysis to avoid timing issues
3. **Mock Context Creation**: Creates `SyntaxNodeAnalysisContext` instances manually to enable the two-phase approach

### 2. MapsterConfigurationDiscovery

**Location**: `MapsterConfigurationDiscovery.cs`
**Purpose**: Discovers and parses Mapster configuration calls in source code

#### Detection Patterns:

The discovery system recognizes several Mapster configuration patterns:

```csharp
// Pattern 1: Generic TypeAdapterConfig with fluent API
TypeAdapterConfig<Person, PersonDto>
    .NewConfig()
    .Map(dest => dest.Id, src => src.Id.ToString())
    .Ignore(dest => dest.InternalId);

// Pattern 2: GlobalSettings configuration
TypeAdapterConfig.GlobalSettings
    .NewConfig<Person, PersonDto>()
    .Map(dest => dest.Name, src => src.FullName);
```

#### Discovery Algorithm:

```csharp
public void DiscoverConfiguration(SyntaxNodeAnalysisContext context)
{
    var invocation = (InvocationExpressionSyntax)context.Node;
    
    // Step 1: Check if this is a configuration call
    if (!IsMapsterConfigurationCall(invocation, semanticModel))
        return;

    // Step 2: Extract type information (source/destination types)
    var configInfo = ExtractConfigurationInfo(invocation, semanticModel);
    
    // Step 3: Process the entire method chain
    ProcessConfigurationChain(invocation, configInfo, semanticModel);
}
```

#### Configuration Types Supported:

1. **Property Mapping**: `.Map(dest => dest.Property, src => expression)`
2. **Property Ignore**: `.Ignore(dest => dest.Property)`
3. **Constructor Mapping**: `.MapWith(src => new Destination(...))`
4. **Conditional Mapping**: `.Map(dest => dest.Property, src => expression, condition)`

#### Chain Processing:

The discovery system processes entire fluent API chains:

```csharp
private void ProcessConfigurationChain(InvocationExpressionSyntax initialCall, 
                                     TypeConfigurationInfo configInfo, 
                                     SemanticModel semanticModel)
{
    // Find the complete statement containing all chained calls
    var statement = initialCall.FirstAncestorOrSelf<StatementSyntax>();
    
    // Process all invocations in the statement
    var invocations = statement.DescendantNodes().OfType<InvocationExpressionSyntax>();
    
    foreach (var invocation in invocations)
    {
        ProcessSingleConfigurationCall(invocation, configInfo, semanticModel);
    }
}
```

### 3. MappingConfigurationRegistry

**Location**: `MappingConfigurationRegistry.cs`
**Purpose**: Centralized storage for discovered custom mapping configurations

#### Data Structure:

```csharp
public class MappingConfigurationRegistry
{
    // Type-level mappings: "SourceType→DestinationType" -> Set<CustomMappingInfo>
    private readonly Dictionary<string, HashSet<CustomMappingInfo>> _typeMappings;
    
    // Property-level mappings: "SourceType→DestinationType" -> PropertyName -> CustomMappingInfo
    private readonly Dictionary<string, Dictionary<string, CustomMappingInfo>> _propertyMappings;
}
```

#### Key Operations:

1. **Registration**: `RegisterMapping(sourceType, destType, mappingInfo)`
2. **Type-Level Lookup**: `HasCustomMapping(sourceType, destType)`
3. **Property-Level Lookup**: `HasPropertyMapping(sourceType, destType, propertyName)`
4. **Retrieval**: `GetPropertyMapping(sourceType, destType, propertyName)`

#### Custom Mapping Information:

```csharp
public class CustomMappingInfo
{
    public CustomMappingType MappingType { get; set; }    // Map, Ignore, MapWith, etc.
    public string? PropertyName { get; set; }             // For property-level mappings
    public ExpressionSyntax? MappingExpression { get; set; }     // The mapping expression
    public ExpressionSyntax? DestinationExpression { get; set; } // dest => dest.Property
    public ExpressionSyntax? SourceExpression { get; set; }      // src => src.Value
    public SemanticModel? SemanticModel { get; set; }     // For expression analysis
    public Location? Location { get; set; }               // For diagnostic reporting
}
```

### 4. TypeCompatibilityChecker

**Location**: `TypeCompatibilityChecker.cs`
**Purpose**: Performs type compatibility analysis between source and destination types

#### Compatibility Checks:

1. **Nullability Analysis**: Detects nullable-to-non-nullable mappings
2. **Type Conversion Analysis**: Identifies incompatible type mappings
3. **Custom Mapping Awareness**: Respects user-defined mappings
4. **Property-Level Analysis**: Recursive analysis for complex types

#### Analysis Algorithm:

```csharp
public TypeCompatibilityResult CheckCompatibility(ITypeSymbol sourceType, 
                                                ITypeSymbol destinationType)
{
    // Step 1: Check for custom type-level mapping
    if (registry?.HasCustomMapping(sourceType, destinationType) == true)
        return TypeCompatibilityResult.Compatible();
    
    // Step 2: Direct type compatibility
    var directResult = CheckDirectTypeCompatibility(sourceType, destinationType);
    
    // Step 3: Property-level analysis for complex types
    var propertyResult = propertyAnalyzer.AnalyzePropertyMapping(sourceType, destinationType);
    
    return CombineResults(directResult, propertyResult);
}
```

#### Nullability Analysis:

```csharp
private bool HasNullabilityIssue(ITypeSymbol sourceType, ITypeSymbol destinationType)
{
    // Check if source is nullable and destination is non-nullable
    var sourceNullable = IsNullableType(sourceType);
    var destNullable = IsNullableType(destinationType);
    
    return sourceNullable && !destNullable;
}
```

### 5. PropertyMappingAnalyzer

**Location**: `PropertyMappingAnalyzer.cs`
**Purpose**: Analyzes property-level mapping compatibility with deep recursive analysis

#### Key Features:

1. **Recursive Analysis**: Analyzes nested object properties
2. **Circular Reference Detection**: Prevents infinite recursion
3. **Caching**: Avoids redundant analysis of the same type pairs
4. **Custom Mapping Integration**: Respects property-level custom mappings

#### Analysis Process:

```csharp
public PropertyAnalysisResult AnalyzePropertyMapping(ITypeSymbol sourceType, 
                                                   ITypeSymbol destinationType)
{
    // Step 1: Check cache
    if (_analysisCache.TryGetValue(cacheKey, out var cachedResult))
        return cachedResult;
    
    // Step 2: Circular reference detection
    if (_currentAnalysisStack.Contains(cacheKey))
        return CircularReferenceResult();
    
    // Step 3: Perform analysis
    _currentAnalysisStack.Add(cacheKey);
    var result = PerformPropertyAnalysis(sourceType, destinationType, "", 0);
    _analysisCache[cacheKey] = result;
    _currentAnalysisStack.Remove(cacheKey);
    
    return result;
}
```

#### Property Matching Algorithm:

```csharp
private PropertyAnalysisResult PerformPropertyAnalysis(ITypeSymbol sourceType, 
                                                     ITypeSymbol destinationType,
                                                     string propertyPath,
                                                     int currentDepth)
{
    var issues = new List<PropertyCompatibilityIssue>();
    
    // Get mappable properties from both types
    var sourceProperties = GetMappableProperties(sourceType);
    var destinationProperties = GetMappableProperties(destinationType);
    
    foreach (var destProp in destinationProperties)
    {
        var sourceProp = FindMatchingProperty(sourceProperties, destProp);
        
        // Check for custom property mapping first
        if (HasCustomPropertyMapping(destProp.Name))
        {
            ValidateCustomMapping(destProp, sourceProp);
            continue; // Skip default validation
        }
        
        // Perform standard compatibility checks
        if (sourceProp == null)
            issues.Add(CreateMissingPropertyIssue(destProp));
        else
            issues.AddRange(CheckPropertyCompatibility(sourceProp, destProp));
        
        // Recursive analysis for complex types
        if (IsComplexType(sourceProp?.Type) && IsComplexType(destProp.Type))
            issues.AddRange(AnalyzeNestedProperties(sourceProp.Type, destProp.Type));
    }
    
    return new PropertyAnalysisResult { Issues = issues.ToImmutableArray() };
}
```

## Diagnostic System

### Diagnostic Categories

The analyzer reports several categories of diagnostics:

1. **MAPSTER001**: Top-level nullable-to-non-nullable mapping
2. **MAPSTER001P**: Property-level nullable-to-non-nullable mapping
3. **MAPSTER002**: Top-level type incompatibility
4. **MAPSTER002P**: Property-level type incompatibility
5. **MAPSTER003**: Missing property mapping
6. **MAPSTER004**: Custom mapping expression issues
7. **MAPSTER005**: Custom mapping return type incompatibility
8. **MAPSTER006**: Custom mapping null value issues

### Diagnostic Creation:

```csharp
private static void ReportDiagnostics(SyntaxNodeAnalysisContext context,
                                    InvocationExpressionSyntax invocation,
                                    TypeCompatibilityResult compatibilityResult,
                                    AdaptCallInfo adaptCallInfo)
{
    // Top-level nullability issues
    if (compatibilityResult.HasNullabilityIssue)
    {
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.NullableToNonNullableMapping,
            adaptCallInfo.Location,
            adaptCallInfo.SourceType.ToDisplayString(),
            adaptCallInfo.DestinationType.ToDisplayString());
        
        context.ReportDiagnostic(diagnostic);
    }
    
    // Property-level issues
    foreach (var propertyIssue in compatibilityResult.PropertyIssues)
    {
        var descriptor = GetDiagnosticDescriptorForPropertyIssue(propertyIssue.IssueType);
        var diagnostic = Diagnostic.Create(descriptor, adaptCallInfo.Location, 
                                         propertyIssue.PropertyPath,
                                         propertyIssue.SourceType,
                                         propertyIssue.DestinationType);
        context.ReportDiagnostic(diagnostic);
    }
}
```

## Advanced Features

### 1. Custom Mapping Validation

The analyzer validates custom mapping expressions for potential issues:

```csharp
private List<PropertyCompatibilityIssue> ValidateCustomPropertyMapping(
    CustomMappingInfo customMapping, 
    IPropertySymbol sourceProperty, 
    IPropertySymbol destinationProperty, 
    string propertyPath)
{
    var issues = new List<PropertyCompatibilityIssue>();
    
    // Check for dangerous method calls
    var dangerousMethod = CheckForDangerousMethodCalls(customMapping.SourceExpression);
    if (!string.IsNullOrEmpty(dangerousMethod))
    {
        issues.Add(new PropertyCompatibilityIssue
        {
            PropertyPath = propertyPath,
            IssueType = PropertyIssueType.CustomMappingDangerousExpression,
            Severity = DiagnosticSeverity.Warning,
            Description = $"Expression uses '{dangerousMethod}' which may throw exceptions"
        });
    }
    
    return issues;
}
```

### 2. Performance Optimizations

#### Caching Strategy:

```csharp
public class PropertyMappingAnalyzer
{
    private readonly Dictionary<string, PropertyAnalysisResult> _analysisCache = new();
    private readonly HashSet<string> _currentAnalysisStack = new();
    private const int MaxRecursionDepth = 5;
}
```

#### Circular Reference Prevention:

```csharp
if (_currentAnalysisStack.Contains(cacheKey))
{
    return new PropertyAnalysisResult
    {
        HasCircularReference = true,
        Issues = ImmutableArray<PropertyCompatibilityIssue>.Empty
    };
}
```

### 3. Complex Type Handling

The analyzer handles various complex scenarios:

```csharp
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
```

## Integration with Roslyn

### Analyzer Registration:

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MapsterAdaptAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(/* all diagnostic descriptors */);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }
}
```

### Compilation-Level Analysis:

The analyzer uses compilation-level analysis to ensure proper state sharing between discovery and analysis phases:

```csharp
private void AnalyzeCompilation(CompilationAnalysisContext compilationContext)
{
    var registry = new MappingConfigurationRegistry();
    var discovery = new MapsterConfigurationDiscovery(registry);
    
    // Collect all invocations first
    var allInvocations = CollectAllInvocations(compilationContext.Compilation);
    
    // Discovery phase
    foreach (var (invocation, semanticModel) in allInvocations)
        discovery.DiscoverConfiguration(CreateContext(invocation, semanticModel));
    
    // Analysis phase
    foreach (var (invocation, semanticModel) in allInvocations)
        if (!IsMapsterConfigurationCall(invocation, semanticModel))
            AnalyzeInvocation(CreateContext(invocation, semanticModel), registry);
}
```

## Common Patterns and Edge Cases

### 1. Fluent API Chain Processing

```csharp
// The analyzer handles complex fluent chains:
TypeAdapterConfig<Person, PersonDto>
    .NewConfig()
    .Map(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}")
    .Map(dest => dest.Age, src => DateTime.Now.Year - src.BirthYear)
    .Ignore(dest => dest.InternalId)
    .MapWith(src => new PersonDto { /* custom construction */ });
```

### 2. Generic Type Handling

The analyzer correctly handles generic types and type arguments:

```csharp
// Extracts T and U from TypeAdapterConfig<T, U>
private TypeConfigurationInfo? ExtractConfigurationInfo(InvocationExpressionSyntax invocation)
{
    if (symbolInfo.Symbol is INamedTypeSymbol namedType && namedType.IsGenericType)
    {
        var typeArgs = namedType.TypeArguments;
        if (typeArgs.Length == 2)
        {
            return new TypeConfigurationInfo
            {
                SourceType = typeArgs[0],
                DestinationType = typeArgs[1]
            };
        }
    }
}
```

### 3. Property Path Tracking

For nested objects, the analyzer tracks property paths:

```csharp
// Example: "Address.Street" for person.Address.Street
private string CombinePropertyPath(string basePath, string propertyName)
{
    return string.IsNullOrEmpty(basePath) ? propertyName : $"{basePath}.{propertyName}";
}
```

## Testing and Validation

The analyzer includes comprehensive testing through:

1. **Unit Tests**: Test individual components in isolation
2. **Integration Tests**: Test complete analysis scenarios
3. **Sample Projects**: Real-world usage examples
4. **Edge Case Tests**: Handle unusual or complex mapping scenarios

## Performance Characteristics

- **Time Complexity**: O(n × m × d) where n = number of properties, m = number of types, d = max depth
- **Space Complexity**: O(k) where k = number of cached analysis results
- **Optimization**: Caching and circular reference detection prevent exponential blowup

## Future Enhancements

Potential areas for improvement:

1. **Incremental Analysis**: Only re-analyze changed code
2. **Configuration File Support**: External mapping configuration files
3. **Advanced Pattern Recognition**: More sophisticated mapping pattern detection
4. **Performance Profiling**: Built-in performance metrics
5. **IDE Integration**: Enhanced Visual Studio/VS Code integration

## Conclusion

The Mapster Analyzer represents a sophisticated static analysis tool that combines Roslyn's powerful symbol analysis capabilities with domain-specific knowledge of Mapster's mapping patterns. Its two-phase architecture ensures reliable configuration discovery while maintaining high performance through strategic caching and circular reference prevention.

The analyzer's strength lies in its ability to understand both explicit custom configurations and implicit mapping behaviors, providing developers with compile-time feedback that prevents runtime mapping failures.