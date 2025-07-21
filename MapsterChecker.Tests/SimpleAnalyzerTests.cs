using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MapsterChecker.Analyzer;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

public class SimpleAnalyzerTests
{
    [Fact]
    public void NullableToNonNullable_ShouldReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        var result = nullableString.Adapt<string>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var mapster001 = diagnostics.FirstOrDefault(d => d.Id == "MAPSTER001");
        
        Assert.NotNull(mapster001);
        Assert.Contains("string?", mapster001.GetMessage());
        Assert.Contains("string", mapster001.GetMessage());
    }

    [Fact]
    public void NonNullableToNullable_ShouldNotReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string nonNullableString = ""test"";
        var result = nonNullableString.Adapt<string?>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var mapster001 = diagnostics.FirstOrDefault(d => d.Id == "MAPSTER001");
        
        Assert.Null(mapster001);
    }

    [Fact]
    public void IncompatibleTypes_ShouldReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int number = 42;
        var result = number.Adapt<System.DateTime>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var mapster002 = diagnostics.FirstOrDefault(d => d.Id == "MAPSTER002");
        
        Assert.NotNull(mapster002);
        Assert.Contains("int", mapster002.GetMessage());
        Assert.Contains("System.DateTime", mapster002.GetMessage());
    }

    [Fact]
    public void CompatibleTypes_ShouldNotReportDiagnostic()
    {
        const string code = @"
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

        var diagnostics = GetDiagnostics(code);
        var mapsterDiagnostics = diagnostics.Where(d => d.Id.StartsWith("MAPSTER")).ToList();
        
        Assert.Empty(mapsterDiagnostics);
    }

    [Fact]
    public void NullableValueTypeToNonNullable_ShouldReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int? nullableInt = 42;
        var result = nullableInt.Adapt<int>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var mapster001 = diagnostics.FirstOrDefault(d => d.Id == "MAPSTER001");
        
        Assert.NotNull(mapster001);
        Assert.Contains("int?", mapster001.GetMessage());
        Assert.Contains("int", mapster001.GetMessage());
    }

    [Fact]
    public void NonMapsterAdaptCall_ShouldNotReportDiagnostic()
    {
        const string code = @"
public class TestClass
{
    public string Adapt<T>() => ""test"";
    
    public void TestMethod()
    {
        var result = Adapt<int>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var mapsterDiagnostics = diagnostics.Where(d => d.Id.StartsWith("MAPSTER")).ToList();
        
        Assert.Empty(mapsterDiagnostics);
    }

    private static Diagnostic[] GetDiagnostics(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new MapsterAdaptAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var result = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
        return result.ToArray();
    }
}