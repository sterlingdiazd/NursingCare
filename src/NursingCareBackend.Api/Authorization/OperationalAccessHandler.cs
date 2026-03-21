using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NursingCareBackend.Application.Identity.Repositories;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Authorization;

public sealed class OperationalAccessHandler : AuthorizationHandler<OperationalAccessRequirement>
{
    private readonly IUserRepository _userRepository;

    public OperationalAccessHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationalAccessRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        if (!context.User.IsInRole(SystemRoles.Nurse))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user?.NurseProfile?.IsActive == true)
        {
            context.Succeed(requirement);
        }
    }
}
