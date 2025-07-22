using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

/// <summary>
/// Simple test to debug analyzer behavior
/// </summary>
public class CustomMappingWarningTests
{
    [Fact]
    public async Task SimpleStringNullableTest()
    {
        const string testCode = @"
#nullable enable
using Mapster;

class Program
{
    void Test()
    {
        string? nullableString = ""test"";
        string result = nullableString.Adapt<string>();
    }
}";

        // This should show us what the analyzer actually produces
        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        // Add Mapster package reference
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        // Let's see what the analyzer finds - no expected diagnostics means it should pass if no diagnostics are found
        await test.RunAsync();
    }

    [Fact]
    public async Task NullableIntTestWithExpectedDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int? nullableInt = 42;
        var result = nullableInt.Adapt<int>();
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001")
            .WithSpan(9, 22, 9, 46)
            .WithArguments("int?", "int");

        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        // Add Mapster package reference
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }
}