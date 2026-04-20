using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncBillingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF OBJECT_ID('PaymentValidations', 'U') IS NOT NULL DROP TABLE [PaymentValidations];");
            migrationBuilder.Sql(
                "IF OBJECT_ID('Receipts', 'U') IS NOT NULL DROP TABLE [Receipts];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentValidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CareRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InvoiceReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SystemTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ValidatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValidatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
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
    }
}
