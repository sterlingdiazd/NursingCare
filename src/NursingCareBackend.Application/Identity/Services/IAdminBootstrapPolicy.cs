namespace NursingCareBackend.Application.Identity.Services;

public interface IAdminBootstrapPolicy
{
  Task EnsureSetupAdminAllowedAsync(CancellationToken cancellationToken = default);
}
