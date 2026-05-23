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

// ── Nurse-payments reports ───────────────────────────────────────────────────

public record NursePaymentsDailyRow(
    string Date,
    int ServiceCount,
    decimal Amount,
    decimal CumulativeAmount
);

public record NursePaymentsDailyReport(
    IReadOnlyList<NursePaymentsDailyRow> Rows,
    decimal TotalAccrued
);

public record NursePaymentsByTypeRow(
    string ServiceType,
    int ServiceCount,
    decimal Amount
);

public record NursePaymentsByTypeReport(
    IReadOnlyList<NursePaymentsByTypeRow> Rows,
    decimal Total
);

public record NursePaymentsByPeriodRow(
    string PeriodLabel,
    int ServiceCount,
    decimal Amount
);

public record NursePaymentsByPeriodReport(
    IReadOnlyList<NursePaymentsByPeriodRow> Rows,
    decimal Total
);

public record NursePaymentsRankingRow(
    string NurseName,
    int ServiceCount,
    int DaysWorked,
    decimal Amount
);

public record NursePaymentsRankingReport(
    IReadOnlyList<NursePaymentsRankingRow> Rows,
    decimal Total
);
