using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(220)", maxLength: 220, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DeepLinkPath = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: true),
                    RequiresAction = table.Column<bool>(type: "bit", nullable: false),
                    IsDismissed = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBySystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminNotifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminNotifications_RecipientUserId_ArchivedAtUtc_ReadAtUtc",
                table: "AdminNotifications",
                columns: new[] { "RecipientUserId", "ArchivedAtUtc", "ReadAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminNotifications");
        }
    }
}
