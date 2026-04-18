using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceBreakdownSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LineBeforeVolumeDiscount",
                table: "CareRequests",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubtotalBeforeSupplies",
                table: "CareRequests",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceAfterVolumeDiscount",
                table: "CareRequests",
                type: "decimal(10,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineBeforeVolumeDiscount",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "SubtotalBeforeSupplies",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "UnitPriceAfterVolumeDiscount",
                table: "CareRequests");
        }
    }
}
