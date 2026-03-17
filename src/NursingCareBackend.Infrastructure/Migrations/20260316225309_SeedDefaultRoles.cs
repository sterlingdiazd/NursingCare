using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert default roles
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { Guid.Parse("550e8400-e29b-41d4-a716-446655440001"), "Admin" },
                    { Guid.Parse("550e8400-e29b-41d4-a716-446655440002"), "Nurse" },
                    { Guid.Parse("550e8400-e29b-41d4-a716-446655440003"), "User" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete roles in reverse order
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: Guid.Parse("550e8400-e29b-41d4-a716-446655440001"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: Guid.Parse("550e8400-e29b-41d4-a716-446655440002"));

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: Guid.Parse("550e8400-e29b-41d4-a716-446655440003"));
        }
    }
}
