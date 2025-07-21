# MapsterChecker - Project Implementation Plan

## Overview
A Roslyn analyzer that performs static analysis on Mapster.Adapt method calls to detect type compatibility issues at build time, with a focus on nullable to non-nullable type mapping violations.

## Project Structure
```
MapsterChecker/
├── MapsterChecker.Analyzer/          # Main analyzer project
│   ├── MapsterAdaptAnalyzer.cs       # Core analyzer implementation
│   ├── DiagnosticDescriptors.cs      # Diagnostic definitions
│   ├── TypeCompatibilityChecker.cs   # Type analysis logic
│   └── MapsterChecker.Analyzer.csproj
├── MapsterChecker.Tests/             # Unit tests
│   ├── MapsterAdaptAnalyzerTests.cs  # Test cases
│   └── MapsterChecker.Tests.csproj
├── MapsterChecker.Package/           # NuGet packaging
│   └── MapsterChecker.Package.csproj
└── samples/                          # Sample projects for testing
    └── SampleApp/
```

## Diagnostic Rules

### MAPSTER001: Nullable to Non-Nullable Mapping
**Severity**: Warning  
**Description**: Detects when mapping from a nullable type to a non-nullable type  
**Example**:
```csharp
string? nullableString = GetNullableString();
var result = nullableString.Adapt<string>(); // MAPSTER001
```

### MAPSTER002: Incompatible Type Mapping
**Severity**: Error  
**Description**: Detects when source and destination types are fundamentally incompatible  
**Example**:
```csharp
int number = 42;
var result = number.Adapt<DateTime>(); // MAPSTER002
```

### MAPSTER003: Missing Property Mapping (Future)
**Severity**: Info  
**Description**: Detects when destination type has properties not present in source type  
**Example**:
```csharp
class Source { public string Name { get; set; } }
class Dest { public string Name { get; set; } public int Age { get; set; } }
var result = source.Adapt<Dest>(); // MAPSTER003: Age property not mapped
```

## Implementation Phases

### Phase 1: Core Infrastructure ✅ PLANNING
- [x] Project structure setup
- [ ] Basic analyzer skeleton
- [ ] Diagnostic descriptors
- [ ] Unit test framework
- [ ] CI/CD pipeline

### Phase 2: Basic Detection
- [ ] Syntax analysis for Adapt method calls
- [ ] Pattern matching for both Adapt variants
- [ ] Mapster namespace filtering
- [ ] Basic type extraction

### Phase 3: Type Compatibility Analysis
- [ ] Semantic model integration
- [ ] Nullable reference type checking
- [ ] Value type compatibility
- [ ] Basic reference type checking

### Phase 4: Advanced Features
- [ ] Generic type support
- [ ] Collection mapping analysis
- [ ] Nested object validation
- [ ] Performance optimization

### Phase 5: Production Ready
- [ ] Code fix providers
- [ ] Configuration system
- [ ] Documentation
- [ ] NuGet publishing

## Key Technical Decisions

### Target Framework
- **.NET Standard 2.0**: Ensures compatibility with both .NET Framework and .NET Core/5+
- **C# Latest**: Use latest language features while targeting older framework

### Dependencies
- **Microsoft.CodeAnalysis.CSharp 4.5.0**: Core Roslyn APIs
- **Microsoft.CodeAnalysis.Analyzers 3.3.4**: Analyzer infrastructure
- **Microsoft.CodeAnalysis.Testing**: Unit testing framework

### Architecture Patterns
- **Single Responsibility**: Each analyzer class handles one type of violation
- **Semantic Analysis**: Use Roslyn's semantic model for accurate type information
- **Fast Exit**: Minimize performance impact on build times
- **Configurable**: Allow teams to customize rule severity and behavior

## Supported Mapster Patterns

### Generic Adapt Method
```csharp
var destination = source.Adapt<DestinationType>();
```

### Non-Generic Adapt Method
```csharp
var destination = new DestinationType();
source.Adapt(destination);
```

### Extension Method Usage
```csharp
using Mapster;
var result = sourceObject.Adapt<TargetType>();
```

## Type Compatibility Rules

### Nullable Analysis
- `string?` → `string`: ⚠️ MAPSTER001
- `string` → `string?`: ✅ Safe
- `int?` → `int`: ⚠️ MAPSTER001
- `int` → `int?`: ✅ Safe

### Value Type Compatibility
- `int` → `long`: ✅ Safe (widening)
- `long` → `int`: ⚠️ Potential data loss
- `string` → `int`: ❌ MAPSTER002 (incompatible)

### Reference Type Compatibility
- Inheritance hierarchy: ✅ Safe (base to derived with validation)
- Interface to concrete: ✅ Safe (with runtime validation)
- Unrelated types: ❌ MAPSTER002

### Collection Compatibility
- `List<T>` → `IEnumerable<U>`: Check T → U compatibility
- `T[]` → `List<U>`: Check T → U compatibility
- Size-specific collections: Validate capacity constraints

## Testing Strategy

### Unit Test Categories
1. **Positive Cases**: Valid mappings that should not trigger diagnostics
2. **Negative Cases**: Invalid mappings that should trigger specific diagnostics
3. **Edge Cases**: Complex scenarios, generics, nested types
4. **Performance Tests**: Ensure analyzer completes quickly

### Test Data Organization
```csharp
[Theory]
[InlineData("string?", "string", "MAPSTER001")]
[InlineData("int", "DateTime", "MAPSTER002")]
[InlineData("List<string>", "IEnumerable<string>", null)]
public void AnalyzeAdaptCall_WithTypeMapping_ReturnsExpectedDiagnostic(
    string sourceType, string destType, string expectedDiagnostic)
```

## Configuration Options

### EditorConfig Integration
```ini
[*.cs]
# Severity levels
dotnet_diagnostic.MAPSTER001.severity = warning
dotnet_diagnostic.MAPSTER002.severity = error
dotnet_diagnostic.MAPSTER003.severity = suggestion

# Rule-specific options
mapster_checker.nullable_mapping_severity = warning
mapster_checker.ignore_test_projects = true
```

### Suppression Support
```csharp
#pragma warning disable MAPSTER001
var result = nullableString.Adapt<string>();
#pragma warning restore MAPSTER001

// Or via attribute
[SuppressMessage("MapsterChecker", "MAPSTER001")]
public string ConvertToString(string? input) => input.Adapt<string>();
```

## Performance Considerations

### Optimization Strategies
- **Syntax-first filtering**: Quick rejection of non-Adapt calls
- **Semantic analysis caching**: Cache type symbol lookups
- **Incremental analysis**: Only re-analyze changed files
- **Lazy evaluation**: Defer expensive operations until needed

### Performance Targets
- **< 50ms per file**: Average analysis time
- **< 100MB memory**: Peak memory usage for large solutions
- **No false positives**: Accuracy over speed when in conflict

## Success Metrics

### Adoption Metrics
- NuGet download count
- GitHub stars and forks
- Community feedback and issues

### Quality Metrics
- False positive rate < 5%
- False negative rate < 1%
- Build time impact < 10%
- User satisfaction surveys

## Future Enhancements

### Potential Features
- **Code fix providers**: Automatic fixes for common violations
- **Custom mapping rules**: User-defined compatibility rules
- **IDE integration**: Real-time analysis in Visual Studio/Rider
- **Batch analysis**: Analyze entire solutions for mapping issues
- **Reporting dashboard**: Visual reports of mapping violations across projects

### Integration Opportunities
- **SonarQube rules**: Custom quality gate integration
- **GitHub Actions**: Automated PR checks
- **Azure DevOps**: Build pipeline integration
- **EditorConfig**: Expanded configuration options