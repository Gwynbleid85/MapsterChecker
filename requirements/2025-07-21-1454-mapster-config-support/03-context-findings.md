# Context Findings

## Current Codebase Analysis

### Core Components Identified
1. **MapsterAdaptAnalyzer.cs:47-128** - Main analyzer that detects Mapster.Adapt calls
2. **TypeCompatibilityChecker.cs:21-142** - Performs type compatibility validation
3. **PropertyMappingAnalyzer.cs:27-142** - Analyzes property-level compatibility recursively
4. **DiagnosticDescriptors.cs** - Defines all diagnostic rules (MAPSTER001, MAPSTER002, MAPSTER003 + property variants)

### Current Analyzer Flow
1. `MapsterAdaptAnalyzer.AnalyzeInvocation()` detects Mapster.Adapt calls
2. `ExtractAdaptCallInfo()` extracts source/destination types from invocation
3. `TypeCompatibilityChecker.CheckCompatibility()` validates type compatibility
4. `PropertyMappingAnalyzer.AnalyzePropertyMapping()` performs recursive property analysis
5. Diagnostics reported based on compatibility issues found

### Sample Configuration Analysis
- **File:** `samples/SampleApp/MapsterConfig.cs:9-11`
- **Pattern:** `TypeAdapterConfig<Person, PersonDto>.NewConfig().Map(dest => dest.Id, src => int.Parse(src.Id))`
- **Current Issue:** This custom mapping from `string Id` to `int Id` would trigger MAPSTER002 error, but should be valid due to the custom configuration

### Key Integration Points for Custom Config Support

#### 1. Configuration Discovery (New Component Needed)
- Scan compilation for `TypeAdapterConfig<TSource, TDestination>.NewConfig()` calls
- Parse referenced projects for configuration files
- Build mapping configuration registry during compilation

#### 2. Enhanced TypeCompatibilityChecker (Modifications Required)
- **File:** `TypeCompatibilityChecker.cs:21`
- **Required Changes:** Add configuration lookup before performing compatibility checks
- **Method:** Modify `CheckCompatibility()` to consult custom mappings first

#### 3. Property-Level Configuration Support (Modifications Required)  
- **File:** `PropertyMappingAnalyzer.cs:152`
- **Required Changes:** `CheckDirectPropertyCompatibility()` needs to check for custom property mappings
- **Method:** Look up property-specific `.Map()` configurations before falling back to default checks

#### 4. New Diagnostic Patterns Needed
- **File:** `DiagnosticDescriptors.cs`
- **Required:** Diagnostics for invalid custom mapping expressions
- **Required:** Warnings for custom mappings that could fail at runtime

## Mapster API Patterns to Support

### High Priority Patterns
1. **Basic Property Mapping:** `.Map(dest => dest.Property, src => src.SourceProperty)`
2. **Expression Mapping:** `.Map(dest => dest.Id, src => int.Parse(src.Id))`
3. **Constructor Mapping:** `.MapWith(src => new Destination(src.Param))`
4. **Ignore Mapping:** `.Ignore(dest => dest.PropertyToIgnore)`

### Medium Priority Patterns
1. **Conditional Mapping:** `.Map(dest => dest.Property, src => src.Property, srcCond => srcCond.Condition)`
2. **Global Settings:** `TypeAdapterConfig.GlobalSettings.NewConfig<>()`
3. **Assembly Scanning:** `TypeAdapterConfig.GlobalSettings.Scan(assembly)`

### Low Priority Patterns
1. **IRegister implementations:** Custom mapping profile classes
2. **Lifecycle hooks:** `.BeforeMapping()`, `.AfterMapping()`
3. **Bidirectional configs:** `.TwoWays()`

## Technical Constraints Identified

### Performance Considerations
- Configuration discovery must happen once per compilation, not per Adapt call
- Cache mapping configurations to avoid repeated parsing
- Limit configuration scanning to avoid excessive compilation times

### Scope Limitations
- Only analyze source code configurations, not compiled assemblies (per user requirements)
- No support for configuration inheritance/chaining (per user requirements)
- Focus on type safety validation of custom expressions (per user requirements)

## Architecture Recommendations

### New Components Required
1. **MapsterConfigurationDiscovery** - Scans compilation for TypeAdapterConfig setups
2. **MappingConfigurationRegistry** - Stores and queries custom mapping rules
3. **CustomMappingValidator** - Validates safety of custom mapping expressions

### Modified Components Required
1. **MapsterAdaptAnalyzer** - Initialize configuration discovery during analysis context setup
2. **TypeCompatibilityChecker** - Consult configuration registry before performing checks
3. **PropertyMappingAnalyzer** - Check for property-level custom mappings

### Integration Strategy
- Configuration discovery happens once during `Initialize()` phase
- Registry passed to all compatibility checkers during analysis
- Fallback to existing logic when no custom configuration found