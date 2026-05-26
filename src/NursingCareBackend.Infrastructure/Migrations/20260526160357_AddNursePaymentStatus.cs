using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNursePaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing NursePeriodPayment rows represent real prior confirmations, so backfill them
            // to "Confirmed". New rows are set explicitly by the domain (Create -> Confirmed).
            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "NursePeriodPayments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Confirmed");

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatusReason",
                table: "NursePeriodPayments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StatusChangedAtUtc",
                table: "NursePeriodPayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StatusChangedByUserId",
                table: "NursePeriodPayments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "NursePeriodPayments");

            migrationBuilder.DropColumn(
                name: "PaymentStatusReason",
                table: "NursePeriodPayments");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                table: "NursePeriodPayments");

            migrationBuilder.DropColumn(
                name: "StatusChangedByUserId",
                table: "NursePeriodPayments");
        }
    }
}
