using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;

namespace NursingCareBackend.Infrastructure.Authentication;

public sealed class AdminBootstrapPolicy : IAdminBootstrapPolicy
{
  private readonly IUserRepository _userRepository;
  private readonly IHostEnvironment _environment;
  private readonly AdminBootstrapOptions _options;

  public AdminBootstrapPolicy(
    IUserRepository userRepository,
    IHostEnvironment environment,
    IOptions<AdminBootstrapOptions> options)
  {
    _userRepository = userRepository;
    _environment = environment;
    _options = options.Value;
  }

  public async Task EnsureSetupAdminAllowedAsync(CancellationToken cancellationToken = default)
  {
    // Production check must run FIRST: the endpoint is unconditionally blocked in production
    // regardless of whether an admin account exists.
    if (_environment.IsProduction() && !_options.AllowInProduction)
    {
      throw new InvalidOperationException(
        "Bootstrap admin setup is disabled in production. Use the Admin Portal after the initial installation flow.");
    }

    if (await _userRepository.AnyAdminExistsAsync(cancellationToken))
    {
      throw new InvalidOperationException(
        "Bootstrap admin setup is no longer available because an admin account already exists.");
    }
  }
}
