using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentProofOcrAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OcrAssessedAtUtc",
                table: "PaymentProofs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OcrClientEdited",
                table: "PaymentProofs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OcrConfidence",
                table: "PaymentProofs",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrDraftSentence",
                table: "PaymentProofs",
                type: "nvarchar(700)",
                maxLength: 700,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OcrExtractedAmount",
                table: "PaymentProofs",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrExtractedBank",
                table: "PaymentProofs",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrExtractedBankReference",
                table: "PaymentProofs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OcrExtractedPaymentDate",
                table: "PaymentProofs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProvider",
                table: "PaymentProofs",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrWarningsJson",
                table: "PaymentProofs",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OcrAssessedAtUtc",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrClientEdited",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrConfidence",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrDraftSentence",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrExtractedAmount",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrExtractedBank",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrExtractedBankReference",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrExtractedPaymentDate",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrProvider",
                table: "PaymentProofs");

            migrationBuilder.DropColumn(
                name: "OcrWarningsJson",
                table: "PaymentProofs");
        }
    }
}
