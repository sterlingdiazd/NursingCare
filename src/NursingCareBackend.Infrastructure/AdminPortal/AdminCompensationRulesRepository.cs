using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

public sealed class AdminCompensationRulesRepository : IAdminCompensationRulesRepository
{
    private readonly NursingCareDbContext _dbContext;

    public AdminCompensationRulesRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminCompensationRuleListResult> GetRulesAsync(CancellationToken cancellationToken)
    {
        var rules = await _dbContext.CompensationRules
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var items = rules
            .Select(r => new AdminCompensationRuleListItem(
                r.Id,
                r.Name,
                r.EmploymentType.ToString(),
                r.BaseCompensationPercent,
                r.TransportIncentivePercent,
                r.ComplexityBonusPercent,
                r.MedicalSuppliesPercent,
                r.IsActive,
                r.CreatedAtUtc))
            .ToList()
            .AsReadOnly();

        return new AdminCompensationRuleListResult(items, items.Count);
    }

    public async Task<AdminCompensationRuleDetail?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.CompensationRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);

        if (rule is null) return null;

        return new AdminCompensationRuleDetail(
            rule.Id,
            rule.Name,
            rule.EmploymentType.ToString(),
            rule.BaseCompensationPercent,
            rule.TransportIncentivePercent,
            rule.ComplexityBonusPercent,
            rule.MedicalSuppliesPercent,
            rule.IsActive,
            rule.CreatedAtUtc);
    }

    public async Task<Guid> CreateRuleAsync(CreateCompensationRuleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CompensationEmploymentType>(request.EmploymentType, ignoreCase: true, out var employmentType))
            throw new ArgumentException($"Tipo de empleo invalido: {request.EmploymentType}");

        var rule = CompensationRule.Create(
            request.Name,
            employmentType,
            null, // careRequestCategoryCode
            null, // unitTypeCode
            null, // nurseCategoryCode
            request.BaseCompensationPercent,
            0m, // fixedAmountPerUnit
            request.TransportIncentivePercent,
            request.ComplexityBonusPercent,
            request.MedicalSuppliesPercent,
            0m, // partialServicePercent
            0m, // expressServicePercent
            0m, // suspendedServicePercent
            true, // isActive
            0, // priority
            DateTime.UtcNow);

        _dbContext.CompensationRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }

    public async Task<bool> UpdateRuleAsync(Guid ruleId, UpdateCompensationRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.CompensationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);

        if (rule is null) return false;

        rule.Update(
            request.Name,
            request.BaseCompensationPercent,
            request.TransportIncentivePercent,
            request.ComplexityBonusPercent,
            request.MedicalSuppliesPercent,
            DateTime.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeactivateRuleAsync(Guid ruleId, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.CompensationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);

        if (rule is null) return false;

        rule.Deactivate(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}