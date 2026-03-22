namespace NursingCareBackend.Application.AdminPortal.Auditing;

public static class AdminAuditActions
{
  public const string AdminAccountCreated = "AdminAccountCreated";
  public const string AdminRoleGranted = "AdminRoleGranted";
  public const string NurseProfileCreatedByAdmin = "NurseProfileCreatedByAdmin";
  public const string NurseProfileCompleted = "NurseProfileCompleted";
  public const string NurseProfileUpdated = "NurseProfileUpdated";
  public const string NurseOperationalAccessChanged = "NurseOperationalAccessChanged";
}
