using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollOverrideAndRecalculationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollLineOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OverrideAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLineOverrides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollRecalculationAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinesAffected = table.Column<int>(type: "int", nullable: false),
                    TotalOldNet = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalNewNet = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollRecalculationAudits", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollLineOverrides");

            migrationBuilder.DropTable(
                name: "PayrollRecalculationAudits");
        }
    }
}
