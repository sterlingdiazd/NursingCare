namespace NursingCareBackend.Domain.Identity;

public static class SystemRoles
{
  public const string Admin = "ADMIN";
  public const string Nurse = "NURSE";
  public const string Client = "CLIENT";

  public static readonly (Guid Id, string Name)[] Defaults =
  [
    (Guid.Parse("550e8400-e29b-41d4-a716-446655440001"), Admin),
    (Guid.Parse("550e8400-e29b-41d4-a716-446655440002"), Nurse),
    (Guid.Parse("550e8400-e29b-41d4-a716-446655440003"), Client),
  ];
}
