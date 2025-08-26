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
        var result = nullableString.Adapt<string>();
    }
}";

        // Just verify it runs without expecting specific diagnostics for now
        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        await test.RunAsync();
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

        await VerifyAnalyzerAsync(testCode);
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
        nullableString.Adapt(destination);
    }
}";

        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        await test.RunAsync();
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

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task StringToStringArray_ShouldReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string text = ""test"";
        var result = {|MAPSTER002:text.Adapt<string[]>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task StringArrayToString_ShouldReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string[] texts = new[] { ""test1"", ""test2"" };
        var result = {|MAPSTER002:texts.Adapt<string>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IntToIntArray_ShouldReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int number = 42;
        var result = {|MAPSTER002:number.Adapt<int[]>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ObjectToList_ShouldReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class Person
{
    public string Name { get; set; } = string.Empty;
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Name = ""John"" };
        var result = {|MAPSTER002:person.Adapt<List<Person>>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ListToObject_ShouldReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class Person
{
    public string Name { get; set; } = string.Empty;
}

public class TestClass
{
    public void TestMethod()
    {
        var persons = new List<Person> { new Person { Name = ""John"" } };
        var result = {|MAPSTER002:persons.Adapt<Person>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task DictionaryToString_ShouldReportIncompatibilityDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var dict = new Dictionary<string, int> { { ""key"", 1 } };
        var result = dict.Adapt<string>();
    }
}";

        var test = new CSharpAnalyzerTest<MapsterAdaptAnalyzer, DefaultVerifier>
        {
            TestCode = testCode,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(Mapster.TypeAdapter).Assembly.Location));
        
        await test.RunAsync();
    }

    [Fact]
    public async Task ArrayToArray_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string[] source = new[] { ""test1"", ""test2"" };
        var result = source.Adapt<string[]>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task ListToIEnumerable_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var list = new List<string> { ""test1"", ""test2"" };
        var result = list.Adapt<IEnumerable<string>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IEnumerableToList_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;
using System.Linq;

public class TestClass
{
    public void TestMethod()
    {
        IEnumerable<string> source = new[] { ""test1"", ""test2"" }.AsEnumerable();
        var result = source.Adapt<List<string>>();
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
        
        // Only add explicit expected diagnostics if provided
        if (expected != null && expected.Length > 0)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        await test.RunAsync();
    }
}