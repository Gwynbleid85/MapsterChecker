using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using MapsterChecker.Analyzer;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

public class DebugConfigurationDiscoveryTests
{
    [Fact]
    public void DebugConfigurationDiscovery_ExactSampleAppScenario()
    {
        const string code = @"
using Mapster;

public class Person
{
    public string Id { get; set; }
    public string? Name { get; set; }
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
";

        // Parse the code
        var tree = CSharpSyntaxTree.ParseText(code);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapterConfig).Assembly.Location)
        };
        
        var compilation = CSharpCompilation.Create("TestAssembly", [tree], references);
        var semanticModel = compilation.GetSemanticModel(tree);
        
        // Test configuration discovery
        var registry = new MappingConfigurationRegistry();
        var discovery = new MapsterConfigurationDiscovery(registry);
        
        // Find all invocations
        var root = tree.GetRoot();
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();
        
        // Test each invocation
        foreach (var invocation in invocations)
        {
            var context = CreateAnalysisContext(invocation, semanticModel);
            discovery.DiscoverConfiguration(context);
        }
        
        // Check if configuration was discovered
        var allMappings = registry.GetAllMappings().ToList();
        Assert.NotEmpty(allMappings);
        
        // Find Person -> PersonDto mapping
        var personType = compilation.GetTypeByMetadataName("Person");
        var personDtoType = compilation.GetTypeByMetadataName("PersonDto");
        
        Assert.NotNull(personType);
        Assert.NotNull(personDtoType);
        
        // Check if custom mapping exists
        bool hasCustomMapping = registry.HasCustomMapping(personType, personDtoType);
        Assert.True(hasCustomMapping, "Should have discovered Person -> PersonDto custom mapping");
        
        // Check if property mapping exists for Id
        bool hasIdPropertyMapping = registry.HasPropertyMapping(personType, personDtoType, "Id");
        Assert.True(hasIdPropertyMapping, "Should have discovered Id property mapping");
    }

    /// <summary>
    /// Helper method to create a SyntaxNodeAnalysisContext for testing.
    /// </summary>
    private static SyntaxNodeAnalysisContext CreateAnalysisContext(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Create a mock analysis context
        var analysisContext = new SyntaxNodeAnalysisContext(
            invocation,
            semanticModel,
            default(AnalyzerOptions),
            _ => { }, // Mock diagnostic reporter
            _ => true, // Mock analyzer state
            default(System.Threading.CancellationToken)
        );

        return analysisContext;
    }
}