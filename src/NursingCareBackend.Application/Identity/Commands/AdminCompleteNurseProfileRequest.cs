using System.ComponentModel.DataAnnotations;
using NursingCareBackend.Application.Identity.Validation;

namespace NursingCareBackend.Application.Identity.Commands;

public sealed record AdminCompleteNurseProfileRequest(
    [Required]
    [StringLength(150)]
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "El nombre solo puede contener letras y espacios.")]
    string Name,
    [Required]
    [StringLength(150)]
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "El apellido solo puede contener letras y espacios.")]
    string LastName,
    [Required]
    [StringLength(11, MinimumLength = 11)]
    [RegularExpression(IdentityInputRules.IdentificationNumberPattern, ErrorMessage = "La cedula debe tener exactamente 11 digitos.")]
    string IdentificationNumber,
    [Required]
    [StringLength(10, MinimumLength = 10)]
    [RegularExpression(IdentityInputRules.PhonePattern, ErrorMessage = "El telefono debe tener exactamente 10 digitos.")]
    string Phone,
    [Required]
    [EmailAddress]
    string Email,
    DateOnly HireDate,
    [Required]
    string Specialty,
    [RegularExpression(IdentityInputRules.NumericOnlyPattern, ErrorMessage = "La licencia solo puede contener digitos.")]
    string? LicenseId,
    [Required]
    [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "El banco solo puede contener letras y espacios.")]
    string BankName,
    [RegularExpression(IdentityInputRules.NumericOnlyPattern, ErrorMessage = "El numero de cuenta solo puede contener digitos.")]
    string? AccountNumber,
    [Required]
    string Category
);
