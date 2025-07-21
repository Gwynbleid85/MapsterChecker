# Expert Detail Answers

## Q1: Should the analyzer extend MapsterAdaptAnalyzer.Initialize() to discover configurations during compilation setup?
**Answer:** Yes

## Q2: Should custom mapping validation create new diagnostic IDs (MAPSTER004+) or extend existing ones?
**Answer:** Yes

## Q3: Should the configuration discovery component use SyntaxNodeAction to find TypeAdapterConfig calls, similar to how MapsterAdaptAnalyzer.cs:35 finds Adapt calls?
**Answer:** Yes

## Q4: Should property-level custom mappings completely bypass PropertyMappingAnalyzer.CheckDirectPropertyCompatibility() or supplement it with additional validation?
**Answer:** Supplement it

## Q5: Should the MappingConfigurationRegistry be a static class or instance-based dependency passed to TypeCompatibilityChecker.cs:54?
**Answer:** Instance-based dependency