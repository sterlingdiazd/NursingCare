using System.ComponentModel.DataAnnotations;

namespace NursingCareBackend.Api.Controllers.Admin;

public sealed class UpdateAdminUserRolesRequest
{
  [Required]
  public string[] RoleNames { get; set; } = [];
}
