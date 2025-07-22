using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Comprehensive tests for the MapsterChecker analyzer covering various scenarios.
/// </summary>
public class ComprehensiveAnalyzerTestsSimplified
{
    [Fact]
    public async Task DirectMapping_IncompatibleGuidToString_ShowsError()
    {
        const string testCode = @"
using System;
using Mapster;

class Program
{
    void Test()
    {
        Guid guid = Guid.NewGuid();
        string result = {|MAPSTER002:guid.Adapt<string>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002")
            .WithSpan(9, 21, 9, 42)
            .WithArguments("System.Guid", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task DirectMapping_IncompatibleStringToGuid_ShowsError()
    {
        const string testCode = @"
using System;
using Mapster;

class Program
{
    void Test()
    {
        string str = ""test"";
        Guid result = {|MAPSTER002:str.Adapt<Guid>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002")
            .WithSpan(9, 23, 9, 37)
            .WithArguments("string", "System.Guid");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task ComplexTypeMapping_IncompatibleProperties_ShowsPropertyErrors()
    {
        const string testCode = @"
using System;
using Mapster;

class Source
{
    public Guid Id { get; set; }
    public string? NullableName { get; set; }
}

class Destination
{
    public string Id { get; set; } = """";
    public string Name { get; set; } = """";
}

class Program
{
    void Test()
    {
        var source = new Source();
        var dest = {|MAPSTER002P:source.Adapt<Destination>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(21, 20, 21, 43)
            .WithArguments("Id", "System.Guid", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task CustomConfig_PropertyMapping_OverridesIncompatibilityError()
    {
        const string testCode = @"
using System;
using Mapster;

class Source
{
    public Guid Id { get; set; }
    public string Name { get; set; } = """";
}

class Destination
{
    public string Id { get; set; } = """";
    public string Name { get; set; } = """";
}

class Program
{
    static void ConfigureMapping()
    {
        TypeAdapterConfig<Source, Destination>
            .NewConfig()
            .Map(dest => dest.Id, src => src.Id.ToString());
    }
    
    void Test()
    {
        ConfigureMapping();
        
        var source = new Source();
        var dest = source.Adapt<Destination>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task CustomConfig_PartialMapping_OnlyConfiguredPropertiesFixed()
    {
        const string testCode = @"
using System;
using Mapster;

class Source
{
    public Guid Id { get; set; }
    public int Status { get; set; }
}

class Destination
{
    public string Id { get; set; } = """";
    public DateTime Status { get; set; }
}

class Program
{
    static void ConfigureMapping()
    {
        TypeAdapterConfig<Source, Destination>
            .NewConfig()
            .Map(dest => dest.Id, src => src.Id.ToString());
    }
    
    void Test()
    {
        ConfigureMapping();
        
        var source = new Source();
        var dest = {|MAPSTER002P:source.Adapt<Destination>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(30, 20, 30, 43)
            .WithArguments("Status", "int", "System.DateTime");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NullableToNonNullable_ShowsWarning()
    {
        const string testCode = @"
using System;
using Mapster;

class Program
{
    void Test()
    {
        string? nullableString = ""test"";
        string nonNullableString = {|MAPSTER001:nullableString.Adapt<string>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001")
            .WithSpan(9, 36, 9, 63)
            .WithArguments("string?", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task CompatibleNumericTypes_NoError()
    {
        const string testCode = @"
using Mapster;

class Program
{
    void Test()
    {
        int number = 42;
        long longNumber = number.Adapt<long>();
        
        string nonNullableString = ""test"";
        string? nullableString = nonNullableString.Adapt<string?>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NestedObjects_IncompatibleNestedProperties_ShowsNestedPropertyErrors()
    {
        const string testCode = @"
using System;
using Mapster;

class Address
{
    public Guid Id { get; set; }
    public string Street { get; set; } = """";
}

class AddressDto
{
    public string Id { get; set; } = """";
    public string Street { get; set; } = """";
}

class Person
{
    public string Name { get; set; } = """";
    public Address Address { get; set; } = new Address();
}

class PersonDto
{
    public string Name { get; set; } = """";
    public AddressDto Address { get; set; } = new AddressDto();
}

class Program
{
    void Test()
    {
        var person = new Person();
        var dto = {|MAPSTER002P:person.Adapt<PersonDto>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(32, 19, 32, 41)
            .WithArguments("Address.Id", "System.Guid", "string");

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