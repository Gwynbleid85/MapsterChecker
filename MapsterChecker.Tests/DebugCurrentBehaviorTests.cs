using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Debug test to understand current analyzer behavior
/// </summary>
public class DebugCurrentBehaviorTests
{
    [Fact]
    public async Task Debug_GuidToString_Direct()
    {
        const string testCode = @"
using System;
using Mapster;

class Program
{
    void Test()
    {
        Guid guid = Guid.NewGuid();
        string result = guid.Adapt<string>();
    }
}";

        // This test will fail and show us what the analyzer actually produces
        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        // Add Mapster package reference
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        await test.RunAsync();
    }

    [Fact]
    public async Task Debug_GuidToString_Property()
    {
        const string testCode = @"
using System;
using Mapster;

class Source
{
    public Guid Id { get; set; }
}

class Destination
{
    public string Id { get; set; } = """";
}

class Program
{
    void Test()
    {
        var source = new Source();
        var dest = source.Adapt<Destination>();
    }
}";

        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        // Add Mapster package reference
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        await test.RunAsync();
    }
}