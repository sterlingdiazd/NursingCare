using System.Text.RegularExpressions;

namespace NursingCareBackend.Application.Identity.Validation;

public static class IdentityInputRules
{
    public const string TextOnlyPattern = @"^[\p{L} ]+$";
    public const string IdentificationNumberPattern = @"^\d{11}$";
    public const string PhonePattern = @"^\d{10}$";
    public const string NumericOnlyPattern = @"^\d+$";

    private static readonly Regex TextOnlyRegex = new(TextOnlyPattern, RegexOptions.Compiled);
    private static readonly Regex IdentificationNumberRegex = new(IdentificationNumberPattern, RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(PhonePattern, RegexOptions.Compiled);
    private static readonly Regex NumericOnlyRegex = new(NumericOnlyPattern, RegexOptions.Compiled);

    public static void EnsureTextOnlyRequired(string value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", parameterName);
        }

        if (!TextOnlyRegex.IsMatch(value.Trim()))
        {
            throw new ArgumentException($"{displayName} must contain letters and spaces only.", parameterName);
        }
    }

    public static void EnsureIdentificationNumber(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identification number is required.", parameterName);
        }

        if (!IdentificationNumberRegex.IsMatch(value.Trim()))
        {
            throw new ArgumentException("Identification number must contain exactly 11 digits.", parameterName);
        }
    }

    public static void EnsurePhone(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Phone is required.", parameterName);
        }

        if (!PhoneRegex.IsMatch(value.Trim()))
        {
            throw new ArgumentException("Phone must contain exactly 10 digits.", parameterName);
        }
    }

    public static void EnsureNumericOnlyOptional(string? value, string parameterName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!NumericOnlyRegex.IsMatch(value.Trim()))
        {
            throw new ArgumentException($"{displayName} must contain digits only.", parameterName);
        }
    }
}
