namespace NursingCareBackend.Application.Identity.Commands;

public record ResetPasswordRequest(string Email, string Code, string NewPassword);
