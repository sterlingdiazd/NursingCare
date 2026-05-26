namespace NursingCareBackend.Application.AdminPortal.Auditing;

public static class AdminAuditActions
{
  public const string AdminAccountCreated = "AdminAccountCreated";
  public const string AdminRoleGranted = "AdminRoleGranted";
  public const string ClientProfileCreatedByAdmin = "ClientProfileCreatedByAdmin";
  public const string ClientProfileUpdated = "ClientProfileUpdated";
  public const string ClientActiveStateChanged = "ClientActiveStateChanged";
  public const string NurseProfileCreatedByAdmin = "NurseProfileCreatedByAdmin";
  public const string NurseProfileCompleted = "NurseProfileCompleted";
  public const string NurseProfileUpdated = "NurseProfileUpdated";
  public const string NurseOperationalAccessChanged = "NurseOperationalAccessChanged";
  public const string CatalogEntryUpdated = "CatalogEntryUpdated";

  // Money flow (payroll / nurse payment)
  public const string ConfirmNursePayment = "ConfirmNursePayment";
  public const string NursePaymentFailed = "NursePaymentFailed";
  public const string NursePaymentReversed = "NursePaymentReversed";
  public const string ClosePeriod = "ClosePeriod";

  // Money flow (client payment)
  public const string RejectPaymentProof = "RejectPaymentProof";
}
