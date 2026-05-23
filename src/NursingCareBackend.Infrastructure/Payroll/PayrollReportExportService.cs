using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Payroll;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class PayrollReportExportService : IPayrollReportExportService
{
    private static readonly CultureInfo DominicanCulture = new("es-DO");
    private static readonly Regex RequestIdPattern = new(@"solicitud\s+([0-9a-fA-F-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const string Ink = "#1F2933";
    private const string Muted = "#667085";
    private const string Line = "#D0D5DD";
    private const string Soft = "#F8FAFC";
    private const string Soft2 = "#F2F4F7";
    private const string Navy = "#1D3557";
    private const string Green = "#247A5A";

    public byte[] GeneratePdf(AdminPayrollPeriodDetail period, CompanyInfo company)
    {
        var totals = CalculateTotals(period);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(34);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(7.4f).FontColor(Ink));
                BuildFooter(page, $"Período: {PeriodLabel(period)}");

                page.Content().Column(col =>
                {
                    col.Spacing(11);
                    col.Item().Text("Reporte de Nómina - Revisión Operativa").Bold().FontSize(18).FontColor(Ink);
                    col.Item().Text($"{company.Name}{(string.IsNullOrWhiteSpace(company.Rnc) ? "" : $" · RNC {company.Rnc}")} | Reporte profesional de nómina y soporte de pago").FontSize(9).FontColor(Muted);
                    col.Item().Element(c => BuildMetadataTable(c, period, totals));
                    col.Item().Element(c => BuildKpiTable(c, period, totals));

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Spacing(5);
                            left.Item().Text("Estado de Monto a Pagar").Bold().FontSize(11).FontColor(Navy);
                            left.Item().Element(c => BuildStatementTable(c, totals));
                        });
                        row.ConstantItem(18);
                        row.RelativeItem().Column(right =>
                        {
                            right.Spacing(5);
                            right.Item().Text("Notas de Revisión").Bold().FontSize(11).FontColor(Navy);
                            right.Item().Element(c => BuildNotesTable(c, period, totals));
                        });
                    });

                    col.Item().Text("Resumen por Enfermera").Bold().FontSize(11).FontColor(Navy);
                    col.Item().Element(c => BuildStaffTable(c, period));
                });
            });

            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(34);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(7.4f).FontColor(Ink));
                BuildFooter(page, "Controles y aprobación");

                page.Content().Column(col =>
                {
                    col.Spacing(12);
                    col.Item().Text("Controles y Aprobación").Bold().FontSize(18).FontColor(Ink);
                    col.Item().Text("Validaciones operativas antes de liberar el pago. Los montos provienen del detalle aprobado de nómina.").FontSize(9).FontColor(Muted);
                    col.Item().Element(c => BuildControlsTable(c, period, totals));
                    col.Item().PaddingTop(12).Element(BuildApprovalTable);
                });
            });

            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(6.8f).FontColor(Ink));
                BuildFooter(page, "Detalle de líneas de nómina");

                page.Content().Column(col =>
                {
                    col.Spacing(9);
                    col.Item().Text("Detalle de Líneas de Nómina").Bold().FontSize(17).FontColor(Ink);
                    col.Item().Text("Detalle por servicio que sustenta el monto a pagar. Las descripciones largas se separan en servicio y solicitud para mantener la tabla legible.").FontSize(8.5f).FontColor(Muted);
                    col.Item().Element(c => BuildLineTable(c, period));
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateHtml(AdminPayrollPeriodDetail period, CompanyInfo company)
    {
        var totals = CalculateTotals(period);
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>Reporte de Nómina</title>");
        sb.AppendLine("<style>:root{--ink:#1F2933;--muted:#667085;--line:#D0D5DD;--soft:#F8FAFC;--soft2:#F2F4F7;--navy:#1D3557;--green:#247A5A}*{box-sizing:border-box}body{margin:0;background:#eef2f6;color:var(--ink);font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Arial,sans-serif}main{max-width:1120px;margin:28px auto;background:white;padding:40px 44px;border:1px solid var(--line)}h1{font-size:28px;margin:0 0 4px}h2{font-size:17px;margin:30px 0 10px;color:var(--navy)}.sub{color:var(--muted);font-size:13px}.meta,.kpis{display:grid;gap:10px}.meta{grid-template-columns:repeat(4,1fr);margin-top:22px}.kpis{grid-template-columns:repeat(5,1fr);margin-top:18px}.box{border:1px solid var(--line);background:var(--soft);padding:12px}.label{font-size:11px;color:var(--muted);text-transform:uppercase;letter-spacing:.04em}.value{font-weight:700;font-size:16px;margin-top:4px}.due{color:var(--green)}table{width:100%;border-collapse:collapse;font-size:12px;margin-top:8px;table-layout:fixed}th{background:var(--soft2);text-align:left;color:var(--ink)}td,th{border:1px solid var(--line);padding:7px 8px;vertical-align:top;overflow-wrap:anywhere}tbody tr:nth-child(even){background:var(--soft)}.num{text-align:right;white-space:nowrap}.twocol{display:grid;grid-template-columns:1fr 1fr;gap:18px}.approval{display:grid;grid-template-columns:repeat(3,1fr);gap:20px;margin-top:20px}.sig{height:72px;border:1px solid var(--line);padding:10px;background:var(--soft)}@media print{body{background:white}main{margin:0;max-width:none;border:0}}</style></head><body><main>");
        sb.AppendLine("<h1>Reporte de Nómina - Revisión Operativa</h1>");
        sb.AppendLine($"<div class=\"sub\">{Html(company.Name)} | Período {Html(PeriodLabel(period))}</div>");
        sb.AppendLine("<section class=\"meta\">");
        AppendBox(sb, "Período", PeriodLabel(period));
        AppendBox(sb, "Estado", FormatStatus(period.Status));
        AppendBox(sb, "Corte", FormatDate(period.CutoffDate));
        AppendBox(sb, "Pago", FormatDate(period.PaymentDate));
        sb.AppendLine("</section><section class=\"kpis\">");
        AppendBox(sb, "Líneas", period.Lines.Count.ToString(CultureInfo.InvariantCulture));
        AppendBox(sb, "Enfermeras", period.StaffSummary.Count.ToString(CultureInfo.InvariantCulture));
        AppendBox(sb, "Bruto", FormatCurrency(totals.Gross));
        AppendBox(sb, "Deducciones", FormatCurrency(totals.Deductions));
        AppendBox(sb, "Neto a pagar", FormatCurrency(totals.Net), "due");
        sb.AppendLine("</section><div class=\"twocol\"><section><h2>Estado de Monto a Pagar</h2><table><tbody>");
        foreach (var row in StatementRows(totals))
            sb.AppendLine($"<tr><td>{Html(row.Label)}</td><td class=\"num{(row.IsTotal ? " due" : "")}\">{Html(FormatCurrency(row.Amount))}</td></tr>");
        sb.AppendLine("</tbody></table></section><section><h2>Notas de Revisión</h2><table><tbody>");
        sb.AppendLine($"<tr><td>Estado de control</td><td><strong>{Html(ControlStatus(period, totals))}</strong></td></tr>");
        sb.AppendLine("<tr><td>Conciliación</td><td>El resumen por enfermera coincide con el detalle de líneas y el total neto.</td></tr>");
        sb.AppendLine("<tr><td>Aprobación</td><td>Verificar servicios completados, conservar el archivo fuente, anexar comprobantes firmados y aprobar la liberación del pago.</td></tr>");
        sb.AppendLine("</tbody></table></section></div><h2>Resumen por Enfermera</h2><table><thead><tr><th>Enfermera</th><th class=\"num\">Líneas</th><th class=\"num\">Bruto</th><th class=\"num\">Deducciones</th><th class=\"num\">Neto</th></tr></thead><tbody>");
        foreach (var staff in period.StaffSummary)
            sb.AppendLine($"<tr><td>{Html(staff.NurseDisplayName)}</td><td class=\"num\">{staff.LineCount}</td><td class=\"num\">{Html(FormatCurrency(staff.GrossCompensation))}</td><td class=\"num\">{Html(FormatCurrency(staff.DeductionsTotal))}</td><td class=\"num due\">{Html(FormatCurrency(staff.NetCompensation))}</td></tr>");
        sb.AppendLine("</tbody></table><h2>Detalle de Líneas de Nómina</h2><table><thead><tr><th style=\"width:5%\">#</th><th style=\"width:20%\">Enfermera</th><th>Servicio / solicitud</th><th class=\"num\" style=\"width:13%\">Base</th><th class=\"num\" style=\"width:13%\">Neto</th></tr></thead><tbody>");
        var index = 1;
        foreach (var line in period.Lines)
        {
            var details = DescribeLine(line.Description);
            sb.AppendLine($"<tr><td>{index++}</td><td>{Html(line.NurseDisplayName)}</td><td><strong>{Html(details.Service)}</strong><br><span class=\"sub\">Solicitud {Html(details.RequestId)}</span></td><td class=\"num\">{Html(FormatCurrency(line.BaseCompensation))}</td><td class=\"num\">{Html(FormatCurrency(line.NetCompensation))}</td></tr>");
        }
        sb.AppendLine("</tbody></table><h2>Aprobación</h2><div class=\"approval\"><div class=\"sig\">Preparado por / Fecha</div><div class=\"sig\">Revisado por / Fecha</div><div class=\"sig\">Aprobado por / Fecha</div></div></main></body></html>");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] GenerateXlsx(AdminPayrollPeriodDetail period, CompanyInfo company)
    {
        var totals = CalculateTotals(period);
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "[Content_Types].xml", XlsxContentTypes());
            AddEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            AddEntry(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/><Relationship Id=\"rId3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet3.xml\"/><Relationship Id=\"rId4\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
            AddEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Resumen Ejecutivo\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Resumen Enfermeras\" sheetId=\"2\" r:id=\"rId2\"/><sheet name=\"Líneas Nómina\" sheetId=\"3\" r:id=\"rId3\"/></sheets></workbook>");
            AddEntry(archive, "xl/styles.xml", XlsxStyles());
            AddEntry(archive, "xl/worksheets/sheet1.xml", SummarySheet(period, totals, company.Name));
            AddEntry(archive, "xl/worksheets/sheet2.xml", StaffSheet(period));
            AddEntry(archive, "xl/worksheets/sheet3.xml", LinesSheet(period));
        }
        return stream.ToArray();
    }

    private static void BuildFooter(PageDescriptor page, string label)
    {
        page.Footer().BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(7).FontColor(Muted);
            row.ConstantItem(90).AlignRight().Text(text =>
            {
                text.Span("Página ").FontSize(7).FontColor(Muted);
                text.CurrentPageNumber().FontSize(7).FontColor(Muted);
            });
        });
    }

    private static void BuildMetadataTable(IContainer container, AdminPayrollPeriodDetail period, PayrollReportTotals totals)
    {
        var rows = new[]
        {
            ("Período de nómina", PeriodLabel(period), "Estado", FormatStatus(period.Status)),
            ("Inicio", FormatDate(period.StartDate), "Fin", FormatDate(period.EndDate)),
            ("Corte", FormatDate(period.CutoffDate), "Pago", FormatDate(period.PaymentDate)),
            ("Preparado", DateTime.UtcNow.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC", "Control", ControlStatus(period, totals))
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(105);
                cols.RelativeColumn(2);
                cols.ConstantColumn(90);
                cols.RelativeColumn(2);
            });

            foreach (var row in rows)
            {
                LabelCell(table, row.Item1);
                BodyCell(table, row.Item2);
                LabelCell(table, row.Item3);
                BodyCell(table, row.Item4);
            }
        });
    }

    private static void BuildKpiTable(IContainer container, AdminPayrollPeriodDetail period, PayrollReportTotals totals)
    {
        var rows = new[]
        {
            ("Líneas", period.Lines.Count.ToString(CultureInfo.InvariantCulture)),
            ("Enfermeras", period.StaffSummary.Count.ToString(CultureInfo.InvariantCulture)),
            ("Bruto", FormatCurrency(totals.Gross)),
            ("Deducciones", FormatCurrency(totals.Deductions)),
            ("Neto a pagar", FormatCurrency(totals.Net))
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.RelativeColumn();
                cols.RelativeColumn(1.4f);
                cols.RelativeColumn(1.4f);
                cols.RelativeColumn(1.5f);
            });

            foreach (var row in rows) HeaderCell(table, row.Item1);
            foreach (var row in rows) BodyCell(table, row.Item2, row.Item1 == "Neto a pagar", alignRight: row.Item1 is "Bruto" or "Deducciones" or "Neto a pagar");
        });
    }

    private static void BuildStatementTable(IContainer container, PayrollReportTotals totals)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.4f);
                cols.ConstantColumn(130);
            });

            HeaderCell(table, "Componente");
            HeaderCell(table, "Monto", alignRight: true);
            foreach (var row in StatementRows(totals))
            {
                BodyCell(table, row.Label, row.IsTotal);
                BodyCell(table, FormatCurrency(row.Amount), row.IsTotal, alignRight: true);
            }
        });
    }

    private static void BuildNotesTable(IContainer container, AdminPayrollPeriodDetail period, PayrollReportTotals totals)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(115);
                cols.RelativeColumn();
            });

            LabelCell(table, "Estado de control");
            BodyCell(table, ControlStatus(period, totals), ControlStatus(period, totals) == "APROBADO");
            LabelCell(table, "Conciliación");
            BodyCell(table, "El resumen por enfermera coincide con el detalle de líneas y el total neto.");
            LabelCell(table, "Aprobación");
            BodyCell(table, "Verificar servicios completados, conservar el archivo fuente, anexar comprobantes firmados y aprobar la liberación del pago.");
        });
    }

    private static void BuildStaffTable(IContainer container, AdminPayrollPeriodDetail period)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.5f);
                cols.ConstantColumn(55);
                cols.ConstantColumn(112);
                cols.ConstantColumn(112);
                cols.ConstantColumn(120);
            });

            HeaderCell(table, "Enfermera");
            HeaderCell(table, "Líneas", alignRight: true);
            HeaderCell(table, "Bruto", alignRight: true);
            HeaderCell(table, "Deducciones", alignRight: true);
            HeaderCell(table, "Neto", alignRight: true);

            foreach (var row in period.StaffSummary)
            {
                BodyCell(table, row.NurseDisplayName);
                BodyCell(table, row.LineCount.ToString(CultureInfo.InvariantCulture), alignRight: true);
                BodyCell(table, FormatCurrency(row.GrossCompensation), alignRight: true);
                BodyCell(table, FormatCurrency(row.DeductionsTotal), alignRight: true);
                BodyCell(table, FormatCurrency(row.NetCompensation), highlight: true, alignRight: true);
            }
        });
    }

    private static void BuildControlsTable(IContainer container, AdminPayrollPeriodDetail period, PayrollReportTotals totals)
    {
        var staffLines = period.StaffSummary.Sum(x => x.LineCount);
        var staffNet = period.StaffSummary.Sum(x => x.NetCompensation);
        var uniqueNurses = period.Lines.Select(x => x.NurseUserId).Distinct().Count();
        var checks = new[]
        {
            ("Conteo de líneas coincide con resumen", staffLines.ToString(CultureInfo.InvariantCulture), period.Lines.Count.ToString(CultureInfo.InvariantCulture), staffLines == period.Lines.Count),
            ("Neto coincide con resumen", FormatCurrency(staffNet), FormatCurrency(totals.Net), staffNet == totals.Net),
            ("Bruto menos deducciones coincide con neto", FormatCurrency(totals.Gross - totals.Deductions), FormatCurrency(totals.Net), totals.Gross - totals.Deductions == totals.Net),
            ("Cantidad de enfermeras coincide", period.StaffSummary.Count.ToString(CultureInfo.InvariantCulture), uniqueNurses.ToString(CultureInfo.InvariantCulture), period.StaffSummary.Count == uniqueNurses)
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.6f);
                cols.RelativeColumn(1.4f);
                cols.RelativeColumn(1.4f);
                cols.ConstantColumn(95);
            });

            HeaderCell(table, "Control");
            HeaderCell(table, "Esperado", alignRight: true);
            HeaderCell(table, "Actual", alignRight: true);
            HeaderCell(table, "Resultado");

            foreach (var check in checks)
            {
                BodyCell(table, check.Item1);
                BodyCell(table, check.Item2, alignRight: true);
                BodyCell(table, check.Item3, alignRight: true);
                BodyCell(table, check.Item4 ? "APROBADO" : "REVISAR", check.Item4);
            }
        });
    }

    private static void BuildApprovalTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            foreach (var header in new[] { "Preparado por", "Revisado por", "Aprobado por" }) HeaderCell(table, header);
            foreach (var _ in new[] { 1, 2, 3 }) Cell(table).Height(58).Text("");
            foreach (var footer in new[] { "Fecha", "Fecha", "Fecha" }) BodyCell(table, footer);
        });
    }

    private static void BuildLineTable(IContainer container, AdminPayrollPeriodDetail period)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(30);
                cols.RelativeColumn(1.7f);
                cols.RelativeColumn(4.4f);
                cols.ConstantColumn(108);
                cols.ConstantColumn(108);
            });

            HeaderCell(table, "#");
            HeaderCell(table, "Enfermera");
            HeaderCell(table, "Servicio / solicitud");
            HeaderCell(table, "Base", alignRight: true);
            HeaderCell(table, "Neto", alignRight: true);

            var index = 1;
            foreach (var line in period.Lines)
            {
                var details = DescribeLine(line.Description);
                BodyCell(table, index++.ToString(CultureInfo.InvariantCulture), alignRight: true);
                BodyCell(table, line.NurseDisplayName);
                Cell(table).Column(col =>
                {
                    col.Item().Text(details.Service).SemiBold();
                    col.Item().Text($"Solicitud {details.RequestId}").FontSize(6.2f).FontColor(Muted);
                });
                BodyCell(table, FormatCurrency(line.BaseCompensation), alignRight: true);
                BodyCell(table, FormatCurrency(line.NetCompensation), alignRight: true);
            }
        });
    }

    private static PayrollReportTotals CalculateTotals(AdminPayrollPeriodDetail period)
    {
        var baseTotal = period.Lines.Sum(x => x.BaseCompensation);
        var transport = period.Lines.Sum(x => x.TransportIncentive);
        var complexity = period.Lines.Sum(x => x.ComplexityBonus);
        var supplies = period.Lines.Sum(x => x.MedicalSuppliesCompensation);
        var adjustments = period.Lines.Sum(x => x.AdjustmentsTotal);
        // Deductions are period-level per nurse; take them from the staff summary (subtracted once),
        // not from the deduction-free service lines.
        var deductions = period.StaffSummary.Sum(x => x.DeductionsTotal);
        var gross = baseTotal + transport + complexity + supplies + adjustments;
        var net = gross - deductions;
        return new PayrollReportTotals(baseTotal, transport, complexity, supplies, adjustments, deductions, gross, net);
    }

    private static IEnumerable<StatementRow> StatementRows(PayrollReportTotals totals)
    {
        yield return new("Compensación base", totals.Base);
        yield return new("Incentivos de transporte", totals.Transport);
        yield return new("Bonos por complejidad", totals.Complexity);
        yield return new("Compensación por insumos médicos", totals.Supplies);
        yield return new("Ajustes aprobados", totals.Adjustments);
        yield return new("Menos deducciones", totals.Deductions);
        yield return new("Total neto a pagar", totals.Net, true);
    }

    private static string ControlStatus(AdminPayrollPeriodDetail period, PayrollReportTotals totals)
    {
        var staffLines = period.StaffSummary.Sum(x => x.LineCount);
        var staffNet = period.StaffSummary.Sum(x => x.NetCompensation);
        return staffLines == period.Lines.Count && staffNet == totals.Net && totals.Gross - totals.Deductions == totals.Net
            ? "APROBADO"
            : "REVISAR";
    }

    private static IContainer Cell(TableDescriptor table)
    {
        return table.Cell().Border(0.5f).BorderColor(Line).Padding(5);
    }

    private static void HeaderCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var descriptor = Cell(table).Background(Soft2);
        var textDescriptor = descriptor.Text(text);
        if (alignRight) textDescriptor.AlignRight();
        textDescriptor.SemiBold().FontColor(Ink);
    }

    private static void LabelCell(TableDescriptor table, string text)
    {
        Cell(table).Background(Soft).Text(text).SemiBold().FontColor(Muted);
    }

    private static void BodyCell(TableDescriptor table, string text, bool highlight = false, bool alignRight = false)
    {
        var descriptor = Cell(table);
        if (highlight) descriptor = descriptor.Background("#ECFDF3");
        var textDescriptor = descriptor.Text(text);
        if (alignRight) textDescriptor.AlignRight();
        if (highlight) textDescriptor.SemiBold().FontColor(Green);
    }

    private static LineDescription DescribeLine(string description)
    {
        var match = RequestIdPattern.Match(description);
        var requestId = match.Success ? match.Groups[1].Value : "sin identificador";
        var service = match.Success ? description[..match.Index].Trim(' ', '.', '-', '·') : description.Trim();
        service = service.Replace("Servicio ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return new LineDescription(string.IsNullOrWhiteSpace(service) ? "Servicio" : service, requestId);
    }

    private static string PeriodLabel(AdminPayrollPeriodDetail period) =>
        $"{period.StartDate:yyyy-MM-dd} al {period.EndDate:yyyy-MM-dd}";

    private static string FormatStatus(string status) => status.ToUpperInvariant() switch
    {
        "CLOSED" => "Cerrado",
        "OPEN" => "Abierto",
        _ => status
    };

    private static string FormatDate(DateOnly date) => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static string FormatCurrency(decimal value) => $"RD${value.ToString("N2", DominicanCulture)}";

    private static string Html(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static void AppendBox(StringBuilder sb, string label, string value, string className = "") =>
        sb.AppendLine($"<div class=\"box\"><div class=\"label\">{Html(label)}</div><div class=\"value {className}\">{Html(value)}</div></div>");

    private static string XlsxContentTypes() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet3.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>";

    private static string XlsxStyles() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><color rgb=\"FF1F2933\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF1D3557\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFill=\"1\" applyFont=\"1\"/><xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>";

    private static string SummarySheet(AdminPayrollPeriodDetail period, PayrollReportTotals totals, string companyName)
    {
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Reporte de Nómina - Revisión Operativa" },
            new object?[] { "Empresa", companyName },
            new object?[] { "Período de nómina", PeriodLabel(period) },
            new object?[] { "Estado", FormatStatus(period.Status) },
            new object?[] { "Corte", FormatDate(period.CutoffDate) },
            new object?[] { "Pago", FormatDate(period.PaymentDate) },
            new object?[] { "Líneas", period.Lines.Count },
            new object?[] { "Enfermeras", period.StaffSummary.Count },
            new object?[] { "Bruto", totals.Gross },
            new object?[] { "Deducciones", totals.Deductions },
            new object?[] { "Neto a pagar", totals.Net },
            new object?[] { },
            new object?[] { "Estado de Monto a Pagar" }
        };
        rows.AddRange(StatementRows(totals).Select(row => new object?[] { row.Label, row.Amount }));
        return WorksheetXml(rows, headerRows: 1, columnWidths: new[] { 34d, 24d, 18d, 18d });
    }

    private static string StaffSheet(AdminPayrollPeriodDetail period)
    {
        var rows = new List<IReadOnlyList<object?>> { new object?[] { "Enfermera", "Líneas", "Bruto", "Transporte", "Ajustes", "Deducciones", "Neto", "Promedio neto por línea" } };
        rows.AddRange(period.StaffSummary.Select(row => new object?[]
        {
            row.NurseDisplayName,
            row.LineCount,
            row.GrossCompensation,
            row.TransportIncentives,
            row.AdjustmentsTotal,
            row.DeductionsTotal,
            row.NetCompensation,
            row.LineCount > 0 ? row.NetCompensation / row.LineCount : 0m
        }));
        return WorksheetXml(rows, headerRows: 1, columnWidths: new[] { 28d, 10d, 16d, 16d, 16d, 16d, 16d, 24d });
    }

    private static string LinesSheet(AdminPayrollPeriodDetail period)
    {
        var rows = new List<IReadOnlyList<object?>> { new object?[] { "#", "Enfermera", "Servicio", "Solicitud", "Base", "Transporte", "Complejidad", "Insumos", "Ajustes", "Deducciones", "Neto" } };
        var index = 1;
        rows.AddRange(period.Lines.Select(row =>
        {
            var details = DescribeLine(row.Description);
            return new object?[]
            {
                index++,
                row.NurseDisplayName,
                details.Service,
                details.RequestId,
                row.BaseCompensation,
                row.TransportIncentive,
                row.ComplexityBonus,
                row.MedicalSuppliesCompensation,
                row.AdjustmentsTotal,
                row.DeductionsTotal,
                row.NetCompensation
            };
        }));
        return WorksheetXml(rows, headerRows: 1, columnWidths: new[] { 7d, 28d, 26d, 42d, 16d, 16d, 16d, 16d, 16d, 16d, 16d });
    }

    private static string WorksheetXml(IReadOnlyList<IReadOnlyList<object?>> rows, int headerRows, IReadOnlyList<double> columnWidths)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        if (columnWidths.Count > 0)
        {
            sb.Append("<cols>");
            for (var index = 0; index < columnWidths.Count; index++)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<col min=\"{index + 1}\" max=\"{index + 1}\" width=\"{columnWidths[index]}\" customWidth=\"1\"/>");
            }
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<row r=\"{r + 1}\">");
            for (var c = 0; c < rows[r].Count; c++)
            {
                var value = rows[r][c];
                if (value is null) continue;
                var cellRef = $"{ColumnName(c + 1)}{r + 1}";
                var style = r < headerRows ? " s=\"1\"" : value is decimal ? " s=\"2\"" : string.Empty;
                if (value is int or decimal)
                {
                    sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{cellRef}\"{style}><v>{Convert.ToString(value, CultureInfo.InvariantCulture)}</v></c>");
                }
                else
                {
                    sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{cellRef}\" t=\"inlineStr\"{style}><is><t>{SecurityElement.Escape(Convert.ToString(value, CultureInfo.InvariantCulture))}</t></is></c>");
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static string ColumnName(int index)
    {
        var dividend = index;
        var name = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            dividend = (dividend - modulo) / 26;
        }
        return name;
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private sealed record PayrollReportTotals(decimal Base, decimal Transport, decimal Complexity, decimal Supplies, decimal Adjustments, decimal Deductions, decimal Gross, decimal Net);

    private sealed record StatementRow(string Label, decimal Amount, bool IsTotal = false);

    private sealed record LineDescription(string Service, string RequestId);
}
