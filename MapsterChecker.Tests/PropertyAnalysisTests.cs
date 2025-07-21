using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using MapsterChecker.Analyzer;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MapsterChecker.Tests;

public class PropertyAnalysisTests
{
    [Fact]
    public void PropertyNullableToNonNullable_ShouldReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class Source
{
    public string? Name { get; set; }
    public int Age { get; set; }
}

public class Destination  
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class TestClass
{
    public void TestMethod()
    {
        var source = new Source { Name = ""John"", Age = 30 };
        var result = source.Adapt<Destination>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var propertyNullableDiagnostics = diagnostics.Where(d => d.Id == "MAPSTER001P").ToArray();
        
        Assert.Single(propertyNullableDiagnostics);
        var diagnostic = propertyNullableDiagnostics[0];
        Assert.Contains("Name", diagnostic.GetMessage());
        Assert.Contains("string?", diagnostic.GetMessage());
        Assert.Contains("string", diagnostic.GetMessage());
    }

    [Fact]
    public void CompatibleProperties_ShouldNotReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class Source
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class Destination  
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class TestClass
{
    public void TestMethod()
    {
        var source = new Source { Name = ""John"", Age = 30 };
        var result = source.Adapt<Destination>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var propertyDiagnostics = diagnostics.Where(d => d.Id.EndsWith("P")).ToArray();
        
        Assert.Empty(propertyDiagnostics);
    }

    [Fact]
    public void MissingSourceProperty_ShouldReportDiagnostic()
    {
        const string code = @"
using Mapster;

public class Source
{
    public string Name { get; set; } = string.Empty;
}

public class Destination  
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class TestClass
{
    public void TestMethod()
    {
        var source = new Source { Name = ""John"" };
        var result = source.Adapt<Destination>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var missingPropertyDiagnostics = diagnostics.Where(d => d.Id == "MAPSTER003P").ToArray();
        
        Assert.Single(missingPropertyDiagnostics);
        var diagnostic = missingPropertyDiagnostics[0];
        Assert.Contains("Age", diagnostic.GetMessage());
    }

    [Fact]
    public void NestedObjectMapping_ShouldAnalyzeNestedProperties()
    {
        const string code = @"
using Mapster;

public class Address
{
    public string? Street { get; set; }
    public string City { get; set; } = string.Empty;
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class Person
{
    public string Name { get; set; } = string.Empty;
    public Address? Address { get; set; }
}

public class PersonDto
{
    public string Name { get; set; } = string.Empty;
    public AddressDto Address { get; set; } = new AddressDto();
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Name = ""John"" };
        var result = person.Adapt<PersonDto>();
    }
}";

        var diagnostics = GetDiagnostics(code);
        var propertyDiagnostics = diagnostics.Where(d => d.Id.EndsWith("P")).ToArray();
        
        // Should detect nested property issues
        var streetIssue = propertyDiagnostics.FirstOrDefault(d => d.GetMessage().Contains("Street"));
        Assert.NotNull(streetIssue);
        Assert.Contains("string?", streetIssue.GetMessage());
        Assert.Contains("string", streetIssue.GetMessage());
    }

    [Fact]
    public void NonComplexTypeMapping_ShouldNotTriggerPropertyAnalysis()
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
        var topLevelDiagnostics = diagnostics.Where(d => d.Id == "MAPSTER001").ToArray();
        var propertyDiagnostics = diagnostics.Where(d => d.Id.EndsWith("P")).ToArray();
        
        Assert.Single(topLevelDiagnostics); // Should have top-level diagnostic
        Assert.Empty(propertyDiagnostics);  // Should NOT have property diagnostics
    }

    private static Diagnostic[] GetDiagnostics(string code)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new MetadataReference[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var analyzer = new MapsterAdaptAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var result = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().Result;
        return result.ToArray();
    }
}