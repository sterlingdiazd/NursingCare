using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollPeriodReopenAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReopenCount",
                table: "PayrollPeriods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "PayrollPeriods",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAtUtc",
                table: "PayrollPeriods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReopenedByUserId",
                table: "PayrollPeriods",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReopenCount",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "ReopenReason",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "ReopenedAtUtc",
                table: "PayrollPeriods");

            migrationBuilder.DropColumn(
                name: "ReopenedByUserId",
                table: "PayrollPeriods");
        }
    }
}
