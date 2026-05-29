using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientProfileContactFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "Users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "Users",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredAddress",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmergencyContactName_TextOnly",
                table: "Users",
                sql: "[EmergencyContactName] IS NULL OR (LEN(LTRIM(RTRIM([EmergencyContactName]))) > 0 AND [EmergencyContactName] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_EmergencyContactPhone_ExactDigits",
                table: "Users",
                sql: "[EmergencyContactPhone] IS NULL OR (LEN([EmergencyContactPhone]) = 10 AND [EmergencyContactPhone] NOT LIKE '%[^0-9]%')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmergencyContactName_TextOnly",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_EmergencyContactPhone_ExactDigits",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredAddress",
                table: "Users");
        }
    }
}
