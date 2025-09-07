using System;
using System.Collections.Generic;
using System.Text.Json;
using CleanResult;
using Mapster;

namespace SampleApp;

public class MappingTests
{
   
    private static void TestNullableMappings()
    {
        string? nullableString = null;
        var nonNullableString = nullableString.Adapt<string>();
        
        var address = new Address
        {
            Street = "123 Main St",
            City = 456,
            State = "CA",
            ZipCode = "90210"
        };
        
        var addressDto = address.Adapt<AddressDto>();
        
        Console.WriteLine($"⚠️ Nullable to non-nullable string: {nonNullableString}");
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
        // Console.WriteLine($"❌ Int to string (value to reference type): {stringResult}");

        // Test reference type to value type mapping (Bug fix #1)  
        string text = "hello";
        // var intResult = text.Adapt<int>();
        // Console.WriteLine($"❌ String to int (reference to value type): {intResult}");

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
    
    private static void TestAfterMapping()
    {
        var objA = new AfterMappingObjectA
        {
            Id = 1,
            Data = ["one", "two", "three"]
        };

        int[] asdf = [];

        // var _asdff = asdf.Adapt<string[]>();

        var objB = objA.Adapt<AfterMappingObjectB>();
        Console.WriteLine($"AfterMappingObjectB Data: {objB.Data}");
    }
    
    private static void TestWithKeyword()
    {
        
        var recordA = new RecordA(1, "Test");
        
        var recordC = recordA.Adapt<RecordC>() with {Id = recordA.Id.ToString()};

        var recordD = recordA.Adapt<RecordD>() with {Id = recordA.Id.ToString(), Name = [recordA.Name]};
        
        var recordE = recordA.Adapt<RecordE>() with {Id = recordA.Id.ToString(), Age = 42};
    }

    private static void TestPartialClass()
    {
        var partialClass = new PartialClassA
        {
            Id = 1,
            Name = "Test"
        };
        
        var recordA = partialClass.Adapt<RecordA>();
    }


    private static void TestWarningSuppression()
    {

        var address = new Address();
        
        var addressDto = address.Adapt<AddressDto>()!;

    }
    
    private static void TestCollections()
    {
        var listOfInts = new List<int> {1, 2, 3};
        var listOfIntsMapped = listOfInts.Adapt<List<int>>();
        
        var hashSetOfInts = new HashSet<int> {1, 2, 3};
        var hashSetOfIntsMapped = hashSetOfInts.Adapt<HashSet<int>>();
        
        var dictionaryOfIntToString = new Dictionary<int, string>
        {
            {1, "one"},
            {2, "two"},
            {3, "three"}
        };
        var dictionaryOfIntToStringMapped = dictionaryOfIntToString.Adapt<Dictionary<int, string>>();
    }

    private static void TestCollectionsComplexMapping()
    {
        var recordA = new RecordA(1, "Test");
        var hashSetOfRecordA = new HashSet<RecordA> {recordA};
        hashSetOfRecordA.Adapt<HashSet<RecordB>>();
        
        // Should throw error
        hashSetOfRecordA.Adapt<HashSet<RecordD>>();
    }


    private static void TestComplexMapping()
    {
        var compoundA = new CompoundClassA
        {
            Id = 1,
            Name = "Test",
            Nested = [new RecordA(2, "Nested")]
            ,
            PhoneNumbers = [
                new PhoneNumberA { CountryCode = 1, Number = "1234567890" },
                new PhoneNumberA { CountryCode = 44, Number = "9876543210" }
            ]
            
        };
        
        var compoundB = compoundA.Adapt<CompoundClassB>();
    }
}