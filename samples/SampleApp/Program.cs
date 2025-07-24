using System;
using System.Text.Json;
using CleanResult;
using Mapster;

namespace SampleApp;


public class Address
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}

public class Person
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public DateTime? BirthDate { get; set; }
    
    public Address Address { get; set; }
    
}

public class AddressDto
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}

public class PersonDto
{
    public string Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime? BirthDate { get; set; }
    
    public AddressDto Address { get; set; }
}

// Test classes for "no common properties" bug fix
public class TypeWithNoCommonProps
{
    public string PropertyA { get; set; } = "A";
    public int PropertyB { get; set; } = 1;
}

public class AnotherTypeWithNoCommonProps
{
    public string PropertyX { get; set; } = "X";
    public int PropertyY { get; set; } = 2;
}

public class Program
{
    public static void Main()
    {
        // Configure Mapster mappings
        MapsterConfig.Configure();
        
        var person = new Person
        {
            Id = Guid.NewGuid(),
            Name = "John Doe",
            Age = 30,
            BirthDate = DateTime.Now.AddYears(-30)
        };

        Console.WriteLine("=== MapsterChecker Sample Application ===");
        Console.WriteLine();

        Console.WriteLine("Testing valid mappings (should not trigger warnings):");
        TestValidMappings(person);

        Console.WriteLine();
        Console.WriteLine("Testing problematic mappings (should trigger MAPSTER001 warnings):");
        TestProblematicMappings(person);

        Console.WriteLine();
        Console.WriteLine("Testing incompatible mappings (should trigger MAPSTER002 errors):");
        TestIncompatibleMappings();
    }
    
    private static void TestValidMappings(Person person)
    {
        var dto = person.Adapt<PersonDto>();
        
        Console.WriteLine($"✅ Person to PersonDto: {JsonSerializer.Serialize(dto)}");

        var nonNullableString = "test";
        var nullableResult = nonNullableString.Adapt<string?>();
        Console.WriteLine($"✅ Non-nullable to nullable string: {nullableResult}");

        int number = 42;
        long longNumber = number.Adapt<long>();
        Console.WriteLine($"✅ Int to long conversion: {longNumber}");
    }

    private static void TestProblematicMappings(Person person)
    {
        string? nullableName = person.Name;
        var nonNullableName = nullableName.Adapt<string>();
        Console.WriteLine($"⚠️ Nullable to non-nullable mapping: {nonNullableName}");

        DateTime? nullableBirthDate = person.BirthDate;
        var nonNullableBirthDate = nullableBirthDate.Adapt<DateTime>();
        Console.WriteLine($"⚠️ Nullable DateTime to non-nullable: {nonNullableBirthDate}");

        int? nullableAge = person.Age;
        var nonNullableAge = nullableAge.Adapt<int>();
        Console.WriteLine($"⚠️ Nullable int to non-nullable: {nonNullableAge}");
    }


    private static void CleanResultMappings()
    {
        string dataString = "An error occurred";
        // Should trigger error
        var errorResult = dataString.Adapt<Result>();
        
        var mappedDataString = errorResult.Adapt<string>(); 
        
        // Should trigger error
        var errorResultWithData = dataString.Adapt<Result<string>>();
    }

    
    private static void TestIncompatibleMappings()
    {
        Console.WriteLine("The following should now trigger MAPSTER002 errors:");
        
        // Test value type to reference type mapping (Bug fix #1)
        int number = 42;
        // var stringResult = number.Adapt<string>();
        Console.WriteLine($"❌ Int to string (value to reference type): {stringResult}");

        // Test reference type to value type mapping (Bug fix #1)  
        string text = "hello";
        // var intResult = text.Adapt<int>();
        Console.WriteLine($"❌ String to int (reference to value type): {intResult}");

        // Test types with no common properties (Bug fix #2)
        var noCommonProps = new TypeWithNoCommonProps().Adapt<AnotherTypeWithNoCommonProps>();
        Console.WriteLine($"❌ Types with no common properties: {noCommonProps}");
        
        Console.WriteLine();
        Console.WriteLine("These should also trigger errors:");
        Console.WriteLine("- int to DateTime, string to Guid, etc.");
        
        // Uncommenting these lines will cause MAPSTER002 build errors:
        // var dateTime = number.Adapt<DateTime>();
        // var guid = text.Adapt<Guid>();
    }
}