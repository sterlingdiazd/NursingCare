using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReminderStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDueReminderSentAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentOverdueReminderSentAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDueReminderSentAtUtc",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "PaymentOverdueReminderSentAtUtc",
                table: "CareRequests");
        }
    }
}
