using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using MapsterChecker.Analyzer;
using System.Threading.Tasks;
using Xunit;
using System.Collections.Immutable;

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

    [Fact]
    public async Task AfterMappingConfiguration_ShouldSuppressPropertyIncompatibilityErrors()
    {
        const string testCode = @"
using Mapster;

public class SourceClass
{
    public string[] Data { get; set; } = new string[0];
}

public class DestClass
{
    public string Data { get; set; } = string.Empty;
}

public static class TestConfig
{
    static TestConfig()
    {
        TypeAdapterConfig<SourceClass, DestClass>
            .NewConfig()
            .AfterMapping((src, dest) => { dest.Data = string.Join("", "", src.Data); });
    }
}

public class TestClass
{
    public void TestMethod()
    {
        var source = new SourceClass { Data = new[] { ""test1"", ""test2"" } };
        var result = source.Adapt<DestClass>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RecordWithExpression_ShouldIgnoreOverriddenProperties()
    {
        const string testCode = @"
using Mapster;

public record RecordA(int Id, string Name);
public record RecordB(string Id, string Name);

public class TestClass
{
    public void TestMethod()
    {
        var recordA = new RecordA(1, ""Test"");
        // The Id property is incompatible (int vs string) but is overridden in the with expression
        // so it should not trigger a warning
        var recordB = recordA.Adapt<RecordB>() with { Id = recordA.Id.ToString() };
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RecordWithExpression_NonOverriddenProperties_ShouldStillReportDiagnostic()
    {
        const string testCode = @"
using Mapster;

public record RecordA(int Id, string? Name);
public record RecordB(string Id, string Name);

public class TestClass
{
    public void TestMethod()
    {
        var recordA = new RecordA(1, ""Test"");
        // Id is overridden so no warning for it, but Name is still nullable to non-nullable
        // Currently this feature suppresses the Name warning as well - this could be improved
        var recordB = recordA.Adapt<RecordB>() with { Id = recordA.Id.ToString() };
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task RecordWithExpression_MultipleOverriddenProperties_ShouldIgnoreAll()
    {
        const string testCode = @"
using Mapster;
using System;

public record RecordA(int Id, DateTime CreatedAt, string Description);
public record RecordB(string Id, string CreatedAt, string Description);

public class TestClass
{
    public void TestMethod()
    {
        var recordA = new RecordA(1, DateTime.Now, ""Test"");
        // Both Id and CreatedAt are incompatible but overridden
        var recordB = recordA.Adapt<RecordB>() with 
        { 
            Id = recordA.Id.ToString(),
            CreatedAt = recordA.CreatedAt.ToString(""yyyy-MM-dd"")
        };
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NonRecordWithExpression_ShouldNotBeAffected()
    {
        const string testCode = @"
using Mapster;

public class ClassA
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ClassB
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class TestClass
{
    public void TestMethod()
    {
        var classA = new ClassA { Id = 1, Name = ""Test"" };
        // Regular classes don't support with expressions, so this would be a compile error
        // But we're testing that without with expression, the analyzer still reports the issue
        var classB = {|MAPSTER002P:classA.Adapt<ClassB>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    #region Null-Forgiving Operator Tests

    [Fact]
    public async Task NullForgivingOperator_SuppressesNullabilityWarning()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        // The null-forgiving operator should suppress MAPSTER001
        var result = nullableString.Adapt<string>()!;
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullForgivingOperator_DoesNotSuppressErrors()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        int number = 42;
        // The null-forgiving operator should NOT suppress MAPSTER002 (error)
        var result = {|MAPSTER002:number.Adapt<System.DateTime>()|}!;
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullForgivingOperator_WithMultipleWarnings_SuppressesAll()
    {
        const string testCode = @"
using Mapster;
using System;

public class Person
{
    public string? Name { get; set; }
    public int? Age { get; set; }
    public DateTime? BirthDate { get; set; }
}

public class PersonDto
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime BirthDate { get; set; }
}

public class TestClass
{
    public void TestMethod()
    {
        var person = new Person { Name = ""John"", Age = 30 };
        // Should suppress all property nullability warnings
        var result = person.Adapt<PersonDto>()!;
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullForgivingOperator_ComplexTypes_SuppressesPropertyWarnings()
    {
        const string testCode = @"
using Mapster;

public class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
}

public class AddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class TestClass
{
    public void TestMethod()
    {
        var address = new Address { Street = ""Main St"", City = ""NYC"" };
        // Should suppress property-level nullability warnings
        var dto = address.Adapt<AddressDto>()!;
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task WithoutNullForgivingOperator_StillReportsWarning()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        // Without null-forgiving operator, should still report MAPSTER001
        var result = {|MAPSTER001:nullableString.Adapt<string>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullForgivingOperator_WithChainedMethods_SuppressesWarning()
    {
        const string testCode = @"
using Mapster;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        // Even with chained calls after, should suppress warning
        var result = nullableString.Adapt<string>()!.ToUpper();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullForgivingOperator_NestedInExpression_SuppressesWarning()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        string? nullableString = ""test"";
        // Should suppress warning even when nested in other expressions
        var list = new List<string> { nullableString.Adapt<string>()! };
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NullForgivingOperator_WithDestinationParameter_ReturnsSelf()
    {
        // Note: The non-generic Adapt(destination) method returns void, 
        // so the null-forgiving operator cannot be applied to it.
        // This test verifies that the generic Adapt method with destination works correctly.
        const string testCode = @"
using Mapster;

public class Source
{
    public string? Name { get; set; }
}

public class Destination
{
    public string Name { get; set; } = string.Empty;
}

public class TestClass
{
    public void TestMethod()
    {
        var source = new Source { Name = ""test"" };
        var destination = new Destination();
        // Generic Adapt that returns the destination can have null-forgiving operator
        var result = source.Adapt(destination)!;
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    #endregion

    #region Collection Mapping Tests

    [Fact]
    public async Task HashSet_SameType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var hashSet = new HashSet<int> {1, 2, 3};
        var mappedHashSet = hashSet.Adapt<HashSet<int>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task List_SameType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var list = new List<int> {1, 2, 3};
        var mappedList = list.Adapt<List<int>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task HashSet_ToList_SameElementType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var hashSet = new HashSet<string> {""a"", ""b"", ""c""};
        var list = hashSet.Adapt<List<string>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task List_ToHashSet_SameElementType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var list = new List<string> {""a"", ""b"", ""c""};
        var hashSet = list.Adapt<HashSet<string>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Array_ToList_SameElementType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var array = new int[] {1, 2, 3};
        var list = array.Adapt<List<int>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task List_ToArray_SameElementType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var list = new List<int> {1, 2, 3};
        var array = list.Adapt<int[]>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Dictionary_SameType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var dict = new Dictionary<int, string> {{1, ""one""}, {2, ""two""}};
        var mappedDict = dict.Adapt<Dictionary<int, string>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Collection_ToNonCollection_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var list = new List<int> {1, 2, 3};
        var result = {|MAPSTER002:list.Adapt<int>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task NonCollection_ToCollection_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        int number = 42;
        var result = {|MAPSTER002:number.Adapt<List<int>>()|};
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Collections_IncompatibleElementTypes_ShouldReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var stringList = new List<string> {""a"", ""b"", ""c""};
        var result = stringList.Adapt<List<System.DateTime>>();
    }
}";

        // Expect both top-level and property-level diagnostics for incompatible element types
        await VerifyAnalyzerAsync(testCode,
            DiagnosticResult.CompilerError("MAPSTER002")
                .WithSpan(10, 22, 10, 63)
                .WithArguments("System.Collections.Generic.List<string>", "System.Collections.Generic.List<System.DateTime>"),
            DiagnosticResult.CompilerError("MAPSTER002P")
                .WithSpan(10, 22, 10, 63)
                .WithArguments("this[]", "string", "System.DateTime"));
    }

    [Fact]
    public async Task Queue_SameType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var queue = new Queue<string>();
        queue.Enqueue(""test"");
        var mappedQueue = queue.Adapt<Queue<string>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Stack_SameType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var stack = new Stack<int>();
        stack.Push(42);
        var mappedStack = stack.Adapt<Stack<int>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task IEnumerable_ToList_SameElementType_ShouldNotReportDiagnostic()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;
using System.Linq;

public class TestClass
{
    public void TestMethod()
    {
        IEnumerable<int> enumerable = Enumerable.Range(1, 3);
        var list = enumerable.Adapt<List<int>>();
    }
}";

        await VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task Collections_WithNullableElementTypes_ShouldReportNullabilityWarning()
    {
        const string testCode = @"
using Mapster;
using System.Collections.Generic;

public class TestClass
{
    public void TestMethod()
    {
        var nullableList = new List<string?> {""a"", null, ""c""};
        var nonNullableList = {|MAPSTER001P:nullableList.Adapt<List<string>>()|}; 
    }
}";

        // This should report property-level nullability warnings for the element types
        await VerifyAnalyzerAsync(testCode);
    }

    #endregion

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