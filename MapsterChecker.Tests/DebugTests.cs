using Mapster;
using MapsterChecker.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace MapsterCheck.Tests;

public class DebugTests
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

    [Fact]
    public async Task NoWarningTest()
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Properties = new UserProperties
            {
                Value = "Test",
                Age = 30
            }
        };
        
        var userDto = user.Adapt<UserDto>();

        userDto.Properties.Age = 42;
        
        Assert.Equal(42, userDto.Properties.Age);
        Assert.Equal("Test", userDto.Properties.Value);
        Assert.Equal(30, user.Properties.Age);
    }
}

public class User
{
    public Guid Id { get; set; }
    public required UserProperties Properties { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public required UserProperties Properties { get; set; }
}

public class UserProperties
{
    public required string Value { get; set; }
    public int Age { get; set; }
}