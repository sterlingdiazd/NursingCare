namespace NursingCareBackend.Application.Identity.Commands;

public sealed record RefreshTokenRequest(
  string RefreshToken
);
