namespace NursingCareBackend.Application.AdminPortal.Finance;

/// <summary>Rich financial snapshot powering the admin dashboard (the app's home screen).</summary>
public sealed record FinanceOverview(
    DateOnly From,
    DateOnly To,
    FinanceSummary Summary,
    IReadOnlyList<CategoryMargin> ByCategory,
    IReadOnlyList<ServiceLineMargin> ByServiceLine,
    IReadOnlyList<ClientRevenueRow> TopClients,
    IReadOnlyList<NurseParticipationRow> NurseParticipation,
    IReadOnlyList<NurseLoanRow> Loans,
    decimal TotalLoansOutstanding,
    IReadOnlyList<TrendPoint> MonthlyTrend,
    IReadOnlyList<HealthIndicator> Health,
    IReadOnlyList<Insight> Insights);

/// <summary>A value plus its change vs the previous comparable period.</summary>
public sealed record Metric(decimal Value, decimal PreviousValue)
{
    public decimal Delta => Value - PreviousValue;
    public decimal? DeltaPercent => PreviousValue == 0m ? null : decimal.Round((Value - PreviousValue) / PreviousValue * 100m, 1);
}

public sealed record FinanceSummary(
    Metric Revenue,        // delivered-services revenue (basis for margin)
    Metric Collected,      // cash collected (CareRequest Paid in range)
    decimal Pending,       // billed and not yet paid (all-time outstanding)
    Metric LaborCost,
    Metric GrossMargin,
    decimal MarginPercent,
    Metric ServicesCount,
    int ActiveNurses);

public sealed record CategoryMargin(string Category, string DisplayName, decimal Revenue, decimal Labor, decimal Margin, decimal MarginPercent);

public sealed record ServiceLineMargin(string ServiceLine, decimal Revenue, decimal Labor, decimal Margin, decimal MarginPercent);

public sealed record ClientRevenueRow(string ClientName, int ServicesCount, decimal Billed, decimal Collected, decimal Pending, decimal Margin);

public sealed record NurseParticipationRow(
    string NurseName, int ServicesCount, int DaysWorked, decimal RevenueGenerated,
    decimal NetPay, decimal ParticipationPercent, decimal MarginContributed, decimal LoanOutstanding);

public sealed record NurseLoanRow(string NurseName, decimal OutstandingBalance);

public sealed record TrendPoint(string Label, decimal Revenue, decimal Margin);

/// <summary>A business-health metric with a green/amber/red status and the drivers explaining it.</summary>
public sealed record HealthIndicator(
    string Key, string Title, string Status, decimal Value, string ValueLabel,
    decimal Target, string Explanation, IReadOnlyList<string> Drivers);

/// <summary>A proactive insight ("things she might want to know") with a plain-language explanation.</summary>
public sealed record Insight(string Key, string Severity, string Title, string Detail, string? DeepLinkPath);

/// <summary>
/// Source-record detail behind a dashboard metric, shaped for an elegant card presentation
/// (no tables, no truncation): a headline figure + ranked record cards, each with a primary label,
/// a metadata line, a full amount, and a relative bar. Fields/Total feed the headline summary.
/// </summary>
public sealed record FinanceDetail(
    string Title,
    string? Explanation,
    string Headline,
    string HeadlineCaption,
    IReadOnlyList<FinanceField> Summary,
    IReadOnlyList<FinanceDetailRow> Rows,
    string? Footnote);

/// <summary>A labelled figure shown in the headline summary (e.g., "Ingresos" → "RD$ 70,710.00").</summary>
public sealed record FinanceField(string Label, string Value, bool Emphasize = false);

/// <summary>One record card: Primary (title), Meta (subtitle, may hold several labelled facts), Amount, BarFraction (0..1 of the max).</summary>
public sealed record FinanceDetailRow(
    string Primary,
    string Meta,
    string Amount,
    double BarFraction,
    IReadOnlyList<FinanceField> Facts);
