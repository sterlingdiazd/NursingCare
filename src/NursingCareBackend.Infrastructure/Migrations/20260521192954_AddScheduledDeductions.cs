using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstallmentSequence",
                table: "DeductionRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduledDeductionId",
                table: "DeductionRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ScheduledDeductions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeductionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Modality = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Cadence = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartPeriodDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PrincipalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    InterestRatePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    TotalRepayable = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    InstallmentAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalInstallments = table.Column<int>(type: "int", nullable: false),
                    RecurringAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "int", nullable: true),
                    InstallmentsGenerated = table.Column<int>(type: "int", nullable: false),
                    InstallmentsPaid = table.Column<int>(type: "int", nullable: false),
                    AmountSettled = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledDeductions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeductionRecords_ScheduledDeductionId_PayrollPeriodId",
                table: "DeductionRecords",
                columns: new[] { "ScheduledDeductionId", "PayrollPeriodId" },
                unique: true,
                filter: "[ScheduledDeductionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledDeductions_NurseUserId_Status",
                table: "ScheduledDeductions",
                columns: new[] { "NurseUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledDeductions");

            migrationBuilder.DropIndex(
                name: "IX_DeductionRecords_ScheduledDeductionId_PayrollPeriodId",
                table: "DeductionRecords");

            migrationBuilder.DropColumn(
                name: "InstallmentSequence",
                table: "DeductionRecords");

            migrationBuilder.DropColumn(
                name: "ScheduledDeductionId",
                table: "DeductionRecords");
        }
    }
}
