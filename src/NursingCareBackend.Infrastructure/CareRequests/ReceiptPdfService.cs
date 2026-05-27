using System.Globalization;
using NursingCareBackend.Application.CareRequests;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class ReceiptPdfService : IReceiptPdfService
{
    private static readonly CultureInfo DominicanCulture = new("es-DO");

    private readonly IFiscalSettingsProvider _fiscalProvider;

    public ReceiptPdfService(IFiscalSettingsProvider fiscalProvider)
    {
        _fiscalProvider = fiscalProvider;
    }

    public async Task<byte[]> GenerateAsync(ReceiptPdfData data, CancellationToken cancellationToken = default)
    {
        // Read the issuer fiscal config fresh from the owner-editable SystemSettings (FISCAL_*),
        // falling back to appsettings inside the provider. Resolving per call keeps RNC / ITBIS /
        // legal footer / currency edits live without a redeploy.
        var fiscal = await _fiscalProvider.GetAsync(cancellationToken);

        // Amounts are formatted with the configured currency code (e.g. DOP -> RD$). The es-DO
        // culture drives grouping/decimals; we override the currency symbol from the setting so a
        // currency change is reflected on the document.
        var currencyFormat = (NumberFormatInfo)DominicanCulture.NumberFormat.Clone();
        currencyFormat.CurrencySymbol = ResolveCurrencySymbol(fiscal.CurrencyCode);
        string Money(decimal amount) => amount.ToString("C2", currencyFormat);

        // A document is a formal fiscal comprobante (e-NCF) only when the request carries an issued
        // NCF; otherwise it is a non-fiscal proforma / cuenta de cobro keyed by the SOL- number.
        var isFiscal = !string.IsNullOrWhiteSpace(data.Ncf);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    // Company name from configuration (CompanyInfo:Name), centered layout
                    // avoids right-edge clipping that Row + ConstantItem().AlignRight() produces.
                    col.Item().Text(data.CompanyName).Bold().FontSize(15).AlignCenter();
                    col.Item().Text(isFiscal
                            ? "COMPROBANTE FISCAL ELECTRÓNICO (e-NCF)"
                            : "PROFORMA / CUENTA DE COBRO DE SERVICIOS DE ENFERMERÍA")
                        .SemiBold().FontSize(13).AlignCenter();
                    if (isFiscal && !string.IsNullOrWhiteSpace(fiscal.Rnc))
                        col.Item().Text($"RNC: {fiscal.Rnc}").FontSize(10).AlignCenter();
                    col.Item().Text(isFiscal
                            ? $"e-NCF: {data.Ncf}"
                            : $"Documento No: {data.ReceiptNumber}")
                        .FontSize(11).AlignCenter();
                    col.Item().Text($"Fecha: {data.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm", DominicanCulture)} UTC")
                        .FontSize(10).AlignCenter();
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Text("DATOS DEL SERVICIO").SemiBold().FontSize(12);
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        void AddRow(string label, string value)
                        {
                            table.Cell().Text(label).SemiBold();
                            table.Cell().Text(value);
                        }

                        if (!string.IsNullOrWhiteSpace(data.ClientDisplayName))
                            AddRow("Cliente:", data.ClientDisplayName);
                        if (!string.IsNullOrWhiteSpace(data.ClientIdentificationNumber))
                            AddRow("Cédula:", data.ClientIdentificationNumber);
                        AddRow("Tipo de servicio:", data.CareRequestType);
                        AddRow("Unidades:", $"{data.Unit} {data.UnitType}");
                    });

                    col.Item().PaddingTop(15).Text("DATOS DE FACTURACIÓN").SemiBold().FontSize(12);
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        if (isFiscal)
                        {
                            table.Cell().Text("e-NCF:").SemiBold();
                            table.Cell().Text(data.Ncf!);

                            if (!string.IsNullOrWhiteSpace(fiscal.Rnc))
                            {
                                table.Cell().Text("RNC emisor:").SemiBold();
                                table.Cell().Text(fiscal.Rnc!);
                            }

                            table.Cell().Text("Fecha de emisión:").SemiBold();
                            table.Cell().Text((data.NcfIssuedAtUtc ?? data.PaidAtUtc).ToString("dd/MM/yyyy", DominicanCulture));
                        }

                        // The non-fiscal proforma number (SOL-) is always shown; in fiscal mode it
                        // remains the internal cuenta-de-cobro reference alongside the e-NCF.
                        table.Cell().Text(isFiscal ? "Proforma (cuenta de cobro):" : "Número de proforma:").SemiBold();
                        table.Cell().Text(data.InvoiceNumber);

                        table.Cell().Text("Fecha de proforma:").SemiBold();
                        table.Cell().Text(data.InvoicedAtUtc.ToString("dd/MM/yyyy", DominicanCulture));

                        table.Cell().Text("Referencia bancaria:").SemiBold();
                        table.Cell().Text(data.BankReference);

                        table.Cell().Text("Fecha de pago:").SemiBold();
                        table.Cell().Text(data.PaidAtUtc.ToString("dd/MM/yyyy", DominicanCulture));
                    });

                    col.Item().PaddingTop(15).LineHorizontal(1);
                    // Use table for amount rows to keep right-aligned amounts safe from edge clipping.
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        if (isFiscal && fiscal.ItbisRatePercent > 0m)
                        {
                            // ITBIS shown for transparency on the fiscal comprobante. Health services
                            // are typically exempt (rate 0), so this row only appears when configured.
                            var rate = fiscal.ItbisRatePercent / 100m;
                            var taxableBase = decimal.Round(data.Total / (1m + rate), 2, MidpointRounding.AwayFromZero);
                            var itbis = decimal.Round(data.Total - taxableBase, 2, MidpointRounding.AwayFromZero);

                            table.Cell().Text("Base imponible:").SemiBold();
                            table.Cell().Text(Money(taxableBase)).AlignRight();

                            table.Cell().Text($"ITBIS ({fiscal.ItbisRatePercent:0.##}%):").SemiBold();
                            table.Cell().Text(Money(itbis)).AlignRight();
                        }

                        table.Cell().Text("TOTAL PAGADO:").SemiBold().FontSize(13);
                        table.Cell().Text(Money(data.Total)).SemiBold().FontSize(13).AlignRight();
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1);
                });

                page.Footer().AlignCenter().Column(footer =>
                {
                    if (isFiscal && !string.IsNullOrWhiteSpace(fiscal.LegalFooter))
                        footer.Item().Text(fiscal.LegalFooter!).FontSize(8);

                    if (!isFiscal)
                        footer.Item().Text("Este documento es una proforma / cuenta de cobro y no constituye un comprobante fiscal.")
                            .FontSize(8);

                    footer.Item().Text(text =>
                    {
                        text.Span("Documento generado automáticamente el ").FontSize(8);
                        text.Span(data.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm", DominicanCulture)).FontSize(8);
                    });
                });
            });
        }).GeneratePdf();
    }

    // Map the configured ISO currency code to the symbol shown on the document. DOP keeps the
    // Dominican "RD$"; unknown codes fall back to the code itself so totals stay unambiguous.
    private static string ResolveCurrencySymbol(string currencyCode) => currencyCode?.Trim().ToUpperInvariant() switch
    {
        "DOP" => "RD$",
        "USD" => "US$",
        "EUR" => "€",
        null or "" => "RD$",
        var code => $"{code} ",
    };
}
