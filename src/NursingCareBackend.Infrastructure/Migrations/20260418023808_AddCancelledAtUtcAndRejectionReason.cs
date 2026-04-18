using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCancelledAtUtcAndRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "CareRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "CareRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "CareRequests");
        }
    }
}
