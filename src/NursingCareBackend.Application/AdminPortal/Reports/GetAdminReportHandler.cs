using System;
using System.Threading;
using System.Threading.Tasks;

namespace NursingCareBackend.Application.AdminPortal.Reports;

public class GetAdminReportHandler
{
    private readonly IAdminReportsRepository _repository;

    public GetAdminReportHandler(IAdminReportsRepository repository)
    {
        _repository = repository;
    }

    public Task<object> HandleAsync(string reportKey, DateOnly? from, DateOnly? to, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        return reportKey switch
        {
            "care-request-pipeline" => HandleTypedAsync(() => _repository.GetCareRequestPipelineReportAsync(from, to, cancellationToken)),
            "assignment-approval-backlog" => HandleTypedAsync(() => _repository.GetAssignmentApprovalBacklogReportAsync(from, to, cancellationToken)),
            "nurse-onboarding" => HandleTypedAsync(() => _repository.GetNurseOnboardingReportAsync(from, to, cancellationToken)),
            "active-inactive-users" => HandleTypedAsync(() => _repository.GetActiveInactiveUsersReportAsync(from, to, cancellationToken)),
            "nurse-utilization" => HandleTypedAsync(() => _repository.GetNurseUtilizationReportAsync(from, to, pageNumber ?? 1, pageSize ?? 20, cancellationToken)),
            "care-request-completion" => HandleTypedAsync(() => _repository.GetCareRequestCompletionReportAsync(from, to, cancellationToken)),
            "price-usage-summary" => HandleTypedAsync(() => _repository.GetPriceUsageSummaryReportAsync(from, to, cancellationToken)),
            "notification-volume" => HandleTypedAsync(() => _repository.GetNotificationVolumeReportAsync(from, to, cancellationToken)),
            "payroll-summary" => HandleTypedAsync(() => _repository.GetPayrollSummaryReportAsync(from, to, cancellationToken)),
            _ => throw new ArgumentException($"Unknown report key: {reportKey}", nameof(reportKey))
        };
    }

    // Helper to upcast Task<T> to Task<object> implicitly
    private async Task<object> HandleTypedAsync<T>(Func<Task<T>> taskFactory)
    {
        return await taskFactory() ?? throw new InvalidOperationException("Report returned null.");
    }
}
