using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    public partial class AddProfileTypeAndProfileTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileType",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Client");

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Clients_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Nurses",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Specialty = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LicenseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nurses", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Nurses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                UPDATE [Users]
                SET [ProfileType] = 'Nurse'
                WHERE EXISTS (
                    SELECT 1
                    FROM [UserRoles] ur
                    INNER JOIN [Roles] r ON r.[Id] = ur.[RoleId]
                    WHERE ur.[UserId] = [Users].[Id]
                      AND r.[Name] = 'Nurse'
                );
                """);

            migrationBuilder.Sql("""
                INSERT INTO [Nurses] ([UserId])
                SELECT [Id]
                FROM [Users] u
                WHERE u.[ProfileType] = 'Nurse'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Nurses] n
                      WHERE n.[UserId] = u.[Id]
                  );
                """);

            migrationBuilder.Sql("""
                INSERT INTO [Clients] ([UserId])
                SELECT [Id]
                FROM [Users] u
                WHERE u.[ProfileType] = 'Client'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [Clients] c
                      WHERE c.[UserId] = u.[Id]
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Nurses");

            migrationBuilder.DropColumn(
                name: "ProfileType",
                table: "Users");
        }
    }
}
