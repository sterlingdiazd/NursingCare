using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Payroll;
using NursingCareBackend.Application.Exceptions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class PayrollVoucherService : IPayrollVoucherService
{
    private readonly IAdminPayrollRepository _repository;
    private readonly ICompanyInfoProvider _companyProvider;
    private static readonly CultureInfo DominicanCulture = new("es-DO");

    // Shared palette/format with the payroll report export for a consistent look.
    private const string Ink = "#1F2933";
    private const string Muted = "#667085";
    private const string Line = "#D0D5DD";
    private const string Soft = "#F8FAFC";
    private const string Soft2 = "#F2F4F7";
    private const string Navy = "#1D3557";
    private const string Green = "#247A5A";
    private static readonly Regex RequestIdPattern =
        new(@"solicitud\s+([0-9a-fA-F-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PayrollVoucherService(
        IAdminPayrollRepository repository,
        ICompanyInfoProvider companyProvider)
    {
        _repository = repository;
        _companyProvider = companyProvider;
    }

    public async Task<byte[]> GenerateVoucherAsync(
        Guid periodId,
        Guid nurseId,
        CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetVoucherDataAsync(periodId, nurseId, cancellationToken);

        if (data is null)
        {
            throw new VoucherNotFoundException(periodId, nurseId);
        }

        var company = await _companyProvider.GetAsync(cancellationToken);
        return GeneratePdf(data, company);
    }

    public async Task<byte[]> GenerateBulkVouchersZipAsync(
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var allVoucherData = await _repository.GetAllVoucherDataAsync(periodId, cancellationToken);

        if (allVoucherData.Count == 0)
        {
            throw new VoucherNotFoundException(periodId);
        }

        var company = await _companyProvider.GetAsync(cancellationToken);
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var data in allVoucherData)
            {
                var pdfBytes = GeneratePdf(data, company);
                var entryName = $"voucher-{SanitizeName(data.NurseDisplayName)}.pdf";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdfBytes, cancellationToken);
            }
        }

        return zipStream.ToArray();
    }

    private byte[] GeneratePdf(PayrollVoucherData data, CompanyInfo company)
    {
        var hasAdjustments = data.TotalAdjustments != 0m;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(Ink));
                VoucherFooter(page);

                page.Content().Column(col =>
                {
                    // Header: company (left), then a centered document title + period.
                    // Centered/left content avoids the right-edge clipping that Row +
                    // ConstantItem().AlignRight() produces in this layout.
                    col.Item().Text(company.Name).Bold().FontSize(16).FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(company.Rnc))
                    {
                        col.Item().Text($"RNC: {company.Rnc}").FontSize(8).FontColor(Muted);
                    }
                    if (!string.IsNullOrWhiteSpace(company.Phone))
                    {
                        col.Item().Text($"Tel: {company.Phone}").FontSize(8).FontColor(Muted);
                    }
                    if (!string.IsNullOrWhiteSpace(company.Address))
                    {
                        col.Item().Text(company.Address).FontSize(8).FontColor(Muted);
                    }
                    col.Item().PaddingTop(6).BorderBottom(1).BorderColor(Line).PaddingBottom(8).Column(h =>
                    {
                        h.Item().Text("COMPROBANTE DE PAGO").Bold().FontSize(14).FontColor(Ink).AlignCenter();
                        h.Item().Text($"Período {FormatDate(data.PeriodStartDate)} al {FormatDate(data.PeriodEndDate)}")
                            .FontSize(9).FontColor(Muted).AlignCenter();
                    });

                    col.Item().PaddingTop(12).Element(c => BuildInfoTable(c, data));

                    col.Item().PaddingTop(14).Text("Detalle de servicios").Bold().FontSize(11).FontColor(Navy);
                    col.Item().PaddingTop(5).Element(c => BuildServicesTable(c, data));

                    col.Item().PaddingTop(14).Text("Resumen de pago").Bold().FontSize(11).FontColor(Navy);
                    col.Item().PaddingTop(5).Element(c => BuildTotalsTable(c, data, hasAdjustments));

                    if (data.Deductions.Count > 0)
                    {
                        col.Item().PaddingTop(14).Text("Detalle de deducciones").Bold().FontSize(11).FontColor(Navy);
                        col.Item().PaddingTop(5).Element(c => BuildDeductionsTable(c, data));
                    }

                    if (data.PaymentConfirmed)
                    {
                        col.Item().PaddingTop(14).Text("Confirmación de pago").Bold().FontSize(11).FontColor(Green);
                        col.Item().PaddingTop(5).Element(c => BuildPaymentConfirmationTable(c, data));
                    }
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildInfoTable(IContainer container, PayrollVoucherData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(95);
                cols.RelativeColumn();
                cols.ConstantColumn(95);
                cols.RelativeColumn();
            });

            LabelCell(table, "Enfermera");
            BodyCell(table, data.NurseDisplayName);
            LabelCell(table, "Cédula");
            BodyCell(table, string.IsNullOrWhiteSpace(data.NurseCedula) ? "No registrada" : data.NurseCedula);
            LabelCell(table, "Estado");
            BodyCell(table, FormatStatus(data.PeriodStatus));
            LabelCell(table, "Fecha de pago");
            BodyCell(table, FormatDate(data.PaymentDate));
        });
    }

    private static void BuildServicesTable(IContainer container, PayrollVoucherData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.ConstantColumn(150);
            });

            HeaderCell(table, "Servicio");
            HeaderCell(table, "Monto", alignRight: true);

            foreach (var line in data.Lines)
            {
                var (service, requestId) = DescribeLine(line.Description);
                Cell(table).Column(c =>
                {
                    c.Item().Text(service).SemiBold();
                    c.Item().Text($"Solicitud {requestId}").FontSize(7).FontColor(Muted);
                });
                BodyCell(table, FormatCurrency(line.NetCompensation), alignRight: true);
            }
        });
    }

    private static void BuildTotalsTable(IContainer container, PayrollVoucherData data, bool hasAdjustments)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn();
                cols.ConstantColumn(150);
            });

            HeaderCell(table, "Concepto");
            HeaderCell(table, "Monto", alignRight: true);

            BodyCell(table, "Compensación bruta");
            BodyCell(table, FormatCurrency(data.TotalGross), alignRight: true);

            if (hasAdjustments)
            {
                BodyCell(table, "Ajustes");
                BodyCell(table, FormatCurrency(data.TotalAdjustments), alignRight: true);
            }

            BodyCell(table, "Deducciones");
            BodyCell(table, data.TotalDeductions != 0m
                ? $"-{FormatCurrency(data.TotalDeductions)}"
                : FormatCurrency(0m), alignRight: true);

            BodyCell(table, "Total neto a pagar", highlight: true);
            BodyCell(table, FormatCurrency(data.NetCompensation), highlight: true, alignRight: true);
        });
    }

    private static void BuildDeductionsTable(IContainer container, PayrollVoucherData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(1.4f);
                cols.RelativeColumn(3f);
                cols.ConstantColumn(120);
            });

            HeaderCell(table, "Tipo");
            HeaderCell(table, "Concepto");
            HeaderCell(table, "Monto", alignRight: true);

            foreach (var deduction in data.Deductions)
            {
                BodyCell(table, deduction.DeductionTypeLabel);
                BodyCell(table, deduction.Label);
                BodyCell(table, FormatCurrency(deduction.Amount), alignRight: true);
            }
        });
    }

    // Renders the payment-confirmation block shown once the admin confirms the transfer.
    // Two-column label/value table (no Row + ConstantItem().AlignRight) so nothing clips at
    // the right margin. The "PAGADO" state and bank reference make the comprobante a proof of payment.
    private static void BuildPaymentConfirmationTable(IContainer container, PayrollVoucherData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(150);
                cols.RelativeColumn();
            });

            LabelCell(table, "Estado");
            Cell(table).Background("#ECFDF3").Text("PAGADO").SemiBold().FontColor(Green);

            if (!string.IsNullOrWhiteSpace(data.BankReference))
            {
                LabelCell(table, "Referencia bancaria");
                BodyCell(table, data.BankReference!);
            }

            // Prefer the confirmation timestamp; fall back to the period's scheduled payment date.
            var confirmationDate = data.PaymentConfirmedAtUtc is { } confirmedAt
                ? DateOnly.FromDateTime(confirmedAt)
                : data.PaymentDate;
            LabelCell(table, "Fecha de pago");
            BodyCell(table, FormatDate(confirmationDate));
        });
    }

    private static void VoucherFooter(PageDescriptor page)
    {
        page.Footer().BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Text(text =>
        {
            text.AlignCenter();
            text.Span($"Generado el {DateTime.UtcNow:dd-MM-yyyy HH:mm} UTC   ·   Página ").FontSize(7).FontColor(Muted);
            text.CurrentPageNumber().FontSize(7).FontColor(Muted);
        });
    }

    private static IContainer Cell(TableDescriptor table) =>
        table.Cell().Border(0.5f).BorderColor(Line).Padding(5);

    private static void HeaderCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var descriptor = Cell(table).Background(Soft2);
        var textDescriptor = descriptor.Text(text);
        if (alignRight) textDescriptor.AlignRight();
        textDescriptor.SemiBold().FontColor(Ink);
    }

    private static void LabelCell(TableDescriptor table, string text) =>
        Cell(table).Background(Soft).Text(text).SemiBold().FontColor(Muted);

    private static void BodyCell(TableDescriptor table, string text, bool highlight = false, bool alignRight = false)
    {
        var descriptor = Cell(table);
        if (highlight) descriptor = descriptor.Background("#ECFDF3");
        var textDescriptor = descriptor.Text(text);
        if (alignRight) textDescriptor.AlignRight();
        if (highlight) textDescriptor.SemiBold().FontColor(Green);
    }

    private static (string Service, string RequestId) DescribeLine(string description)
    {
        var match = RequestIdPattern.Match(description);
        var requestId = match.Success ? match.Groups[1].Value : "sin identificador";
        var service = match.Success ? description[..match.Index].Trim(' ', '.', '-', '·') : description.Trim();
        service = service.Replace("Servicio ", string.Empty, StringComparison.OrdinalIgnoreCase);
        service = Humanize(service);
        return (string.IsNullOrWhiteSpace(service) ? "Servicio" : service, requestId);
    }

    private static string Humanize(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return code;
        var spaced = code.Replace('_', ' ').Trim();
        return char.ToUpper(spaced[0], DominicanCulture) + spaced[1..];
    }

    private static string FormatStatus(string status) => status.ToUpperInvariant() switch
    {
        "CLOSED" => "Cerrado",
        "OPEN" => "Abierto",
        _ => status
    };

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string FormatCurrency(decimal value) =>
        $"RD$ {value.ToString("N2", DominicanCulture)}";

    private static string SanitizeName(string name) =>
        Regex.Replace(name, @"[^a-zA-Z0-9\-]", "_");
}
