using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNurseAccountTypeAndHolder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountHolderName",
                table: "Nurses",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "Nurses",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountHolderName",
                table: "Nurses");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "Nurses");
        }
    }
}
