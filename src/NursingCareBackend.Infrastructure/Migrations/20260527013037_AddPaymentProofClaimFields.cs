using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProofClaimFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ClaimedAmount",
                table: "PaymentProofs",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBankReference",
                table: "PaymentProofs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClaimedPaymentDate",
                table: "PaymentProofs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayingBank",
                table: "PaymentProofs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentValidations_BankReference",
                table: "PaymentValidations",
                column: "BankReference");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_ClaimedBankReference",
                table: "PaymentProofs",
                column: "ClaimedBankReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentValidations_BankReference",
                table: "PaymentValidations");

            migrationBuilder.DropIndex(
                name: "IX_PaymentProofs_ClaimedBankReference",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "ClaimedAmount",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "ClaimedBankReference",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "ClaimedPaymentDate",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "PayingBank",
                table: "PaymentProofs");
        }
    }
}
