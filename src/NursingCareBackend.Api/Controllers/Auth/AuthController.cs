using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.Identity.Commands;
using NursingCareBackend.Application.Identity.OAuth;
using NursingCareBackend.Application.Identity.Services;
using NursingCareBackend.Application.Identity.Responses;
using NursingCareBackend.Infrastructure.Authentication;

namespace NursingCareBackend.Api.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IGoogleOAuthClient _googleOAuthClient;
    private readonly GoogleOAuthOptions _googleOAuthOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authenticationService,
        IGoogleOAuthClient googleOAuthClient,
        IOptions<GoogleOAuthOptions> googleOAuthOptions,
        ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService;
        _googleOAuthClient = googleOAuthClient;
        _googleOAuthOptions = googleOAuthOptions.Value;
        _logger = logger;
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

    [HttpPost("complete-profile")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteProfile(
        [FromBody] CompleteProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ResolveAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Profile completion failed",
                Detail = "Authenticated user identifier is missing from the access token.",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var response = await _authenticationService.CompleteProfileAsync(
                userId.Value,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Profile completion failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
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
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authenticationService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Invalid email or password.")
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Login failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Login failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// Redirect the browser to Google OAuth2.
    /// </summary>
    [HttpGet("google/start")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult StartGoogleLogin([FromQuery] string? target)
    {
        try
        {
            var redirectTarget = NormalizeRedirectTarget(target);
            return Redirect(_googleOAuthClient.BuildAuthorizationUrl(redirectTarget));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Google OAuth is not configured",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// Handle Google OAuth2 callback and redirect back to the SPA with auth details.
    /// </summary>
    [HttpGet("google/callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var redirectTarget = NormalizeRedirectTarget(state);

        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogWarning("Google OAuth returned an error: {OAuthError}", error);
            return Redirect(BuildFailureRedirect(
                "Error al iniciar sesión con Google. Por favor intenta de nuevo.",
                redirectTarget));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            _logger.LogWarning("Google OAuth callback arrived without an authorization code.");
            return Redirect(BuildFailureRedirect(
                "Error al iniciar sesión con Google. Por favor intenta de nuevo.",
                redirectTarget));
        }

        try
        {
            var response = await _authenticationService.LoginWithGoogleAsync(code, cancellationToken);
            return Redirect(BuildSuccessRedirect(response, redirectTarget));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth error: {OAuthError}", ex.Message);
            return Redirect(BuildFailureRedirect(
                "Error al iniciar sesión con Google. Por favor intenta de nuevo.",
                redirectTarget));
        }
    }

    /// <summary>
    /// Refresh an access token using a valid refresh token
    /// </summary>
    /// <param name="request">Refresh token payload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication token and user details</returns>
    /// <response code="200">Refresh successful</response>
    /// <response code="401">Refresh token invalid or expired</response>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authenticationService.RefreshAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Refresh token is invalid or expired.")
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Refresh failed",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
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

    /// <summary>
    /// Activate a user account (Admin only) - Required for nurses before they can login
    /// </summary>
    /// <param name="request">User ID to activate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">User activated successfully</response>
    /// <response code="400">Invalid input or user already active</response>
    /// <response code="401">Unauthorized - not authenticated</response>
    /// <response code="403">Forbidden - only admins can activate users</response>
    [HttpPost("activate-user")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActivateUser(
        [FromBody] ActivateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return BadRequest(new { error = "Invalid user ID format." });
        }

        try
        {
            await _authenticationService.ActivateUserAsync(userId, cancellationToken);
            return Ok(new { message = "User account activated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private string BuildSuccessRedirect(AuthResponse response, string redirectTarget)
    {
        return BuildRedirect(new Dictionary<string, string?>
        {
            ["oauth"] = "success",
            ["token"] = response.Token,
            ["refreshToken"] = response.RefreshToken,
            ["expiresAtUtc"] = response.ExpiresAtUtc?.ToString("O"),
            ["userId"] = response.UserId.ToString(),
            ["email"] = response.Email,
            ["roles"] = string.Join(",", response.Roles),
            ["requiresProfileCompletion"] = response.RequiresProfileCompletion.ToString().ToLowerInvariant(),
            ["requiresAdminReview"] = response.RequiresAdminReview.ToString().ToLowerInvariant()
        }, redirectTarget);
    }

    private string BuildFailureRedirect(string message, string redirectTarget)
    {
        return BuildRedirect(new Dictionary<string, string?>
        {
            ["oauth"] = "error",
            ["message"] = message
        }, redirectTarget);
    }

    private string BuildRedirect(
        IReadOnlyDictionary<string, string?> parameters,
        string redirectTarget)
    {
        return string.Equals(redirectTarget, "mobile", StringComparison.OrdinalIgnoreCase)
            ? BuildMobileRedirect(parameters)
            : BuildWebRedirect(parameters);
    }

    private string BuildWebRedirect(IReadOnlyDictionary<string, string?> fragmentParameters)
    {
        if (string.IsNullOrWhiteSpace(_googleOAuthOptions.FrontendRedirectUrl))
        {
            throw new InvalidOperationException(
                "Google OAuth frontend redirect is not configured. Set GOOGLE_OAUTH_FRONTEND_REDIRECT_URL.");
        }

        var baseUri = _googleOAuthOptions.FrontendRedirectUrl.Split('#')[0];
        var fragment = QueryHelpers.AddQueryString(string.Empty, fragmentParameters)
            .TrimStart('?');

        return $"{baseUri}#{fragment}";
    }

    private string BuildMobileRedirect(IReadOnlyDictionary<string, string?> queryParameters)
    {
        if (string.IsNullOrWhiteSpace(_googleOAuthOptions.MobileRedirectUrl))
        {
            throw new InvalidOperationException(
                "Google OAuth mobile redirect is not configured. Set GOOGLE_OAUTH_MOBILE_REDIRECT_URL.");
        }

        var query = string.Join(
            "&",
            queryParameters
                .Where(parameter => parameter.Value is not null)
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

        if (string.IsNullOrEmpty(query))
        {
            return _googleOAuthOptions.MobileRedirectUrl;
        }

        var separator = _googleOAuthOptions.MobileRedirectUrl.Contains('?', StringComparison.Ordinal)
            ? "&"
            : "?";

        return $"{_googleOAuthOptions.MobileRedirectUrl}{separator}{query}";
    }

    private static string NormalizeRedirectTarget(string? target)
        => string.Equals(target, "mobile", StringComparison.OrdinalIgnoreCase) ? "mobile" : "web";

    private Guid? ResolveAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
