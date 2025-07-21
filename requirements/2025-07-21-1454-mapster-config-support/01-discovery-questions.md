# Discovery Questions

## Q1: Should the analyzer automatically discover TypeAdapterConfig configurations in the same project?
**Default if unknown:** Yes (analyzers typically scan the compilation context for configurations)

## Q2: Should the analyzer support detecting configuration from external assemblies/references?
**Default if unknown:** No (most static analyzers focus on current compilation unit for performance)

## Q3: Should custom mapping rules override the analyzer's built-in compatibility checks?
**Default if unknown:** Yes (custom configurations should take precedence over default rules)

## Q4: Should the analyzer validate that custom mapping expressions are type-safe?
**Default if unknown:** Yes (maintaining type safety is the primary goal of the analyzer)

## Q5: Should the analyzer handle configuration inheritance and chaining between different TypeAdapterConfig setups?
**Default if unknown:** No (complex inheritance analysis would significantly increase complexity)