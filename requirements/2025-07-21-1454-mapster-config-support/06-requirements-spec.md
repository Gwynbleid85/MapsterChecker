# Requirements Specification: Mapster Configuration Support

## Problem Statement

The current MapsterChecker analyzer validates type compatibility for `Mapster.Adapt()` calls based only on default mapping conventions. However, Mapster supports custom mapping configurations via `TypeAdapterConfig` that can override these default rules, allowing mappings between otherwise incompatible types. The analyzer currently reports false positives when custom configurations make "incompatible" mappings actually valid.

**Example Issue:** 
```csharp
// Custom configuration makes this valid
TypeAdapterConfig<Person, PersonDto>.NewConfig()
    .Map(dest => dest.Id, src => int.Parse(src.Id));

// But analyzer currently reports MAPSTER002 error
var dto = person.Adapt<PersonDto>(); // Should be valid due to custom config
```

## Solution Overview

Extend the MapsterChecker analyzer to discover, parse, and utilize Mapster's `TypeAdapterConfig` configurations when validating `Adapt()` calls. Custom mappings will override default compatibility checks while still validating the safety of custom mapping expressions.

## Functional Requirements

### FR1: Configuration Discovery
- **Requirement:** Automatically discover `TypeAdapterConfig<TSource, TDestination>.NewConfig()` calls in current project and all referenced projects (source code only, not compiled assemblies)
- **Pattern Support:** 
  - `TypeAdapterConfig<Source, Dest>.NewConfig()`
  - `TypeAdapterConfig.GlobalSettings.NewConfig<Source, Dest>()`
  - Method chaining: `.Map().Ignore().TwoWays()`
- **Scope:** Source code in current compilation and referenced project source code
- **Exclusion:** No support for compiled assembly configurations or configuration inheritance

### FR2: Custom Property Mapping Override
- **Requirement:** When `.Map(dest => dest.Property, src => src.Expression)` exists, bypass normal type compatibility checks for that specific property only
- **Behavior:** Custom mapped properties skip `PropertyMappingAnalyzer.CheckDirectPropertyCompatibility()` type incompatibility validation
- **Limitation:** Override applies only to the specifically mapped property, not the entire type

### FR3: Custom Mapping Expression Validation
- **Requirement:** Validate that custom mapping expressions are type-safe and won't cause runtime failures
- **Validation Types:**
  - Expression return type compatibility with destination property
  - Null reference potential in expressions
  - Exception-throwing method calls (like `int.Parse()`)
- **Diagnostic Level:** New diagnostic IDs (MAPSTER004+) for custom expression issues

### FR4: Configuration Pattern Support
**High Priority Patterns:**
- `.Map(dest => dest.Property, src => src.SourceProperty)` - Basic property mapping
- `.Map(dest => dest.Property, src => expression)` - Expression-based mapping  
- `.Ignore(dest => dest.Property)` - Property exclusion
- `.MapWith(src => new Destination(...))` - Constructor-based mapping

**Medium Priority Patterns:**
- `.Map(dest => dest.Property, src => src.Property, condition)` - Conditional mapping
- `TypeAdapterConfig.GlobalSettings` configurations
- Assembly scanning patterns

**Out of Scope:**
- `.BeforeMapping()/.AfterMapping()` lifecycle hooks
- `.TwoWays()` bidirectional configuration analysis
- `IRegister` implementation pattern
- Configuration inheritance/chaining

## Technical Requirements

### TR1: Analyzer Architecture Extension
- **File:** `MapsterChecker.Analyzer/MapsterAdaptAnalyzer.cs:31`
- **Modification:** Extend `Initialize()` method to register `SyntaxNodeAction` for configuration discovery
- **Pattern:** Use `RegisterSyntaxNodeAction(DiscoverConfiguration, SyntaxKind.InvocationExpression)` similar to existing Adapt call detection

### TR2: Configuration Registry Component
- **New Class:** `MappingConfigurationRegistry`
- **Responsibility:** Store discovered custom mappings indexed by source/destination type pairs
- **Architecture:** Instance-based dependency passed to `TypeCompatibilityChecker` and `PropertyMappingAnalyzer`
- **Interface:**
  ```csharp
  public class MappingConfigurationRegistry
  {
      bool HasCustomMapping(ITypeSymbol source, ITypeSymbol dest);
      bool HasPropertyMapping(ITypeSymbol source, ITypeSymbol dest, string propertyName);
      CustomMappingInfo GetPropertyMapping(ITypeSymbol source, ITypeSymbol dest, string propertyName);
  }
  ```

### TR3: Configuration Discovery Component  
- **New Class:** `MapsterConfigurationDiscovery`
- **Responsibility:** Parse `TypeAdapterConfig` syntax trees and extract mapping configurations
- **Integration:** Called during `MapsterAdaptAnalyzer.Initialize()` phase
- **Method:** Analyze invocation expressions for `TypeAdapterConfig<,>.NewConfig()` patterns

### TR4: Enhanced Type Compatibility Integration
- **File:** `MapsterChecker.Analyzer/TypeCompatibilityChecker.cs:54`
- **Modification:** Constructor accepts `MappingConfigurationRegistry` parameter
- **Logic:** `CheckCompatibility()` method consults registry before applying default rules
- **Behavior:** Skip incompatibility checks when custom configuration exists

### TR5: Enhanced Property Analysis Integration
- **File:** `MapsterChecker.Analyzer/PropertyMappingAnalyzer.cs:152`
- **Modification:** `CheckDirectPropertyCompatibility()` accepts registry and checks for custom property mappings
- **Behavior:** Supplement existing validation with custom expression safety analysis
- **Flow:** Check registry → validate custom expression if found → apply default checks if no custom mapping

### TR6: New Diagnostic Descriptors
- **File:** `MapsterChecker.Analyzer/DiagnosticDescriptors.cs`
- **New Diagnostics:**
  - `MAPSTER004`: "Custom mapping expression may throw exception"
  - `MAPSTER005`: "Custom mapping expression return type incompatible" 
  - `MAPSTER006`: "Custom mapping expression may produce null value"
- **Category:** `MapsterChecker.CustomMapping`

## Implementation Hints

### Configuration Discovery Pattern
```csharp
// In MapsterAdaptAnalyzer.Initialize()
var registry = new MappingConfigurationRegistry();
var discovery = new MapsterConfigurationDiscovery(registry);
context.RegisterSyntaxNodeAction(discovery.DiscoverConfiguration, SyntaxKind.InvocationExpression);
context.RegisterSyntaxNodeAction(ctx => AnalyzeInvocation(ctx, registry), SyntaxKind.InvocationExpression);
```

### Type Compatibility Check Pattern
```csharp
// In TypeCompatibilityChecker.CheckCompatibility()
if (_configurationRegistry.HasCustomMapping(sourceType, destinationType))
{
    // Skip default incompatibility checks, but still check nullability
    // Validate custom mapping expressions instead
}
```

### Property-Level Integration Pattern
```csharp  
// In PropertyMappingAnalyzer.CheckDirectPropertyCompatibility()
if (_configurationRegistry.HasPropertyMapping(sourceType, destType, propertyName))
{
    var customMapping = _configurationRegistry.GetPropertyMapping(sourceType, destType, propertyName);
    return ValidateCustomExpression(customMapping); // New validation logic
}
// Fall back to existing compatibility checks
```

## Acceptance Criteria

### AC1: Configuration Discovery
- [ ] Analyzer discovers `TypeAdapterConfig<Person, PersonDto>.NewConfig().Map(...)` in same project
- [ ] Analyzer discovers configurations in referenced project source files  
- [ ] Analyzer ignores configurations in external compiled assemblies
- [ ] Discovery happens once during analyzer initialization, not per Adapt call

### AC2: Custom Mapping Override
- [ ] Custom `.Map(dest => dest.Id, src => int.Parse(src.Id))` prevents MAPSTER002 for `Id` property
- [ ] Other properties without custom mapping still validate normally
- [ ] Top-level type incompatibility still reported when no type-level custom mapping exists

### AC3: Expression Validation
- [ ] `int.Parse(src.Id)` generates MAPSTER004 warning about potential exceptions
- [ ] Null reference potential in expressions generates MAPSTER006 warnings
- [ ] Expression return type compatibility validated with destination property type

### AC4: Integration Preservation
- [ ] All existing MAPSTER001, MAPSTER002, MAPSTER003 diagnostics still work for non-configured mappings
- [ ] Performance impact minimal (configuration discovery during initialization only)
- [ ] Existing test cases continue to pass

### AC5: Sample Validation
- [ ] `samples/SampleApp/MapsterConfig.cs` configuration properly recognized
- [ ] `person.Adapt<PersonDto>()` in `Program.cs:74` no longer reports MAPSTER002 for Id property
- [ ] Custom mapping validates `int.Parse(string)` expression safety

## Assumptions

1. **Configuration Scope:** Only source code configurations are analyzed, not runtime/dynamic configurations
2. **Expression Complexity:** Custom mapping expressions are analyzable via static analysis (no dynamic method calls)
3. **Performance:** Configuration discovery time is acceptable for normal compilation scenarios
4. **Precedence:** Custom mappings completely override default compatibility rules for mapped properties
5. **Validation Depth:** Expression validation focuses on common safety issues, not comprehensive static analysis