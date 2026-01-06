# MapsterChecker

A Roslyn analyzer that performs static analysis on Mapster.Adapt method calls to detect type compatibility issues at build time, with a focus on nullable to non-nullable type mapping violations.

## Overview

MapsterChecker helps prevent runtime null reference exceptions by analyzing your Mapster mapping calls during compilation. It identifies potentially dangerous mappings where nullable types are mapped to non-nullable types, as well as fundamentally incompatible type mappings.

## Features

- **MAPSTER001**: Detects nullable to non-nullable mappings that may cause null reference exceptions
- **MAPSTER002**: Identifies incompatible type mappings that cannot be converted
- **MAPSTER003**: Reports missing property mappings (future feature)
- **Build-time analysis**: Catches issues during compilation, not at runtime
- **IDE integration**: Real-time feedback in Visual Studio, VS Code, and JetBrains Rider
- **Configurable**: Customize rule severity and behavior via EditorConfig

## Installation

### Via NuGet Package Manager

```bash
dotnet add package MapsterChecker.Analyzer
```

### Via Package Manager Console

```powershell
Install-Package MapsterChecker.Analyzer
```

### Via PackageReference

```xml
<PackageReference Include="MapsterChecker.Analyzer" Version="1.0.1">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

## Diagnostic Rules

### MAPSTER001: Nullable to Non-Nullable Mapping

**Severity**: Warning  
**Category**: MapsterChecker.Nullability

Detects when mapping from a nullable type to a non-nullable type, which may result in null reference exceptions.

#### Examples

```csharp
// ⚠️ MAPSTER001: Warning
string? nullableString = GetNullableString();
var result = nullableString.Adapt<string>(); // May throw if nullableString is null

// ⚠️ MAPSTER001: Warning  
int? nullableInt = GetNullableInt();
var number = nullableInt.Adapt<int>(); // May throw if nullableInt is null

// ✅ Safe: Non-nullable to nullable
string nonNullableString = "test";
var result = nonNullableString.Adapt<string?>(); // Safe conversion
```

### MAPSTER002: Incompatible Type Mapping

**Severity**: Error  
**Category**: MapsterChecker.Compatibility

Detects when attempting to map between fundamentally incompatible types that cannot be converted.

#### Examples

```csharp
// ❌ MAPSTER002: Error
int number = 42;
var dateTime = number.Adapt<DateTime>(); // Incompatible types

// ❌ MAPSTER002: Error
string text = "hello";
var guid = text.Adapt<Guid>(); // Cannot convert arbitrary string to Guid

// ✅ Safe: Compatible numeric types
int number = 42;
long longNumber = number.Adapt<long>(); // Valid widening conversion
```

### MAPSTER003: Missing Property Mapping (Future)

**Severity**: Info  
**Category**: MapsterChecker.Mapping

Will detect when destination type contains properties not present in the source type.

## Configuration

### EditorConfig

You can configure rule severity in your `.editorconfig` file:

```ini
[*.cs]
# Set MAPSTER001 to error instead of warning
dotnet_diagnostic.MAPSTER001.severity = error

# Disable MAPSTER002 completely
dotnet_diagnostic.MAPSTER002.severity = none

# Set MAPSTER003 to suggestion
dotnet_diagnostic.MAPSTER003.severity = suggestion
```

### Suppression

You can suppress specific diagnostics using standard C# suppression methods:

```csharp
// Suppress via pragma
#pragma warning disable MAPSTER001
var result = nullableString.Adapt<string>();
#pragma warning restore MAPSTER001

// Suppress via attribute
[SuppressMessage("MapsterChecker", "MAPSTER001")]
public string ConvertToString(string? input) => input.Adapt<string>();
```

## Supported Mapster Patterns

MapsterChecker analyzes the following Mapster usage patterns:

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
- `string?` → `string`: ⚠️ MAPSTER001 (Warning)
- `string` → `string?`: ✅ Safe
- `int?` → `int`: ⚠️ MAPSTER001 (Warning)
- `int` → `int?`: ✅ Safe

### Numeric Type Compatibility
- `int` → `long`: ✅ Safe (widening conversion)
- `float` → `double`: ✅ Safe (widening conversion)
- `long` → `int`: ⚠️ Potential data loss (but allowed by Mapster)
- `string` → `int`: ❌ MAPSTER002 (Incompatible)

### Reference Type Compatibility
- Inheritance hierarchy: ✅ Safe (base ↔ derived)
- Interface to concrete: ✅ Safe (with runtime validation)
- Unrelated types: ❌ MAPSTER002 (Incompatible)

### Collection Compatibility
- `List<T>` → `IEnumerable<U>`: Checks T → U compatibility
- `T[]` → `List<U>`: Checks T → U compatibility
- Collections with incompatible element types: ❌ MAPSTER002

## Building from Source

### Prerequisites
- .NET 9.0 or .NET 10.0 SDK
- Visual Studio 2022 or JetBrains Rider (optional)

### Build Steps

```bash
# Clone the repository
git clone https://github.com/mapsterchecker/mapsterchecker.git
cd mapsterchecker

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Create NuGet package
dotnet pack --configuration Release
```

## Sample Application

The repository includes a sample application demonstrating various mapping scenarios:

```bash
cd samples/SampleApp
dotnet build # This will show analyzer warnings/errors
dotnet run   # Run the sample application
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow existing code style and conventions
- Add unit tests for new features
- Update documentation for user-facing changes
- Ensure all tests pass before submitting PR

## Performance

MapsterChecker is designed to be fast and responsive:

- **< 50ms per file**: Average analysis time
- **Concurrent execution**: Leverages Roslyn's concurrent analysis capabilities
- **Syntax-first filtering**: Quick rejection of non-Adapt method calls
- **Semantic analysis caching**: Efficient type symbol lookups

## Limitations

- **Mapster-specific**: Only analyzes Mapster.Adapt calls, not other mapping libraries
- **Static analysis only**: Cannot detect runtime configuration-based mappings
- **Conservative approach**: May report false positives for complex scenarios
- **Configuration mapping**: Does not analyze custom Mapster configuration rules

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Mapster](https://github.com/MapsterMapper/Mapster) - The object-to-object mapper this analyzer supports
- [Roslyn](https://github.com/dotnet/roslyn) - The .NET Compiler Platform that makes this analyzer possible
- [Microsoft.CodeAnalysis.Testing](https://github.com/dotnet/roslyn-sdk) - Testing framework for analyzers

## Support

- 📖 [Documentation](https://github.com/mapsterchecker/mapsterchecker/wiki)
- 🐛 [Report Issues](https://github.com/mapsterchecker/mapsterchecker/issues)
- 💬 [Discussions](https://github.com/mapsterchecker/mapsterchecker/discussions)
- 📧 [Contact](mailto:support@mapsterchecker.dev)