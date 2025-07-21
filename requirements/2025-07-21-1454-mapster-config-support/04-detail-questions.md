# Expert Detail Questions

## Q1: Should the analyzer extend MapsterAdaptAnalyzer.Initialize() to discover configurations during compilation setup?
**Default if unknown:** Yes (most efficient to scan once during analyzer initialization rather than per-invocation)

## Q2: Should custom mapping validation create new diagnostic IDs (MAPSTER004+) or extend existing ones?
**Default if unknown:** Yes, create new diagnostic IDs (clearer separation of concerns and allows different configuration)

## Q3: Should the configuration discovery component use SyntaxNodeAction to find TypeAdapterConfig calls, similar to how MapsterAdaptAnalyzer.cs:35 finds Adapt calls?
**Default if unknown:** Yes (consistent with existing analyzer pattern and leverages Roslyn's efficient syntax tree traversal)

## Q4: Should property-level custom mappings completely bypass PropertyMappingAnalyzer.CheckDirectPropertyCompatibility() or supplement it with additional validation?
**Default if unknown:** Supplement it (validate the custom expression is type-safe while skipping incompatibility checks for the mapped property)

## Q5: Should the MappingConfigurationRegistry be a static class or instance-based dependency passed to TypeCompatibilityChecker.cs:54?
**Default if unknown:** Instance-based dependency (better testability and follows dependency injection patterns, consistent with SemanticModel parameter)