using System.ComponentModel.DataAnnotations;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Application.Identity.Validation;

namespace NursingCareBackend.Application.Identity.Commands;

public sealed record RegisterRequest(
    [Required]
    [StringLength(150)]
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "Name must contain letters and spaces only.")]
    string Name,
    [Required]
    [StringLength(150)]
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "Last name must contain letters and spaces only.")]
    string LastName,
    [Required]
    [StringLength(11, MinimumLength = 11)]
    [RegularExpression(IdentityInputRules.IdentificationNumberPattern, ErrorMessage = "Identification number must contain exactly 11 digits.")]
    string IdentificationNumber,
    [Required]
    [StringLength(10, MinimumLength = 10)]
    [RegularExpression(IdentityInputRules.PhonePattern, ErrorMessage = "Phone must contain exactly 10 digits.")]
    string Phone,
    [Required]
    [EmailAddress]
    string Email,
    [Required]
    string Password,
    [Required]
    string ConfirmPassword,
    DateOnly? HireDate = null,
    string? Specialty = null,
    [RegularExpression(IdentityInputRules.NumericOnlyPattern, ErrorMessage = "License ID must contain digits only.")]
    string? LicenseId = null,
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "Bank name must contain letters and spaces only.")]
    string? BankName = null,
    [RegularExpression(IdentityInputRules.NumericOnlyPattern, ErrorMessage = "Account number must contain digits only.")]
    string? AccountNumber = null,
    UserProfileType ProfileType = UserProfileType.Client
);
