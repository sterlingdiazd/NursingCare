using System.ComponentModel.DataAnnotations;
using NursingCareBackend.Application.Identity.Validation;

namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed record AdminCreateClientRequest(
  [Required(ErrorMessage = "El nombre es obligatorio.")]
  [StringLength(150, ErrorMessage = "El nombre no puede superar 150 caracteres.")]
  [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "El nombre solo acepta letras y espacios.")]
  string Name,
  [Required(ErrorMessage = "El apellido es obligatorio.")]
  [StringLength(150, ErrorMessage = "El apellido no puede superar 150 caracteres.")]
  [RegularExpression(IdentityInputRules.TextOnlyPattern, ErrorMessage = "El apellido solo acepta letras y espacios.")]
  string LastName,
  [Required(ErrorMessage = "La cedula es obligatoria.")]
  [StringLength(11, MinimumLength = 11, ErrorMessage = "La cedula debe tener exactamente 11 digitos.")]
  [RegularExpression(IdentityInputRules.IdentificationNumberPattern, ErrorMessage = "La cedula debe tener exactamente 11 digitos.")]
  string IdentificationNumber,
  [Required(ErrorMessage = "El telefono es obligatorio.")]
  [StringLength(10, MinimumLength = 10, ErrorMessage = "El telefono debe tener exactamente 10 digitos.")]
  [RegularExpression(IdentityInputRules.PhonePattern, ErrorMessage = "El telefono debe tener exactamente 10 digitos.")]
  string Phone,
  [Required(ErrorMessage = "El correo es obligatorio.")]
  [EmailAddress(ErrorMessage = "Ingresa un correo valido.")]
  string Email,
  [Required(ErrorMessage = "La contrasena es obligatoria.")]
  string Password,
  [Required(ErrorMessage = "Debes confirmar la contrasena.")]
  string ConfirmPassword);
