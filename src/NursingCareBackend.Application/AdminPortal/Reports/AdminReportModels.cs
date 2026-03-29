using System;
using System.Collections.Generic;

namespace NursingCareBackend.Application.AdminPortal.Reports;

public record CareRequestPipelineReport(
    int PendingCount,
    int ApprovedCount,
    int CompletedCount,
    int RejectedCount,
    int UnassignedCount,
    int OverdueCount
);

public record AssignmentApprovalBacklogReport(
    int PendingUnassignedCount,
    int PendingAssignedAwaitingApprovalCount,
    double AverageDaysPending
);

public record NurseOnboardingReport(
    int TotalRegisteredCount,
    int PendingReviewCount,
    int ActiveCount,
    int InactiveCount,
    int CompletedThisPeriodCount
);

public record ActiveInactiveUsersReport(
    int AdminActiveCount,
    int AdminInactiveCount,
    int ClientActiveCount,
    int ClientInactiveCount,
    int NurseActiveCount,
    int NurseInactiveCount
);

public record NurseUtilizationRow(
    string NurseId,
    string NurseName,
    int TotalAssigned,
    int Completed,
    int Pending,
    double CompletionRate
);

public record NurseUtilizationReport(
    IReadOnlyList<NurseUtilizationRow> Rows,
    int TotalNurses,
    int PageNumber,
    int PageSize
);

public record CareRequestCompletionReport(
    int TotalCompletedCount,
    double AverageDaysToComplete,
    Dictionary<string, int> CompletionsByRange
);

public record PriceUsageSummaryRow(
    string RequestType,
    int Count,
    decimal AverageTotal,
    decimal TotalRevenue
);

public record PriceUsageSummaryReport(
    IReadOnlyList<PriceUsageSummaryRow> TopRequestTypes,
    List<string> TopDistanceFactors,
    List<string> TopComplexityLevels
);

public record NotificationVolumeReport(
    int TotalNotificationsCount,
    int UnreadNotificationsCount,
    Dictionary<string, int> NotificationsByCategory,
    int PendingActionItemsCount
);
