using System.ComponentModel.DataAnnotations;
using NursingCareBackend.Application.Identity.Validation;

namespace NursingCareBackend.Application.Identity.Commands;

public sealed record CompleteProfileRequest(
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
    string Phone
);
