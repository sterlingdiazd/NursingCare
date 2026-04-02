using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NursingCareBackend.Api.Security;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Api.Tests;

public sealed class AuthApiTests : IClassFixture<CustomWebApplicationFactory>
{
  private readonly CustomWebApplicationFactory _factory;

  public AuthApiTests(CustomWebApplicationFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task POST_Register_Should_Return_Token_And_Default_Client_Role()
  {
    var client = _factory.CreateClient();
    var email = $"register-{Guid.NewGuid():N}@nursingcare.local";

    var response = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Maria",
      lastName = "Perez",
      identificationNumber = "00112345678",
      phone = "8095550101",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    Assert.NotNull(payload.ExpiresAtUtc);
    Assert.Equal(email, payload.Email);
    Assert.Contains("CLIENT", payload.Roles);
    Assert.False(payload.RequiresAdminReview);
  }

  [Fact]
  public async Task POST_Register_Should_Return_Token_And_Admin_Review_Flag_For_Nurse()
  {
    var client = _factory.CreateClient();
    var email = $"nurse-register-{Guid.NewGuid():N}@nursingcare.local";

    var response = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber = "00122334456",
      phone = "8095550102",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
      bankName = "Banco Central",
      profileType = 1
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    Assert.NotNull(payload.ExpiresAtUtc);
    Assert.Equal(email, payload.Email);
    Assert.Contains("NURSE", payload.Roles);
    Assert.True(payload.RequiresAdminReview);
  }

  [Fact]
  public async Task POST_Login_Should_Return_Token_For_Registered_User()
  {
    var client = _factory.CreateClient();
    var email = $"login-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Carlos",
      lastName = "Diaz",
      identificationNumber = "00122334456",
      phone = "8095550102",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
    Assert.NotNull(payload.ExpiresAtUtc);
    Assert.Equal(email, payload.Email);
  }

  [Fact]
  public async Task POST_Login_Should_Return_Token_For_Nurse_Under_Admin_Review()
  {
    var client = _factory.CreateClient();
    var email = $"pending-nurse-{Guid.NewGuid():N}@nursingcare.local";

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber = "00133445567",
      phone = "8095550103",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
      bankName = "Banco Central",
      profileType = 1
    });

    registerResponse.EnsureSuccessStatusCode();

    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(payload);
    Assert.False(string.IsNullOrWhiteSpace(payload!.Token));
    Assert.True(payload.RequiresAdminReview);
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_Forbidden_For_Nurse_Under_Admin_Review()
  {
    var client = _factory.CreateClient();
    var email = $"review-nurse-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Luisa",
      lastName = "Martinez",
      identificationNumber = "00133445567",
      phone = "8095550103",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!",
      hireDate = "2026-03-21",
      specialty = "Atencion domiciliaria",
      bankName = "Banco Central",
      profileType = 1
    });

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", loginPayload!.Token);

    var response = await client.GetAsync("/api/care-requests");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task POST_Login_Should_Return_Unauthorized_For_Invalid_Credentials()
  {
    var client = _factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email = "missing.user@nursingcare.local",
      password = "WrongPass123!"
    });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(problem);
    Assert.Equal("Correo o contrasena invalidos.", problem!.Detail);
  }

  [Fact]
  public async Task POST_Register_Should_Return_BadRequest_For_Invalid_Identity_Field_Formats()
  {
    var client = _factory.CreateClient();
    var email = $"invalid-register-{Guid.NewGuid():N}@nursingcare.local";

    var response = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Maria1",
      lastName = "Perez",
      identificationNumber = "001-1234567-8",
      phone = "809-555-0101",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(problem);
    Assert.Equal("La solicitud contiene datos invalidos.", problem!.Title);
    Assert.Equal("El nombre solo puede contener letras y espacios.", problem.Detail);
  }

  [Fact]
  public async Task GET_GoogleStart_Should_Redirect_To_Google()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/start");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);
    Assert.StartsWith(
      "https://accounts.google.com/o/oauth2/v2/auth",
      response.Headers.Location!.ToString(),
      StringComparison.Ordinal);
  }

  [Fact]
  public async Task GET_GoogleStart_Should_Include_Mobile_Target_In_State()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/start?target=mobile");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);
    Assert.Contains("state=mobile", response.Headers.Location!.ToString(), StringComparison.Ordinal);
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Login_With_Tokens()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?code=google-success-web");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("http://localhost:3000/login#", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery("?" + response.Headers.Location.Fragment.TrimStart('#'));
    Assert.Equal("success", parameters["oauth"].ToString());
    Assert.Equal("google-success-web@example.com", parameters["email"].ToString());
    Assert.Equal("CLIENT", parameters["roles"].ToString());
    Assert.Equal("true", parameters["requiresProfileCompletion"].ToString());
    Assert.Equal("false", parameters["requiresAdminReview"].ToString());
    Assert.False(string.IsNullOrWhiteSpace(parameters["token"].ToString()));
    Assert.False(string.IsNullOrWhiteSpace(parameters["refreshToken"].ToString()));
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Login_With_Error_When_Google_Fails()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?error=access_denied");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("http://localhost:3000/login#", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery("?" + response.Headers.Location.Fragment.TrimStart('#'));
    Assert.Equal("error", parameters["oauth"].ToString());
    Assert.Equal("Error al iniciar sesión con Google. Por favor intenta de nuevo.", parameters["message"].ToString());
    Assert.False(string.IsNullOrWhiteSpace(parameters["correlationId"].ToString()));
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Mobile_Deep_Link()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?code=google-success-mobile&state=mobile");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("nursingcaremobile://login", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery(response.Headers.Location.Query);
    Assert.Equal("success", parameters["oauth"].ToString());
    Assert.Equal("google-success-mobile@example.com", parameters["email"].ToString());
    Assert.Equal("CLIENT", parameters["roles"].ToString());
    Assert.Equal("true", parameters["requiresProfileCompletion"].ToString());
    Assert.Equal("false", parameters["requiresAdminReview"].ToString());
  }

  [Fact]
  public async Task GET_GoogleCallback_Should_Redirect_Back_To_Login_With_Specific_Error_For_Unverified_Google_Email()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var response = await client.GetAsync("/api/auth/google/callback?code=google-unverified");

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    Assert.NotNull(response.Headers.Location);

    var location = response.Headers.Location!.ToString();
    Assert.StartsWith("http://localhost:3000/login#", location, StringComparison.Ordinal);

    var parameters = QueryHelpers.ParseQuery("?" + response.Headers.Location.Fragment.TrimStart('#'));
    Assert.Equal("error", parameters["oauth"].ToString());
    Assert.Equal("Tu cuenta de Google no tiene el correo verificado.", parameters["message"].ToString());
    Assert.False(string.IsNullOrWhiteSpace(parameters["correlationId"].ToString()));
  }

  [Fact]
  public async Task POST_CompleteProfile_Should_Update_Google_User_And_Return_Profile_As_Completed()
  {
    var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
      AllowAutoRedirect = false
    });

    var callbackResponse = await client.GetAsync("/api/auth/google/callback?code=google-success-complete");

    Assert.Equal(HttpStatusCode.Redirect, callbackResponse.StatusCode);
    Assert.NotNull(callbackResponse.Headers.Location);

    var parameters = QueryHelpers.ParseQuery("?" + callbackResponse.Headers.Location.Fragment.TrimStart('#'));
    var token = parameters["token"].ToString();

    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.PostAsJsonAsync("/api/auth/complete-profile", new
    {
      name = "Mariela",
      lastName = "Suarez",
      identificationNumber = "00199999999",
      phone = "8095550108"
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(payload);
    Assert.False(payload!.RequiresProfileCompletion);
    Assert.Equal("google-success-complete@example.com", payload.Email);
  }

  [Fact]
  public async Task GET_CareRequests_Should_Return_Unauthorized_When_Token_Is_Missing()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/api/care-requests");

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task POST_AssignRole_Should_Return_Forbidden_For_Non_Admin_User()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateToken(_factory.Services, "NURSE"));

    var response = await client.PostAsJsonAsync("/api/auth/assign-role", new
    {
      userId = Guid.NewGuid().ToString(),
      roleName = "ADMIN"
    });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task POST_AssignRole_Should_Reject_Admin_Role_Assignment_Outside_Admin_Portal()
  {
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
      "Bearer",
      JwtTestTokens.CreateAdminToken(_factory.Services));

    var response = await client.PostAsJsonAsync("/api/auth/assign-role", new
    {
      userId = Guid.NewGuid().ToString(),
      roleName = "ADMIN"
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
    Assert.NotNull(payload);
    Assert.Equal(
      "El rol de administracion solo puede asignarse desde el portal administrativo.",
      payload!.Detail);
  }

  [Fact]
  public async Task POST_Refresh_Should_Return_New_Tokens_For_Valid_RefreshToken()
  {
    var client = _factory.CreateClient();
    var email = $"refresh-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Jose",
      lastName = "Santos",
      identificationNumber = "00144556678",
      phone = "8095550104",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = loginPayload!.RefreshToken
    });

    refreshResponse.EnsureSuccessStatusCode();

    var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
    Assert.NotNull(refreshPayload);
    Assert.False(string.IsNullOrWhiteSpace(refreshPayload!.Token));
    Assert.False(string.IsNullOrWhiteSpace(refreshPayload.RefreshToken));
    Assert.NotEqual(loginPayload.RefreshToken, refreshPayload.RefreshToken);
  }

  [Fact]
  public async Task POST_Refresh_Should_Return_Unauthorized_For_Invalid_RefreshToken()
  {
    var client = _factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = "invalid-refresh-token"
    });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task POST_Refresh_Should_Return_Unauthorized_For_Reused_RefreshToken()
  {
    var client = _factory.CreateClient();
    var email = $"refresh-reuse-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Elena",
      lastName = "Ruiz",
      identificationNumber = "00155667789",
      phone = "8095550105",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });

    loginResponse.EnsureSuccessStatusCode();
    var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    var firstRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = loginPayload!.RefreshToken
    });

    firstRefreshResponse.EnsureSuccessStatusCode();

    var reusedRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = loginPayload.RefreshToken
    });

    Assert.Equal(HttpStatusCode.Unauthorized, reusedRefreshResponse.StatusCode);
  }

  [Fact]
  public async Task POST_ForgotPassword_Should_Store_A_Hashed_Code_And_Return_A_Generic_Response()
  {
    ResetAuthTestState();
    var client = _factory.CreateClient();
    var email = $"forgot-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Paola",
      lastName = "Sanchez",
      identificationNumber = "00166778890",
      phone = "8095550106",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new
    {
      email
    });

    response.EnsureSuccessStatusCode();

    var payload = await response.Content.ReadFromJsonAsync<PasswordResetResponseDto>();
    Assert.NotNull(payload);
    Assert.Equal("Si el correo esta registrado, se ha enviado un codigo de recuperacion.", payload!.Message);

    Assert.Single(_factory.EmailService.SentEmails);
    var code = ExtractResetCode(_factory.EmailService.SentEmails[0].HtmlBody);
    var user = await GetUserByEmailAsync(email);

    Assert.False(string.IsNullOrWhiteSpace(user.ResetPasswordCodeHash));
    Assert.NotEqual(code, user.ResetPasswordCodeHash);
    Assert.NotNull(user.ResetPasswordCodeIssuedAtUtc);
    Assert.NotNull(user.ResetPasswordCodeExpiresAtUtc);
    Assert.NotNull(user.ResetPasswordResendAvailableAtUtc);
    Assert.Equal(0, user.ResetPasswordFailedAttemptCount);
  }

  [Fact]
  public async Task POST_ForgotPassword_Should_Suppress_Reissue_Within_Cooldown_And_Reissue_After_It_Expires()
  {
    ResetAuthTestState();
    var client = _factory.CreateClient();
    var email = $"forgot-resend-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Rosa",
      lastName = "Mendez",
      identificationNumber = "00166778891",
      phone = "8095550107",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var firstResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
    firstResponse.EnsureSuccessStatusCode();
    var firstCode = ExtractResetCode(_factory.EmailService.SentEmails[0].HtmlBody);

    var secondResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
    secondResponse.EnsureSuccessStatusCode();
    Assert.Single(_factory.EmailService.SentEmails);

    _factory.TimeProvider.Advance(TimeSpan.FromSeconds(61));

    var thirdResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
    thirdResponse.EnsureSuccessStatusCode();
    Assert.Equal(2, _factory.EmailService.SentEmails.Count);

    var secondCode = ExtractResetCode(_factory.EmailService.SentEmails[1].HtmlBody);
    Assert.NotEqual(firstCode, secondCode);
  }

  [Fact]
  public async Task POST_ResetPassword_Should_Return_A_Message_Revoke_RefreshTokens_And_Require_A_New_Login()
  {
    ResetAuthTestState();
    var client = _factory.CreateClient();
    var email = $"reset-success-{Guid.NewGuid():N}@nursingcare.local";

    var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Teresa",
      lastName = "Lopez",
      identificationNumber = "00166778892",
      phone = "8095550108",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    registerResponse.EnsureSuccessStatusCode();
    var registerPayload = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

    var forgotResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
    forgotResponse.EnsureSuccessStatusCode();
    var code = ExtractResetCode(_factory.EmailService.SentEmails[0].HtmlBody);

    var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password", new
    {
      email,
      code,
      newPassword = "NewPass123!"
    });

    resetResponse.EnsureSuccessStatusCode();
    var payload = await resetResponse.Content.ReadFromJsonAsync<PasswordResetResponseDto>();
    Assert.NotNull(payload);
    Assert.Equal("La contrasena se restablecio correctamente. Inicia sesion con tu nueva contrasena.", payload!.Message);

    var body = await resetResponse.Content.ReadAsStringAsync();
    Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);

    var oldRefreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
    {
      refreshToken = registerPayload!.RefreshToken
    });
    Assert.Equal(HttpStatusCode.Unauthorized, oldRefreshResponse.StatusCode);

    var oldPasswordLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });
    Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLoginResponse.StatusCode);

    var newPasswordLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "NewPass123!"
    });
    newPasswordLoginResponse.EnsureSuccessStatusCode();
  }

  [Fact]
  public async Task POST_ResetPassword_Should_Invalidate_The_Code_After_Five_Invalid_Attempts()
  {
    ResetAuthTestState();
    var client = _factory.CreateClient();
    var email = $"reset-attempts-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Lucia",
      lastName = "Ramirez",
      identificationNumber = "00166778893",
      phone = "8095550109",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    var forgotResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
    forgotResponse.EnsureSuccessStatusCode();
    var code = ExtractResetCode(_factory.EmailService.SentEmails[0].HtmlBody);

    for (var attempt = 0; attempt < 5; attempt += 1)
    {
      var invalidResponse = await client.PostAsJsonAsync("/api/auth/reset-password", new
      {
        email,
        code = "000000",
        newPassword = "NewPass123!"
      });

      Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    var validResponse = await client.PostAsJsonAsync("/api/auth/reset-password", new
    {
      email,
      code,
      newPassword = "NewPass123!"
    });

    Assert.Equal(HttpStatusCode.BadRequest, validResponse.StatusCode);

    var user = await GetUserByEmailAsync(email);
    Assert.Null(user.ResetPasswordCodeHash);
    Assert.Null(user.ResetPasswordCodeExpiresAtUtc);
    Assert.Equal(0, user.ResetPasswordFailedAttemptCount);
  }

  [Fact]
  public async Task POST_Login_Should_Temporarily_Lock_Password_Login_After_Five_Invalid_Attempts()
  {
    ResetAuthTestState();
    var client = _factory.CreateClient();
    var email = $"lockout-{Guid.NewGuid():N}@nursingcare.local";

    await client.PostAsJsonAsync("/api/auth/register", new
    {
      name = "Marcos",
      lastName = "Gil",
      identificationNumber = "00166778894",
      phone = "8095550110",
      email,
      password = "Pass123!",
      confirmPassword = "Pass123!"
    });

    for (var attempt = 0; attempt < 5; attempt += 1)
    {
      var invalidResponse = await client.PostAsJsonAsync("/api/auth/login", new
      {
        email,
        password = "WrongPass123!"
      });

      Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
    }

    var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });
    Assert.Equal(HttpStatusCode.Unauthorized, lockedResponse.StatusCode);

    _factory.TimeProvider.Advance(TimeSpan.FromMinutes(16));

    var recoveredResponse = await client.PostAsJsonAsync("/api/auth/login", new
    {
      email,
      password = "Pass123!"
    });
    recoveredResponse.EnsureSuccessStatusCode();
  }

  [Fact]
  public async Task POST_ForgotPassword_Should_Return_TooManyRequests_When_The_Ip_Limit_Is_Exceeded()
  {
    ResetAuthTestState();
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.10");

    for (var attempt = 0; attempt < 10; attempt += 1)
    {
      var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new
      {
        email = $"missing-{attempt}@nursingcare.local"
      });

      response.EnsureSuccessStatusCode();
    }

    var limitedResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new
    {
      email = "missing-final@nursingcare.local"
    });

    Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    Assert.True(limitedResponse.Headers.RetryAfter?.Delta is not null || limitedResponse.Headers.Contains("Retry-After"));
  }

  private void ResetAuthTestState()
  {
    _factory.TimeProvider.Reset();
    _factory.EmailService.SentEmails.Clear();

    using var scope = _factory.Services.CreateScope();
    var authRateLimiter = scope.ServiceProvider.GetRequiredService<IAuthRateLimiter>() as AuthRateLimiter;
    authRateLimiter?.Reset();
  }

  private async Task<User> GetUserByEmailAsync(string email)
  {
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<NursingCareDbContext>();
    var user = await dbContext.Users.SingleAsync(userRecord => userRecord.Email == email);
    return user;
  }

  private static string ExtractResetCode(string htmlBody)
  {
    var match = Regex.Match(htmlBody, @"\b(\d{6})\b");
    Assert.True(match.Success);
    return match.Groups[1].Value;
  }

  private sealed class AuthResponseDto
  {
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public bool RequiresProfileCompletion { get; set; }
    public bool RequiresAdminReview { get; set; }
  }

  private sealed class PasswordResetResponseDto
  {
    public string Message { get; set; } = string.Empty;
  }

  private sealed class ProblemDetailsDto
  {
    public string? Title { get; set; }
    public string? Detail { get; set; }
  }
}
