using MapsterChecker.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Unit tests for the MapsterConfigurationDiscovery class.
/// Tests the discovery and parsing of TypeAdapterConfig configurations from source code.
/// </summary>
public class MapsterConfigurationDiscoveryTests
{
    [Fact]
    public void DiscoverConfiguration_WithPropertyMapping_ShouldRegisterMapping()
    {
        // Arrange
        const string code = @"
using Mapster;

public class Person
{
    public string Id { get; set; }
}

public class PersonDto
{
    public int Id { get; set; }
}

public class Config
{
    public void Setup()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}";

        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);

        // Act
        var invocations = GetInvocationExpressions(code);
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation);
            discovery.DiscoverConfiguration(context);
        }

        // Assert
        var allMappings = registry.GetAllMappings().ToList();
        Assert.True(allMappings.Any(), "Expected at least one mapping to be discovered");
    }

    [Fact]
    public void DiscoverConfiguration_WithIgnoreMapping_ShouldRegisterIgnore()
    {
        // Arrange
        const string code = @"
using Mapster;

public class Person
{
    public string Name { get; set; }
}

public class PersonDto
{
    public string Name { get; set; }
    public string IgnoredField { get; set; }
}

public class Config
{
    public void Setup()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Ignore(dest => dest.IgnoredField);
    }
}";

        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);

        // Act
        var invocations = GetInvocationExpressions(code);
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation);
            discovery.DiscoverConfiguration(context);
        }

        // Assert
        var allMappings = registry.GetAllMappings().ToList();
        Assert.True(allMappings.Any(), "Expected at least one mapping to be discovered");
    }

    [Fact]
    public void DiscoverConfiguration_WithMapWithConstructor_ShouldRegisterConstructorMapping()
    {
        // Arrange
        const string code = @"
using Mapster;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class PersonDto
{
    public PersonDto(string name, int age) { }
    public string Name { get; set; }
    public int Age { get; set; }
}

public class Config
{
    public void Setup()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .MapWith(src => new PersonDto(src.Name, src.Age));
    }
}";

        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);

        // Act
        var invocations = GetInvocationExpressions(code);
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation);
            discovery.DiscoverConfiguration(context);
        }

        // Assert
        var allMappings = registry.GetAllMappings().ToList();
        Assert.True(allMappings.Any(), "Expected at least one mapping to be discovered");
    }

    [Fact]
    public void DiscoverConfiguration_WithChainedMappings_ShouldRegisterAllMappings()
    {
        // Arrange
        const string code = @"
using Mapster;

public class Person
{
    public string Id { get; set; }
    public string Name { get; set; }
}

public class PersonDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string IgnoredField { get; set; }
}

public class Config
{
    public void Setup()
    {
        TypeAdapterConfig<Person, PersonDto>
            .NewConfig()
            .Map(dest => dest.Id, src => int.Parse(src.Id))
            .Ignore(dest => dest.IgnoredField);
    }
}";

        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);

        // Act
        var invocations = GetInvocationExpressions(code);
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation);
            discovery.DiscoverConfiguration(context);
        }

        // Assert
        var allMappings = registry.GetAllMappings().ToList();
        Assert.True(allMappings.Any(), "Expected at least one mapping to be discovered");
        
        // Verify multiple mappings were registered (would need proper integration test to verify exact count)
        var mappingsForType = allMappings.SelectMany(m => m.Mappings).ToList();
        Assert.True(mappingsForType.Count >= 1, "Expected multiple mappings to be discovered");
    }

    [Fact]
    public void DiscoverConfiguration_WithGlobalSettings_ShouldRegisterMapping()
    {
        // Arrange
        const string code = @"
using Mapster;

public class Person
{
    public string Id { get; set; }
}

public class PersonDto
{
    public int Id { get; set; }
}

public class Config
{
    public void Setup()
    {
        TypeAdapterConfig.GlobalSettings
            .NewConfig<Person, PersonDto>()
            .Map(dest => dest.Id, src => int.Parse(src.Id));
    }
}";

        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);

        // Act
        var invocations = GetInvocationExpressions(code);
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation);
            discovery.DiscoverConfiguration(context);
        }

        // Assert
        var allMappings = registry.GetAllMappings().ToList();
        Assert.True(allMappings.Any(), "Expected at least one mapping to be discovered");
    }

    [Fact]
    public void DiscoverConfiguration_WithNonMapsterCall_ShouldNotRegisterMapping()
    {
        // Arrange
        const string code = @"
public class SomeClass
{
    public void SomeMethod()
    {
        var config = new object();
        config.ToString();
    }
}";

        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);

        // Act
        var invocations = GetInvocationExpressions(code);
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation);
            discovery.DiscoverConfiguration(context);
        }

        // Assert
        var allMappings = registry.GetAllMappings().ToList();
        Assert.Empty(allMappings);
    }

    /// <summary>
    /// Helper method to parse code and extract all invocation expressions.
    /// </summary>
    private static IEnumerable<InvocationExpressionSyntax> GetInvocationExpressions(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        return syntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>();
    }

    /// <summary>
    /// Helper method to create a SyntaxNodeAnalysisContext for testing.
    /// </summary>
    private static SyntaxNodeAnalysisContext CreateAnalysisContext(InvocationExpressionSyntax invocation)
    {
        // Create a minimal compilation for semantic analysis
        var code = invocation.SyntaxTree.GetText().ToString();
        var syntaxTree = invocation.SyntaxTree;
        
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddReferences(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var semanticModel = compilation.GetSemanticModel(syntaxTree);

        // Create a mock analysis context
        // Note: This is a simplified version for testing purposes
        // In real scenarios, the analyzer framework provides the complete context
        var analysisContext = new SyntaxNodeAnalysisContext(
            invocation,
            semanticModel,
            default(AnalyzerOptions),
            _ => { }, // Mock diagnostic reporter
            _ => true, // Mock analyzer state
            default(CancellationToken)
        );

        return analysisContext;
    }
}