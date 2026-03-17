using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.Services;

namespace NursingCareBackend.Api.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <param name="request">Registration details (email, password, confirmPassword)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication token and user details</returns>
    /// <response code="200">Registration successful</response>
    /// <response code="400">Invalid input or user already exists</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authenticationService.RegisterAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials (email, password)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication token and user details</returns>
    /// <response code="200">Login successful</response>
    /// <response code="400">Invalid credentials</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authenticationService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Assign a role to an existing user (Admin only)
    /// </summary>
    /// <param name="request">User ID and role name to assign</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">Role assigned successfully</response>
    /// <response code="400">Invalid input or role already assigned</response>
    /// <response code="401">Unauthorized - not authenticated</response>
    /// <response code="403">Forbidden - only admins can assign roles</response>
    [HttpPost("assign-role")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(
        [FromBody] AssignRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return BadRequest(new { error = "Invalid user ID format." });
        }

        await _authenticationService.AssignRoleAsync(userId, request.RoleName, cancellationToken);
        return Ok(new { message = $"Role '{request.RoleName}' assigned successfully." });
    }

    /// <summary>
    /// Create the first admin user for initial setup (Security: Only call once!)
    /// </summary>
    /// <param name="request">Admin email and password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication token for the new admin</returns>
    /// <response code="200">Admin created successfully</response>
    /// <response code="400">Invalid input or admin already exists</response>
    [HttpPost("setup-admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetupAdmin(
        [FromBody] AdminSetupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authenticationService.CreateAdminAsync(request, cancellationToken);
            return Ok(new
            {
                message = "Admin user created successfully. Please save this token and disable the /setup-admin endpoint in production.",
                data = response
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
