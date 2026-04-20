using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceLifecycleAndPaymentEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "CareRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoicedAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "CareRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentValidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SystemTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ValidatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentValidations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReceiptContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentValidations_CareRequestId",
                table: "PaymentValidations",
                column: "CareRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CareRequestId",
                table: "Receipts",
                column: "CareRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ReceiptNumber",
                table: "Receipts",
                column: "ReceiptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentValidations");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "InvoicedAtUtc",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "VoidedAtUtc",
                table: "CareRequests");
        }
    }
}
