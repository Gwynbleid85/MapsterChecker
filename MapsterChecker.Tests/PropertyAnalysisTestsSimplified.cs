using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Focused tests for property-level analysis scenarios.
/// </summary>
public class PropertyAnalysisTestsSimplified
{
    [Fact]
    public async Task NumericConversions_CompatibleWidening_NoError()
    {
        const string testCode = @"
using Mapster;

class NumericSource
{
    public byte ByteValue { get; set; }
    public int IntValue { get; set; }
    public float FloatValue { get; set; }
}

class NumericDestination
{
    public short ByteValue { get; set; }
    public long IntValue { get; set; }
    public double FloatValue { get; set; }
}

class Program
{
    void Test()
    {
        var source = new NumericSource();
        var dest = source.Adapt<NumericDestination>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NumericConversions_IncompatibleNarrowing_ShowsErrors()
    {
        const string testCode = @"
using Mapster;

class NumericSource
{
    public long LongValue { get; set; }
    public double DoubleValue { get; set; }
}

class NumericDestination
{
    public int LongValue { get; set; }
    public float DoubleValue { get; set; }
}

class Program
{
    void Test()
    {
        var source = new NumericSource();
        var dest = {|MAPSTER002P:source.Adapt<NumericDestination>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(20, 20, 20, 52)
            .WithArguments("LongValue", "long", "int");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NullableReferenceTypes_NullableToNonNullable_ShowsWarning()
    {
        const string testCode = @"
#nullable enable
using Mapster;

class NullableSource
{
    public string? NullableString { get; set; }
    public object? NullableObject { get; set; }
}

class NullableDestination
{
    public string NullableString { get; set; } = """";
    public object NullableObject { get; set; } = new object();
}

class Program
{
    void Test()
    {
        var source = new NullableSource();
        var dest = {|MAPSTER001P:source.Adapt<NullableDestination>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001P")
            .WithSpan(21, 20, 21, 50)
            .WithArguments("NullableString", "string?", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NullableValueTypes_NullableToNonNullable_ShowsWarning()
    {
        const string testCode = @"
using System;
using Mapster;

class NullableValueSource
{
    public int? NullableInt { get; set; }
    public DateTime? NullableDateTime { get; set; }
}

class NullableValueDestination
{
    public int NullableInt { get; set; }
    public DateTime NullableDateTime { get; set; }
}

class Program
{
    void Test()
    {
        var source = new NullableValueSource();
        var dest = {|MAPSTER001P:source.Adapt<NullableValueDestination>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001P")
            .WithSpan(21, 20, 21, 54)
            .WithArguments("NullableInt", "int?", "int");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task CustomConfig_MultipleProperties_AllHandledCorrectly()
    {
        const string testCode = @"
using System;
using Mapster;

class ComplexSource
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public int Status { get; set; }
}

class ComplexDestination
{
    public string Id { get; set; } = """";
    public string Name { get; set; } = """";
    public string Status { get; set; } = """";
}

class Program
{
    static void ConfigureMappings()
    {
        TypeAdapterConfig<ComplexSource, ComplexDestination>
            .NewConfig()
            .Map(dest => dest.Id, src => src.Id.ToString())
            .Map(dest => dest.Name, src => src.Name ?? ""Unknown"")
            .Map(dest => dest.Status, src => src.Status.ToString());
    }
    
    void Test()
    {
        ConfigureMappings();
        
        var source = new ComplexSource();
        var dest = source.Adapt<ComplexDestination>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task InheritanceMapping_HandlesInheritedProperties()
    {
        const string testCode = @"
using System;
using Mapster;

class BaseEntity
{
    public int Id { get; set; }
}

class BaseEntityDto
{
    public int Id { get; set; }
}

class Person : BaseEntity
{
    public string Name { get; set; } = """";
    public Guid PersonId { get; set; }
}

class PersonDto : BaseEntityDto
{
    public string Name { get; set; } = """";
    public string PersonId { get; set; } = """";
}

class Program
{
    void Test()
    {
        var person = new Person();
        var personDto = {|MAPSTER002P:person.Adapt<PersonDto>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002P")
            .WithSpan(31, 25, 31, 46)
            .WithArguments("PersonId", "System.Guid", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task CustomConfig_IgnoreProperty_SkipsValidation()
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
            .Ignore(dest => dest.Id);
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