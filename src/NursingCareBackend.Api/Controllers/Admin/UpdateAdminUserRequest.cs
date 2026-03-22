using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.Admin;

public sealed class UpdateAdminUserRequest
{
  [Required]
  public string Name { get; set; } = string.Empty;

  [Required]
  public string LastName { get; set; } = string.Empty;

  [Required]
  public string IdentificationNumber { get; set; } = string.Empty;

  [Required]
  public string Phone { get; set; } = string.Empty;

  [Required]
  public string Email { get; set; } = string.Empty;
}
