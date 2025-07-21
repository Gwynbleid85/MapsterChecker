using System;
using Mapster;

namespace SampleApp;

public class Person
{
    public string? Name { get; set; }
    public int Age { get; set; }
    public DateTime? BirthDate { get; set; }
}

public class PersonDto
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime BirthDate { get; set; }
}

public class Program
{
    public static void Main()
    {
        var person = new Person
        {
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
        Console.WriteLine($"✅ Person to PersonDto: {dto.Name}, Age: {dto.Age}");

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

    private static void TestIncompatibleMappings()
    {
        Console.WriteLine("The following would cause MAPSTER002 errors and prevent compilation:");
        Console.WriteLine("- int number = 42; var dateTime = number.Adapt<DateTime>();");
        Console.WriteLine("- string text = \"hello\"; var guid = text.Adapt<Guid>();");
        Console.WriteLine("These lines are commented out to allow the sample to run.");
        
        // Uncommenting these lines will cause MAPSTER002 build errors:
        // int number = 42;
        // var dateTime = number.Adapt<DateTime>();
        // Console.WriteLine($"❌ Int to DateTime (incompatible): {dateTime}");

        // string text = "hello";
        // var guid = text.Adapt<Guid>();
        // Console.WriteLine($"❌ String to Guid (incompatible): {guid}");
    }
}