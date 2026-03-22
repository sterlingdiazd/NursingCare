using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Application.Identity.Services;

namespace NursingCareBackend.Infrastructure.Authentication;

public sealed class AdminBootstrapPolicy : IAdminBootstrapPolicy
{
  private readonly IUserRepository _userRepository;
  private readonly IConfiguration _configuration;
  private readonly AdminBootstrapOptions _options;

  public AdminBootstrapPolicy(
    IUserRepository userRepository,
    IConfiguration configuration,
    IOptions<AdminBootstrapOptions> options)
  {
    _userRepository = userRepository;
    _configuration = configuration;
    _options = options.Value;
  }

  public async Task EnsureSetupAdminAllowedAsync(CancellationToken cancellationToken = default)
  {
    if (await _userRepository.AnyAdminExistsAsync(cancellationToken))
    {
      throw new InvalidOperationException(
        "Bootstrap admin setup is no longer available because an admin account already exists.");
    }

    if (string.Equals(_configuration["environment"], "Production", StringComparison.OrdinalIgnoreCase)
      && !_options.AllowInProduction)
    {
      throw new InvalidOperationException(
        "Bootstrap admin setup is disabled in production. Use the Admin Portal after the initial installation flow.");
    }
  }
}
