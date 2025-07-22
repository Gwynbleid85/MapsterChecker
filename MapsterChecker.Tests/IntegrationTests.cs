using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task SampleAppScenario_WithCustomMapping_ShouldNotReportError()
    {
        const string code = @"
using Mapster;
using System;

namespace SampleApp;

public class Person
{
    public string Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public DateTime? BirthDate { get; set; }
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
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

public class Program
{
    public static void TestValidMappings(Person person)
    {
        var dto = person.Adapt<PersonDto>();
    }
}
";

        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        test.TestState.AdditionalReferences.Add(typeof(Mapster.TypeAdapterConfig).Assembly);
        
        // We should NOT expect any diagnostic since custom mapping is configured
        await test.RunAsync();
    }
}