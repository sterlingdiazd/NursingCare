using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameColumnsToMatchEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ServiceType",
                table: "CareRequests",
                newName: "CareRequestType");

            migrationBuilder.RenameColumn(
                name: "ServiceReason",
                table: "CareRequests",
                newName: "CareRequestReason");

            migrationBuilder.RenameColumn(
                name: "ServiceDate",
                table: "CareRequests",
                newName: "CareRequestDate");

            migrationBuilder.RenameColumn(
                name: "ResidentId",
                table: "CareRequests",
                newName: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserID",
                table: "CareRequests",
                newName: "ResidentId");

            migrationBuilder.RenameColumn(
                name: "CareRequestType",
                table: "CareRequests",
                newName: "ServiceType");

            migrationBuilder.RenameColumn(
                name: "CareRequestReason",
                table: "CareRequests",
                newName: "ServiceReason");

            migrationBuilder.RenameColumn(
                name: "CareRequestDate",
                table: "CareRequests",
                newName: "ServiceDate");
        }
    }
}
