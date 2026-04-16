namespace NursingCareBackend.Application.AdminPortal.Payroll;

public interface IAdminCompensationRulesRepository
{
    Task<AdminCompensationRuleListResult> GetRulesAsync(CancellationToken cancellationToken);
    Task<AdminCompensationRuleDetail?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken);
    Task<Guid> CreateRuleAsync(CreateCompensationRuleRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateRuleAsync(Guid ruleId, UpdateCompensationRuleRequest request, CancellationToken cancellationToken);
    Task<bool> DeactivateRuleAsync(Guid ruleId, CancellationToken cancellationToken);
}