using System;
using System.Net;
using CleanResult;
using SampleApp2;

namespace SampleApp;


public record RecordA(int Id, string Name);
public record RecordB(int Id, string Name);

public record RecordC(string Id, string Name);
public record RecordD(string Id, string[] Name, int Age);
public record RecordE(string Id, string Name, int Age);

public class SimpleClassA
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class CompoundClassA
{
    public int Id { get; set; }
    public string Name { get; set; }
    public RecordA[] Nested { get; set; }
    
    public PhoneNumber[] PhoneNumbers { get; set; }
}


public class CompoundClassB
{
    public int Id { get; set; }
    public string Name { get; set; }
    public RecordB[] Nested { get; set; }
    public SampleApp2.PhoneNumber[] PhoneNumbers { get; set; }
    
}

public partial class PartialClassA
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class AfterMappingObjectA
{
    public int Id { get; set; }
    public required string[] Data { get; set; }
    
    public string ExtraProperty { get; set; } = "Extra";
}

public class AfterMappingObjectB
{
    public int Id { get; set; }
    public required string Data { get; set; }
    
    public string ExtraProperty { get; set; } = "Extra";
}

public class Address
{
    public string Street { get; set; }
    public int City { get; set; }
    public string State { get; set; }
    public string ZipCode { get; set; }
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
    public required string Street { get; set; }
    public required int City { get; set; }
    public required string State { get; set; }
    public required string ZipCode { get; set; }
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

public class PhoneNumber : IFormattable
{
    /// <summary>
    /// The country code of the phone number.
    /// </summary>
    public required int CountryCode { get; set; }

    /// <summary>
    /// The phone number in E.164 format.
    /// </summary>
    public required string Number { get; set; }

    /// <inherit />
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        FormattableString formattable = $"+{CountryCode} {Number}";
        return formattable.ToString(formatProvider);
    }

    /// <summary>
    /// Returns the phone number in E.164 format.
    /// </summary>
    /// <returns>Formated phone number</returns>
    public string GetE164()
    {
        return $"+{CountryCode}{Number}";
    }

    /// <summary>
    /// Returns the phone number with all but the last three digits covered with asterisks.
    /// </summary>
    /// <returns>Covered phone number</returns>
    public string GetCovered()
    {
        if (Number.Length <= 4)
        {
            return new string('*', Number.Length);
        }

        var visibleDigits = 3;
        var coveredLength = Number.Length - visibleDigits;
        var coveredPart = new string('*', coveredLength);
        var visiblePart = Number.Substring(coveredLength, visibleDigits);
        return $"+{CountryCode} {coveredPart}{visiblePart}";
    }


    /// <inherit />
    public override string ToString()
    {
        return $"+{CountryCode} {Number}";
    }

    /// <summary>
    /// Finds a phone number in the provided list by its covered representation.
    /// </summary>
    /// <param name="coveredNumber">The covered phone number to search for (e.g., +1 *********123). </param>
    /// <param name="phoneNumbers">The list of phone numbers to search within. </param>
    /// <returns>Phone number</returns>
    public static Result<PhoneNumber> FindInCoveredList(string coveredNumber, PhoneNumber[] phoneNumbers)
    {
        foreach (var phoneNumber in phoneNumbers)
        {
            if (phoneNumber.GetCovered() == coveredNumber)
            {
                return Result.Ok(phoneNumber);
            }
        }

        return Result.Error("Phone number not found in the list.", HttpStatusCode.BadRequest);
    }
}


public record CreateNewRoleCommand(string Name, string Description);

public class Role
{
    public string Description;
    public string Name;
}