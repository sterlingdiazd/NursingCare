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
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("RECIBO DE PAGO DE SERVICIOS DE ENFERMERIA")
                        .SemiBold().FontSize(14).AlignCenter();
                    col.Item().Text($"Recibo No: {data.ReceiptNumber}")
                        .FontSize(12).AlignCenter();
                    col.Item().Text($"Fecha: {data.GeneratedAtUtc:dd/MM/yyyy HH:mm} UTC")
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

                        AddRow("Solicitud ID:", data.CareRequestId.ToString());
                        if (!string.IsNullOrWhiteSpace(data.ClientDisplayName))
                            AddRow("Cliente:", data.ClientDisplayName);
                        if (!string.IsNullOrWhiteSpace(data.ClientIdentificationNumber))
                            AddRow("Cedula:", data.ClientIdentificationNumber);
                        AddRow("Tipo de servicio:", data.CareRequestType);
                        AddRow("Unidades:", $"{data.Unit} {data.UnitType}");
                    });

                    col.Item().PaddingTop(15).Text("DATOS DE FACTURACION").SemiBold().FontSize(12);
                    col.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        table.Cell().Text("Numero de factura:").SemiBold();
                        table.Cell().Text(data.InvoiceNumber);

                        table.Cell().Text("Fecha de factura:").SemiBold();
                        table.Cell().Text(data.InvoicedAtUtc.ToString("dd/MM/yyyy", DominicanCulture));

                        table.Cell().Text("Referencia bancaria:").SemiBold();
                        table.Cell().Text(data.BankReference);

                        table.Cell().Text("Fecha de pago:").SemiBold();
                        table.Cell().Text(data.PaidAtUtc.ToString("dd/MM/yyyy", DominicanCulture));
                    });

                    col.Item().PaddingTop(15).LineHorizontal(1);
                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL PAGADO:").SemiBold().FontSize(13);
                        row.RelativeItem().Text(data.Total.ToString("C2", DominicanCulture)).SemiBold().FontSize(13).AlignRight();
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Documento generado automaticamente el ").FontSize(8);
                    text.Span(data.GeneratedAtUtc.ToString("dd/MM/yyyy HH:mm", DominicanCulture)).FontSize(8);
                });
            });
        }).GeneratePdf();
    }
}
