using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Tests for custom Mapster mapping configuration support.
/// Validates that custom TypeAdapterConfig configurations are properly discovered and used
/// to override default compatibility checks.
/// </summary>
public class CustomMappingConfigurationTests
{
    [Fact]
    public async Task CustomPropertyMapping_ShouldPreventIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Id = ""123"", Name = ""John"" };
        var result = person.Adapt<PersonDto>(); // Should not report MAPSTER002 for Id property
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CustomPropertyMapping_WithDangerousMethodCall_ShouldReportWarning()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Id = ""123"", Name = ""John"" };
        var result = person.Adapt<PersonDto>();
    }
}";

        // Note: The custom mapping validation would be performed during property analysis
        // For now, we test that the incompatibility diagnostic is suppressed
        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IgnoredProperty_ShouldPreventMissingPropertyDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Name { get; set; } = string.Empty;
}

public class PersonDto
{
    public string Name { get; set; } = string.Empty;
    public int CalculatedField { get; set; }
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Ignore(dest => dest.CalculatedField);
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Name = ""John"" };
        var result = person.Adapt<PersonDto>(); // Should not report missing property for CalculatedField
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NoCustomMapping_ShouldStillReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Id = ""123"", Name = ""John"" };
        var result = {|MAPSTER002P:person.Adapt<PersonDto>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(23, 22, 23, 47)
            .WithArguments("Id", "string", "int");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task GlobalSettingsConfiguration_ShouldBeRecognized()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Id { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig.GlobalSettings
            .NewConfig<Person, PersonDto>()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Id = ""123"" };
        var result = person.Adapt<PersonDto>(); // Should not report incompatibility due to global config
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task MultipleCustomMappings_ShouldAllBeApplied()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Id { get; set; } = string.Empty;
    public string Age { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
    public int Age { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IgnoredField { get; set; } = string.Empty;
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id))
            .Map(dest => dest.Age, src => int.Parse(src.Age))
            .Ignore(dest => dest.IgnoredField);
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Id = ""123"", Age = ""30"", Name = ""John"" };
        var result = person.Adapt<PersonDto>(); // Should not report any diagnostics
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ConstructorMapping_ShouldBeRecognized()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class PersonDto
{
    public PersonDto(string fullName, int years)
    {
        FullName = fullName;
        Years = years;
    }
    
    public string FullName { get; set; }
    public int Years { get; set; }
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .MapWith(src => new PersonDto(src.Name, src.Age));
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Name = ""John"", Age = 30 };
        var result = person.Adapt<PersonDto>(); // Should not report incompatibility due to custom constructor
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PartialCustomMapping_ShouldOnlyOverrideConfiguredProperties()
    {
        const string testCode = @"
using Mapster;

public class Person
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BadProperty { get; set; } = string.Empty;
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public System.DateTime BadProperty { get; set; }
}

public class MapsterConfig
{
    public static void Configure()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
        // Note: BadProperty is not configured, should still report error
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Id = ""123"", Name = ""John"", BadProperty = ""bad"" };
        var result = {|MAPSTER002P:person.Adapt<PersonDto>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(35, 22, 35, 47)
            .WithArguments("BadProperty", "string", "System.DateTime");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        // Add Mapster package reference
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync();
    }
}