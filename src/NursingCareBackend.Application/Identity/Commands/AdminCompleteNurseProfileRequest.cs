using System.ComponentModel.DataAnnotations;
using NursingCareBackend.Application.Identity.Validation;

namespace NursingCareBackend.Application.Identity.Commands;

public sealed record AdminCompleteNurseProfileRequest(
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
    DateOnly HireDate,
    [Required]
    string Specialty,
    [RegularExpression(IdentityInputRules.NumericOnlyPattern, ErrorMessage = "License ID must contain digits only.")]
    string? LicenseId,
    [Required]
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "Bank name must contain letters and spaces only.")]
    string BankName,
    [RegularExpression(IdentityInputRules.NumericOnlyPattern, ErrorMessage = "Account number must contain digits only.")]
    string? AccountNumber,
    [Required]
    string Category
);
