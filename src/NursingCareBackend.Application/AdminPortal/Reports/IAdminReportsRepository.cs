using System;
using System.Threading;
using System.Threading.Tasks;

namespace NursingCareBackend.Application.AdminPortal.Reports;

public interface IAdminReportsRepository
{
    Task<CareRequestPipelineReport> GetCareRequestPipelineReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    
    Task<AssignmentApprovalBacklogReport> GetAssignmentApprovalBacklogReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    
    Task<NurseOnboardingReport> GetNurseOnboardingReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    
    Task<ActiveInactiveUsersReport> GetActiveInactiveUsersReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    
    Task<NurseUtilizationReport> GetNurseUtilizationReportAsync(DateOnly? from, DateOnly? to, int pageNumber, int pageSize, CancellationToken cancellationToken);
    
    Task<CareRequestCompletionReport> GetCareRequestCompletionReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    
    Task<PriceUsageSummaryReport> GetPriceUsageSummaryReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    
    Task<NotificationVolumeReport> GetNotificationVolumeReportAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
}
