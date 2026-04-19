using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingFieldsToCareRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankReference",
                table: "CareRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "CareRequests",
                type: "nvarchar(max)",
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

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceiptGeneratedAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "CareRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "CareRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankReference",
                table: "CareRequests");

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
                name: "ReceiptGeneratedAtUtc",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
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
