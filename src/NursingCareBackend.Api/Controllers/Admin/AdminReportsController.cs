using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NursingCareBackend.Api.Extensions;
using NursingCareBackend.Application.AdminPortal.Reports;
using NursingCareBackend.Domain.Identity;

namespace NursingCareBackend.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = SystemRoles.Admin)]
public sealed class AdminReportsController : ControllerBase
{
    private readonly GetAdminReportHandler _handler;

    private static readonly HashSet<string> ValidReportKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "care-request-pipeline",
        "assignment-approval-backlog",
        "nurse-onboarding",
        "active-inactive-users",
        "nurse-utilization",
        "care-request-completion",
        "price-usage-summary",
        "notification-volume",
        "payroll-summary"
    };

    public AdminReportsController(GetAdminReportHandler handler)
    {
        _handler = handler;
    }

    [HttpGet("{reportKey}")]
    public async Task<IActionResult> GetReport(
        [FromRoute] string reportKey,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!ValidReportKeys.Contains(reportKey))
        {
            return this.ProblemResponse(
                404,
                "Reporte no encontrado.",
                $"El reporte solicitado '{reportKey}' no existe.");
        }

        try
        {
            var data = await _handler.HandleAsync(reportKey, from, to, pageNumber, pageSize, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return this.ProblemResponse(
                500,
                "Error al procesar el reporte",
                ex.Message);
        }
    }

    [HttpGet("{reportKey}/export")]
    public async Task<IActionResult> ExportReport(
        [FromRoute] string reportKey,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? pageNumber,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!ValidReportKeys.Contains(reportKey))
        {
            return this.ProblemResponse(
                404,
                "Reporte no encontrado.",
                $"El reporte solicitado '{reportKey}' no existe.");
        }

        try
        {
            var data = await _handler.HandleAsync(reportKey, from, to, pageNumber, pageSize, cancellationToken);
            var csvString = GenerateCsvForReport(reportKey, data);
            
            var bytes = Encoding.UTF8.GetBytes(csvString);
            var result = new FileContentResult(bytes, "text/csv")
            {
                FileDownloadName = $"{reportKey}-{DateTime.UtcNow:yyyyMMdd}.csv"
            };
            
            // Allow JS to see the content-disposition header for filename extraction
            HttpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return result;
        }
        catch (Exception ex)
        {
            return this.ProblemResponse(
                500,
                "Error al exportar el reporte",
                ex.Message);
        }
    }

    private string GenerateCsvForReport(string reportKey, object data)
    {
        var sb = new StringBuilder();

        switch (data)
        {
            case CareRequestPipelineReport r:
                sb.AppendLine("Metrica,Valor");
                sb.AppendLine($"Pendiente,{r.PendingCount}");
                sb.AppendLine($"Aprobada,{r.ApprovedCount}");
                sb.AppendLine($"Completada,{r.CompletedCount}");
                sb.AppendLine($"Rechazada,{r.RejectedCount}");
                sb.AppendLine($"Sin asignar,{r.UnassignedCount}");
                sb.AppendLine($"Vencidas,{r.OverdueCount}");
                break;
                
            case AssignmentApprovalBacklogReport r:
                sb.AppendLine("Metrica,Valor");
                sb.AppendLine($"Pendientes sin enfermera,{r.PendingUnassignedCount}");
                sb.AppendLine($"Pendientes con enfermera esperando aprobacion,{r.PendingAssignedAwaitingApprovalCount}");
                sb.AppendLine($"Dias promedio en espera,{r.AverageDaysPending:F2}");
                break;
                
            case NurseOnboardingReport r:
                sb.AppendLine("Metrica,Valor");
                sb.AppendLine($"Total registradas,{r.TotalRegisteredCount}");
                sb.AppendLine($"Pendientes de revision,{r.PendingReviewCount}");
                sb.AppendLine($"Activas,{r.ActiveCount}");
                sb.AppendLine($"Inactivas,{r.InactiveCount}");
                sb.AppendLine($"Completadas en periodo,{r.CompletedThisPeriodCount}");
                break;
                
            case ActiveInactiveUsersReport r:
                sb.AppendLine("Perfil,Activas,Inactivas");
                sb.AppendLine($"Administrador,{r.AdminActiveCount},{r.AdminInactiveCount}");
                sb.AppendLine($"Cliente,{r.ClientActiveCount},{r.ClientInactiveCount}");
                sb.AppendLine($"Enfermera,{r.NurseActiveCount},{r.NurseInactiveCount}");
                break;
                
            case NurseUtilizationReport r:
                sb.AppendLine("ID Enfermera,Nombre,Total Asignadas,Completadas,Pendientes,Tasa de Cierre");
                foreach (var row in r.Rows)
                {
                    sb.AppendLine($"{EscapeCsv(row.NurseId)},{EscapeCsv(row.NurseName)},{row.TotalAssigned},{row.Completed},{row.Pending},{row.CompletionRate:P2}");
                }
                break;
                
            case CareRequestCompletionReport r:
                sb.AppendLine("Metrica,Valor");
                sb.AppendLine($"Total completadas,{r.TotalCompletedCount}");
                sb.AppendLine($"Dias promedio hasta cierre,{r.AverageDaysToComplete:F2}");
                sb.AppendLine("");
                sb.AppendLine("Mes,Completadas");
                foreach (var kvp in r.CompletionsByRange)
                {
                    sb.AppendLine($"{EscapeCsv(kvp.Key)},{kvp.Value}");
                }
                break;
                
            case PriceUsageSummaryReport r:
                sb.AppendLine("Tipo de Servicio,Conteo,Total Promedio,Ingresos Totales");
                foreach (var row in r.TopRequestTypes)
                {
                    sb.AppendLine($"{EscapeCsv(row.RequestType)},{row.Count},{row.AverageTotal:F2},{row.TotalRevenue:F2}");
                }
                break;
                
            case NotificationVolumeReport r:
                sb.AppendLine("Metrica,Valor");
                sb.AppendLine($"Total notificaciones,{r.TotalNotificationsCount}");
                sb.AppendLine($"Notificaciones sin leer,{r.UnreadNotificationsCount}");
                sb.AppendLine($"Acciones pendientes,{r.PendingActionItemsCount}");
                sb.AppendLine("");
                sb.AppendLine("Categoria,Conteo");
                foreach (var kvp in r.NotificationsByCategory)
                {
                    sb.AppendLine($"{EscapeCsv(kvp.Key)},{kvp.Value}");
                }
                break;

            case PayrollSummaryReport r:
                sb.AppendLine("Periodo,Fecha Inicio,Fecha Fin,Fecha Corte,Fecha Pago");
                sb.AppendLine($"{EscapeCsv(r.PeriodLabel)},{r.StartDate:yyyy-MM-dd},{r.EndDate:yyyy-MM-dd},{r.CutoffDate:yyyy-MM-dd},{r.PaymentDate:yyyy-MM-dd}");
                sb.AppendLine("");
                sb.AppendLine("Enfermera,Servicios,Bruto,Transporte,Ajustes,Deducciones,Neto");
                foreach (var row in r.Staff)
                {
                    sb.AppendLine($"{EscapeCsv(row.NurseName)},{row.ServiceCount},{row.GrossCompensation:F2},{row.TransportIncentives:F2},{row.AdjustmentsTotal:F2},{row.DeductionsTotal:F2},{row.NetCompensation:F2}");
                }
                sb.AppendLine("");
                sb.AppendLine("Enfermera,Solicitud,Tipo,Categoria,Fecha Ejecucion,Empleo,Variante,Total Servicio,Base,Transporte,Complejidad,Insumos,Ajustes,Deducciones,Neto");
                foreach (var row in r.Services)
                {
                    sb.AppendLine($"{EscapeCsv(row.NurseName)},{EscapeCsv(row.CareRequestId)},{EscapeCsv(row.CareRequestType)},{EscapeCsv(row.PricingCategoryCode)},{row.ExecutedAtUtc:yyyy-MM-dd},{EscapeCsv(row.EmploymentType)},{EscapeCsv(row.ServiceVariant)},{row.CareRequestTotal:F2},{row.BaseCompensation:F2},{row.TransportIncentive:F2},{row.ComplexityBonus:F2},{row.MedicalSuppliesCompensation:F2},{row.AdjustmentsTotal:F2},{row.DeductionsTotal:F2},{row.NetCompensation:F2}");
                }
                break;
                
            default:
                sb.AppendLine("Datos no disponibles");
                break;
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('\"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
