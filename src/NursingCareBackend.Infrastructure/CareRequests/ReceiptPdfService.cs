using System.Globalization;
using NursingCareBackend.Application.CareRequests;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NursingCareBackend.Infrastructure.CareRequests;

public sealed class ReceiptPdfService : IReceiptPdfService
{
    private static readonly CultureInfo DominicanCulture = new("es-DO");

    public byte[] Generate(ReceiptPdfData data)
    {
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
                    col.Item().Text("RECIBO DE PAGO DE SERVICIOS DE ENFERMERÍA")
                        .SemiBold().FontSize(13).AlignCenter();
                    col.Item().Text($"Recibo No: {data.ReceiptNumber}")
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

                        table.Cell().Text("Número de factura:").SemiBold();
                        table.Cell().Text(data.InvoiceNumber);

                        table.Cell().Text("Fecha de factura:").SemiBold();
                        table.Cell().Text(data.InvoicedAtUtc.ToString("dd/MM/yyyy", DominicanCulture));

                        table.Cell().Text("Referencia bancaria:").SemiBold();
                        table.Cell().Text(data.BankReference);

                        table.Cell().Text("Fecha de pago:").SemiBold();
                        table.Cell().Text(data.PaidAtUtc.ToString("dd/MM/yyyy", DominicanCulture));
                    });

                    col.Item().PaddingTop(15).LineHorizontal(1);
                    // Use table for total row to keep right-aligned amount safe from edge clipping.
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });
                        table.Cell().Text("TOTAL PAGADO:").SemiBold().FontSize(13);
                        table.Cell().Text(data.Total.ToString("C2", DominicanCulture)).SemiBold().FontSize(13).AlignRight();
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Documento generado automáticamente el ").FontSize(8);
                    text.Span(data.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm", DominicanCulture)).FontSize(8);
                });
            });
        }).GeneratePdf();
    }
}
