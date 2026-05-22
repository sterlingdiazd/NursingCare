using Microsoft.EntityFrameworkCore;
using NursingCareBackend.Application.AdminPortal.Finance;
using NursingCareBackend.Domain.CareRequests;
using NursingCareBackend.Domain.Identity;
using NursingCareBackend.Domain.Payroll;
using NursingCareBackend.Infrastructure.Persistence;

namespace NursingCareBackend.Infrastructure.AdminPortal;

/// <summary>
/// Aggregates the financial dashboard. Revenue/margin breakdowns use a delivered-services basis
/// (ServiceExecution executed in the range: revenue = SubtotalBeforeSupplies, labor = NetCompensation)
/// so categories/lines/nurses reconcile to the total. Cash metrics (collected/pending) come from the
/// CareRequest invoice/payment dates.
/// </summary>
public sealed class AdminFinanceRepository : IAdminFinanceRepository
{
    private readonly NursingCareDbContext _dbContext;

    public AdminFinanceRepository(NursingCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private static decimal R(decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);
    private static decimal Pct(decimal part, decimal whole) => whole == 0m ? 0m : decimal.Round(part / whole * 100m, 1);

    private static string ServiceLineOf(string? categoryCode) => categoryCode switch
    {
        "hogar" => "Casa hogar",
        "domicilio" => "Domicilio",
        "medicos" => "Servicios médicos",
        _ => "Otros",
    };

    public async Task<FinanceOverview> GetOverviewAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from) (from, to) = (to, from);
        var lengthDays = to.DayNumber - from.DayNumber + 1;
        var prevTo = from.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(lengthDays - 1));
        var trendFrom = new DateOnly(to.Year, to.Month, 1).AddMonths(-5);

        DateTime Start(DateOnly d) => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime EndEx(DateOnly d) => d.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Delivered services from the start of the trend window through the current range end.
        // ServiceExecution.PricingCategoryCode holds the pricing GROUP (hogar / domicilio / medicos).
        var execs = await _dbContext.ServiceExecutions.AsNoTracking()
            .Where(e => e.ServiceDate >= trendFrom && e.ServiceDate <= to)
            .Select(e => new { e.NurseUserId, e.CareRequestId, e.ServiceDate, e.PricingCategoryCode, e.SubtotalBeforeSupplies, e.NetCompensation })
            .ToListAsync(cancellationToken);

        var inRange = execs.Where(e => e.ServiceDate >= from && e.ServiceDate <= to).ToList();
        var inPrev = execs.Where(e => e.ServiceDate >= prevFrom && e.ServiceDate <= prevTo).ToList();

        // Cash side from care requests.
        var requests = await _dbContext.CareRequests.AsNoTracking()
            .Where(c => c.VoidedAtUtc == null && (c.InvoicedAtUtc != null || c.PaidAtUtc != null))
            .Select(c => new { c.UserID, c.Total, c.InvoicedAtUtc, c.PaidAtUtc })
            .ToListAsync(cancellationToken);

        decimal CollectedIn(DateOnly f, DateOnly t) => requests
            .Where(c => c.PaidAtUtc >= Start(f) && c.PaidAtUtc < EndEx(t)).Sum(c => c.Total);
        var collected = CollectedIn(from, to);
        var collectedPrev = CollectedIn(prevFrom, prevTo);
        var pending = requests.Where(c => c.InvoicedAtUtc != null && c.PaidAtUtc == null).Sum(c => c.Total);
        var billed = requests.Where(c => c.InvoicedAtUtc >= Start(from) && c.InvoicedAtUtc < EndEx(to)).Sum(c => c.Total);

        // Active loan balances (amortizing). Loaded here so loan nurses are included in name resolution.
        var loanRows = await _dbContext.ScheduledDeductions.AsNoTracking()
            .Where(s => s.Status == ScheduledDeductionStatus.Active && s.Modality == ScheduleModality.Amortizing)
            .Select(s => new { s.NurseUserId, Remaining = s.TotalRepayable - s.AmountSettled })
            .ToListAsync(cancellationToken);
        var loansByNurse = loanRows.GroupBy(l => l.NurseUserId)
            .ToDictionary(g => g.Key, g => R(g.Sum(x => x.Remaining)));

        // Delivered-services revenue/labor/margin (basis for all breakdowns).
        var revenue = inRange.Sum(x => x.SubtotalBeforeSupplies);
        var revenuePrev = inPrev.Sum(x => x.SubtotalBeforeSupplies);
        var labor = inRange.Sum(x => x.NetCompensation);
        var laborPrev = inPrev.Sum(x => x.NetCompensation);
        var margin = revenue - labor;
        var marginPrev = revenuePrev - laborPrev;

        var activeNurses = inRange.Select(e => e.NurseUserId).Distinct().Count();

        // Names.
        var nurseIds = execs.Select(e => e.NurseUserId).Distinct().ToList();
        var clientIds = requests.Select(c => c.UserID).Distinct().ToList();
        var allIds = nurseIds.Concat(clientIds).Concat(loansByNurse.Keys).Distinct().ToList();
        var names = await _dbContext.Users.AsNoTracking()
            .Where(u => allIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Name, u.LastName, u.Email })
            .ToListAsync(cancellationToken);
        string NameOf(Guid id)
        {
            var u = names.FirstOrDefault(n => n.Id == id);
            if (u is null) return "—";
            if (!string.IsNullOrWhiteSpace(u.DisplayName)) return u.DisplayName!;
            var full = string.Join(" ", new[] { u.Name, u.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return string.IsNullOrWhiteSpace(full) ? u.Email : full;
        }

        // By category (delivered basis).
        var byCategory = inRange
            .GroupBy(e => e.PricingCategoryCode ?? "otros")
            .Select(g =>
            {
                var rev = g.Sum(x => x.SubtotalBeforeSupplies);
                var lab = g.Sum(x => x.NetCompensation);
                var code = g.Key;
                return new CategoryMargin(code, ServiceLineOf(code), R(rev), R(lab), R(rev - lab), Pct(rev - lab, rev));
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();

        // By service line (roll category group up).
        var byServiceLine = inRange
            .GroupBy(e => ServiceLineOf(e.PricingCategoryCode))
            .Select(g =>
            {
                var rev = g.Sum(x => x.SubtotalBeforeSupplies);
                var lab = g.Sum(x => x.NetCompensation);
                return new ServiceLineMargin(g.Key, R(rev), R(lab), R(rev - lab), Pct(rev - lab, rev));
            })
            .OrderByDescending(s => s.Revenue)
            .ToList();

        // Top clients (cash + delivered margin via execution->careRequest).
        var execByRequest = inRange.GroupBy(e => e.CareRequestId)
            .ToDictionary(g => g.Key, g => (Rev: g.Sum(x => x.SubtotalBeforeSupplies), Lab: g.Sum(x => x.NetCompensation), Count: g.Count()));
        // Map careRequestId -> client. Need request->client; load minimal map for in-range executions.
        var inRangeRequestIds = inRange.Select(e => e.CareRequestId).Distinct().ToList();
        var reqClient = await _dbContext.CareRequests.AsNoTracking()
            .Where(c => inRangeRequestIds.Contains(c.Id))
            .Select(c => new { c.Id, c.UserID, c.Total, c.PaidAtUtc, c.InvoicedAtUtc })
            .ToListAsync(cancellationToken);
        var topClients = reqClient
            .GroupBy(c => c.UserID)
            .Select(g =>
            {
                decimal rev = 0, lab = 0; int svc = 0;
                foreach (var r in g)
                    if (execByRequest.TryGetValue(r.Id, out var x)) { rev += x.Rev; lab += x.Lab; svc += x.Count; }
                var billedC = g.Sum(r => r.Total);
                var collectedC = g.Where(r => r.PaidAtUtc != null).Sum(r => r.Total);
                var pendingC = g.Where(r => r.InvoicedAtUtc != null && r.PaidAtUtc == null).Sum(r => r.Total);
                return new ClientRevenueRow(NameOf(g.Key), svc, R(billedC), R(collectedC), R(pendingC), R(rev - lab));
            })
            .OrderByDescending(c => c.Billed)
            .Take(8)
            .ToList();

        // Loans section (loanRows/loansByNurse computed earlier for name resolution).
        var loans = loansByNurse.Where(kv => kv.Value > 0m)
            .Select(kv => new NurseLoanRow(NameOf(kv.Key), kv.Value))
            .OrderByDescending(l => l.OutstandingBalance).ToList();
        var totalLoans = R(loans.Sum(l => l.OutstandingBalance));

        // Nurse participation.
        var totalServices = inRange.Count;
        var nurseParticipation = inRange
            .GroupBy(e => e.NurseUserId)
            .Select(g =>
            {
                var rev = g.Sum(x => x.SubtotalBeforeSupplies);
                var pay = g.Sum(x => x.NetCompensation);
                var days = g.Select(x => x.ServiceDate).Distinct().Count();
                return new NurseParticipationRow(
                    NameOf(g.Key), g.Count(), days, R(rev), R(pay),
                    Pct(g.Count(), totalServices == 0 ? 1 : totalServices),
                    R(rev - pay), loansByNurse.GetValueOrDefault(g.Key, 0m));
            })
            .OrderByDescending(n => n.RevenueGenerated)
            .ToList();

        // Monthly trend (delivered revenue + margin).
        var trend = Enumerable.Range(0, 6).Select(i =>
        {
            var monthStart = trendFrom.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var xs = execs.Where(e => e.ServiceDate >= monthStart && e.ServiceDate <= monthEnd).ToList();
            var rev = xs.Sum(x => x.SubtotalBeforeSupplies);
            var lab = xs.Sum(x => x.NetCompensation);
            return new TrendPoint($"{monthStart:MM/yyyy}", R(rev), R(rev - lab));
        }).ToList();

        // Health indicators (status + drivers).
        var marginPctVal = Pct(margin, revenue);
        var laborPctVal = Pct(labor, revenue);
        var collectionRate = billed == 0m ? 100m : Pct(collected, billed);
        var loansPctOfLabor = labor == 0m ? 0m : Pct(totalLoans, labor);
        string Band(decimal v, decimal green, decimal amber, bool higherIsBetter)
            => higherIsBetter
                ? (v >= green ? "green" : v >= amber ? "amber" : "red")
                : (v <= green ? "green" : v <= amber ? "amber" : "red");

        var health = new List<HealthIndicator>
        {
            new("margin", "Margen bruto", Band(marginPctVal, 40, 30, true), marginPctVal, $"{marginPctVal:0.0}%", 40,
                "Lo que queda después de pagar a las enfermeras. Meta 40%+.",
                byCategory.OrderBy(c => c.MarginPercent).Take(2).Select(c => $"{c.DisplayName}: {c.MarginPercent:0.0}% de margen").ToList()),
            new("labor", "Costo de nómina", Band(laborPctVal, 60, 70, false), laborPctVal, $"{laborPctVal:0.0}% de ingresos", 60,
                "Parte de los ingresos que se va en pago a enfermeras. Meta menos de 60%.",
                new[] { $"Nómina del período: RD$ {labor:N2}", $"Ingresos del período: RD$ {revenue:N2}" }.ToList()),
            new("collection", "Cobranza", Band(collectionRate, 95, 85, true), collectionRate, $"{collectionRate:0.0}% cobrado", 95,
                "Porcentaje de lo facturado que ya entró en efectivo. Meta 95%+.",
                new[] { $"Pendiente de cobro: RD$ {pending:N2}" }.ToList()),
            new("loans", "Préstamos a enfermeras", Band(loansPctOfLabor, 15, 25, false), loansPctOfLabor, $"RD$ {totalLoans:N2}", 15,
                "Saldo prestado a enfermeras frente a la nómina. Vigilar si supera 15%.",
                loans.Take(3).Select(l => $"{l.NurseName}: RD$ {l.OutstandingBalance:N2}").ToList()),
        };

        // Proactive insights ("things she might want to know").
        var insights = new List<Insight>();
        if (byServiceLine.Count >= 2)
        {
            var best = byServiceLine.OrderByDescending(s => s.MarginPercent).First();
            var worst = byServiceLine.OrderBy(s => s.MarginPercent).First();
            if (best.ServiceLine != worst.ServiceLine && best.MarginPercent - worst.MarginPercent >= 5m)
                insights.Add(new("line_margin", "info", $"{best.ServiceLine} es más rentable",
                    $"{best.ServiceLine} deja {best.MarginPercent:0.0}% de margen vs {worst.MarginPercent:0.0}% de {worst.ServiceLine}.", null));
        }
        var clientBilledTotal = reqClient.Sum(r => r.Total);
        if (topClients.Count > 0 && clientBilledTotal > 0m)
        {
            var top = topClients[0];
            var share = Pct(top.Billed, clientBilledTotal);
            if (share >= 40m)
                insights.Add(new("client_concentration", "warning", "Concentración de ingresos",
                    $"{top.ClientName} representa {share:0.0}% de lo facturado. Diversificar reduce el riesgo.", null));
        }
        if (pending > 0m)
            insights.Add(new("pending", collectionRate < 85m ? "warning" : "info", "Cobros pendientes",
                $"Tienes RD$ {pending:N2} facturados sin cobrar. Confirma los pagos para reflejarlos como ingreso.", "/admin/care-requests"));
        var idleNurses = nurseParticipation.Count(n => n.DaysWorked > 0 && n.DaysWorked < 5);
        if (idleNurses > 0)
            insights.Add(new("idle", "info", "Capacidad ociosa",
                $"{idleNurses} enfermera(s) trabajaron menos de 5 días en el período: capacidad disponible para más servicios.", null));

        return new FinanceOverview(
            from, to,
            new FinanceSummary(
                new Metric(R(revenue), R(revenuePrev)),
                new Metric(R(collected), R(collectedPrev)),
                R(pending),
                new Metric(R(labor), R(laborPrev)),
                new Metric(R(margin), R(marginPrev)),
                marginPctVal,
                new Metric(inRange.Count, inPrev.Count),
                activeNurses),
            byCategory, byServiceLine, topClients, nurseParticipation, loans, totalLoans, trend, health, insights);
    }
}
