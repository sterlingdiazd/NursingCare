using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameApprovedAtUtcAndAddOverrideTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ApprovedAtUtc",
                table: "PayrollLineOverrides",
                newName: "ResolvedAtUtc");

            migrationBuilder.AddColumn<bool>(
                name: "IsOverridden",
                table: "PayrollLines",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalNetCompensation",
                table: "PayrollLines",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOverridden",
                table: "PayrollLines");

            migrationBuilder.DropColumn(
                name: "OriginalNetCompensation",
                table: "PayrollLines");

            migrationBuilder.RenameColumn(
                name: "ResolvedAtUtc",
                table: "PayrollLineOverrides",
                newName: "ApprovedAtUtc");
        }
    }
}
