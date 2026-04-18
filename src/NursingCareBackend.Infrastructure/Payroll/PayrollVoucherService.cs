using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NursingCareBackend.Application.AdminPortal.Payroll;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Infrastructure.Payroll;

public sealed class CompanyInfoOptions
{
    public const string SectionName = "CompanyInfo";
    public string Name { get; set; } = "NursingCare";
    public string? Rnc { get; set; }
}

public sealed class PayrollVoucherService : IPayrollVoucherService
{
    private readonly IAdminPayrollRepository _repository;
    private readonly CompanyInfoOptions _companyInfo;
    private static readonly CultureInfo DominicanCulture = new("es-DO");

    public PayrollVoucherService(
        IAdminPayrollRepository repository,
        IOptions<CompanyInfoOptions> companyInfo)
    {
        _repository = repository;
        _companyInfo = companyInfo.Value;
    }

    public async Task<byte[]> GenerateVoucherAsync(
        Guid periodId,
        Guid nurseId,
        CancellationToken cancellationToken = default)
    {
        var data = await _repository.GetVoucherDataAsync(periodId, nurseId, cancellationToken);

        if (data is null)
        {
            throw new InvalidOperationException("Periodo o enfermera no encontrado.");
        }

        return GeneratePdf(data);
    }

    public async Task<byte[]> GenerateBulkVouchersZipAsync(
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        var allVoucherData = await _repository.GetAllVoucherDataAsync(periodId, cancellationToken);

        if (allVoucherData.Count == 0)
        {
            throw new InvalidOperationException("Periodo no encontrado o sin lineas de nomina.");
        }

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var data in allVoucherData)
            {
                var pdfBytes = GeneratePdf(data);
                var entryName = $"voucher-{SanitizeName(data.NurseDisplayName)}.pdf";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pdfBytes, cancellationToken);
            }
        }

        return zipStream.ToArray();
    }

    private byte[] GeneratePdf(PayrollVoucherData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Content().Column(col =>
                {
                    // Company header
                    col.Item().BorderBottom(1).PaddingBottom(6).Column(header =>
                    {
                        header.Item().Text(_companyInfo.Name)
                            .Bold().FontSize(14);
                        if (!string.IsNullOrWhiteSpace(_companyInfo.Rnc))
                        {
                            header.Item().Text($"RNC: {_companyInfo.Rnc}")
                                .FontSize(9).FontColor(Colors.Grey.Darken2);
                        }
                    });

                    col.Item().PaddingTop(8).PaddingBottom(8)
                        .Text("COMPROBANTE DE PAGO")
                        .Bold().FontSize(13).AlignCenter();

                    // Period info
                    col.Item().BorderBottom(1).BorderTop(1).PaddingVertical(6).Column(period =>
                    {
                        period.Item().Text($"Periodo: {FormatDate(data.PeriodStartDate)} al {FormatDate(data.PeriodEndDate)}");
                        period.Item().Text($"Fecha de Pago: {FormatDate(data.PaymentDate)}");
                        period.Item().Text($"Estado: {data.PeriodStatus}");
                    });

                    // Nurse info
                    col.Item().PaddingTop(6).PaddingBottom(6).BorderBottom(1).Column(nurse =>
                    {
                        nurse.Item().Text($"Enfermera: {data.NurseDisplayName}").Bold();
                        nurse.Item().Text($"Cedula: {data.NurseCedula ?? "No registrada"}");
                    });

                    // Line items table
                    col.Item().PaddingTop(8).Text("DETALLE DE SERVICIOS").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Descripcion").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Base").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Transp.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Compl.").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Insumos").Bold();
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Neto").Bold();
                        });

                        // Data rows
                        foreach (var line in data.Lines)
                        {
                            table.Cell().Padding(3).Text(line.Description);
                            table.Cell().Padding(3).AlignRight().Text(FormatCurrency(line.BaseCompensation));
                            table.Cell().Padding(3).AlignRight().Text(FormatCurrency(line.TransportIncentive));
                            table.Cell().Padding(3).AlignRight().Text(FormatCurrency(line.ComplexityBonus));
                            table.Cell().Padding(3).AlignRight().Text(FormatCurrency(line.MedicalSuppliesCompensation));
                            table.Cell().Padding(3).AlignRight().Text(FormatCurrency(line.NetCompensation));
                        }
                    });

                    // Totals
                    col.Item().PaddingTop(8).BorderTop(1).PaddingTop(6).Column(totals =>
                    {
                        totals.Item().Text("TOTALES").Bold().FontSize(10);
                        totals.Item().PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("Compensacion Bruta:");
                            row.ConstantItem(120).AlignRight().Text(FormatCurrency(data.TotalGross));
                        });
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Transporte:");
                            row.ConstantItem(120).AlignRight().Text(FormatCurrency(data.TotalTransport));
                        });
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Ajustes:");
                            row.ConstantItem(120).AlignRight().Text(FormatCurrency(data.TotalAdjustments));
                        });
                        totals.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Deducciones:");
                            row.ConstantItem(120).AlignRight()
                                .Text($"-{FormatCurrency(data.TotalDeductions)}");
                        });
                        totals.Item().PaddingTop(4).BorderTop(1).PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text("COMPENSACION NETA:").Bold();
                            row.ConstantItem(120).AlignRight()
                                .Text(FormatCurrency(data.NetCompensation)).Bold();
                        });
                    });

                    // Deductions detail (only if any)
                    if (data.Deductions.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("DETALLE DE DEDUCCIONES").Bold().FontSize(10);
                        col.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(2);
                                cols.RelativeColumn(4);
                                cols.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Tipo").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Concepto").Bold();
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Monto").Bold();
                            });

                            foreach (var deduction in data.Deductions)
                            {
                                table.Cell().Padding(3).Text(deduction.DeductionTypeLabel);
                                table.Cell().Padding(3).Text(deduction.Label);
                                table.Cell().Padding(3).AlignRight().Text(FormatCurrency(deduction.Amount));
                            }
                        });
                    }

                    // Generation timestamp
                    col.Item().PaddingTop(12).AlignRight()
                        .Text($"Generado el {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string FormatDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy");

    private static string FormatCurrency(decimal value) =>
        $"RD$ {value.ToString("N2", DominicanCulture)}";

    private static string SanitizeName(string name) =>
        Regex.Replace(name, @"[^a-zA-Z0-9\-]", "_");
}
