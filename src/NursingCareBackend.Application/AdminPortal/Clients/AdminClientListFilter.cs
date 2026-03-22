namespace NursingCareBackend.Application.AdminPortal.Clients;

public sealed record AdminClientListFilter(
  string? Search,
  string? Status);
