using System.Threading.Tasks;
using MapsterChecker.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace MapsterChecker.Tests;

public class DebugTest
{
    [Fact]
    public async Task SimpleTest()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        var result = nullableString.Adapt<string>();
    }
}";

        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        // This should produce MAPSTER001 warning
        await test.RunAsync();
    }
}