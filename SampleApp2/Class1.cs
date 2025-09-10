using System.Net;
using CleanResult;

namespace SampleApp2;



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