using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Application.Identity.Users;

public static class UserAccountStateEvaluator
{
  public static bool RequiresProfileCompletion(User user)
    => !string.IsNullOrWhiteSpace(user.GoogleSubjectId)
        && (string.IsNullOrWhiteSpace(user.Name)
            || string.IsNullOrWhiteSpace(user.LastName)
            || string.IsNullOrWhiteSpace(user.IdentificationNumber)
            || string.IsNullOrWhiteSpace(user.Phone));

  public static bool RequiresAdminReview(User user)
    => user.ProfileType == UserProfileType.NURSE && user.NurseProfile?.IsActive != true;

  public static bool RequiresManualIntervention(User user)
    => !user.UserRoles.Any()
        || (user.ProfileType == UserProfileType.NURSE && user.NurseProfile is null)
        || (user.ProfileType == UserProfileType.CLIENT && user.ClientProfile is null);
}
