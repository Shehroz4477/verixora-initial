using BuildingBlocks.Domain;
using PhoneNumbers;

namespace Identity.Application;

/// <summary>
/// Validates and canonicalizes user phone numbers using current international
/// numbering-plan metadata. API clients must submit an E.164 international
/// number; national numbers have no trustworthy country context at the API.
/// </summary>
public static class InternationalPhoneNumber
{
    private static readonly PhoneNumberUtil Utility = PhoneNumberUtil.GetInstance();

    public static bool TryNormalizeE164(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var candidate = value.Trim();
        if (!candidate.StartsWith("+", StringComparison.Ordinal))
            return false;

        try
        {
            var parsed = Utility.Parse(candidate, null);
            if (!Utility.IsValidNumber(parsed))
                return false;

            var numberType = Utility.GetNumberType(parsed);
            if (numberType is not PhoneNumberType.MOBILE and not PhoneNumberType.FIXED_LINE_OR_MOBILE)
                return false;

            normalized = Utility.Format(parsed, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }

    public static string NormalizeE164(string? value)
    {
        if (!TryNormalizeE164(value, out var normalized))
            throw new DomainException("A valid international E.164 mobile number is required.");

        return normalized;
    }
}
