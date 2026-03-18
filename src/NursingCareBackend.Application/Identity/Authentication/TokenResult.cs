namespace NursingCareBackend.Application.Identity.Authentication;

public sealed record TokenResult(
  string Token,
  DateTime ExpiresAtUtc
);
