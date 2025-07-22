using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

public class MapsterAdaptAnalyzerTests
{
    [Fact]
    public async Task NullableToNonNullable_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        var result = {|MAPSTER001:nullableString.Adapt<string>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001")
            .WithSpan(9, 22, 9, 52)
            .WithArguments("string?", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NonNullableToNullable_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string nonNullableString = ""test"";
        var result = nonNullableString.Adapt<string?>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IncompatibleTypes_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int number = 42;
        var result = {|MAPSTER002:number.Adapt<System.DateTime>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerError("MAPSTER002")
            .WithSpan(9, 22, 9, 50)
            .WithArguments("int", "System.DateTime");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task CompatibleTypes_ShouldNotReportDiagnostic()
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
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Name = ""John"", Age = 30 };
        var result = person.Adapt<PersonDto>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task AdaptWithDestinationParameter_NullableToNonNullable_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        string destination = string.Empty;
        {|MAPSTER001:nullableString.Adapt(destination)|};
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001")
            .WithSpan(10, 9, 10, 43)
            .WithArguments("string?", "string");

        await VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task NonMapsterAdaptCall_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
public class TestClass
{
    public string Adapt<T>() => ""test"";
    
    public void TestMethod()
    {
        var result = Adapt<int>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NumericTypeConversion_ShouldNotReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int number = 42;
        var result = number.Adapt<long>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullableValueTypeToNonNullable_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int? nullableInt = 42;
        var result = {|MAPSTER001:nullableInt.Adapt<int>()|};
    }
}";

        var expected = DiagnosticResult
            .CompilerWarning("MAPSTER001")
            .WithSpan(9, 22, 9, 46)
            .WithArguments("int?", "int");

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