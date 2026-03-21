using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceIdentityFieldValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [Users]
                SET [IdentificationNumber] = NULLIF(
                    REPLACE(REPLACE(REPLACE(REPLACE([IdentificationNumber], '-', ''), ' ', ''), '/', ''), '.', ''),
                    '')
                WHERE [IdentificationNumber] IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [Users]
                SET [Phone] = NULLIF(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Phone], '-', ''), ' ', ''), '/', ''), '.', ''), '(', ''), ')', ''),
                    '')
                WHERE [Phone] IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [Nurses]
                SET [LicenseId] = NULLIF(
                    REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER([LicenseId]), 'LIC', ''), '-', ''), ' ', ''), '/', ''), '.', ''), '#', ''),
                    '')
                WHERE [LicenseId] IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [Nurses]
                SET [AccountNumber] = NULLIF(
                    REPLACE(REPLACE(REPLACE(REPLACE([AccountNumber], '-', ''), ' ', ''), '/', ''), '.', ''),
                    '')
                WHERE [AccountNumber] IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_IdentificationNumber_ExactDigits",
                table: "Users",
                sql: "[IdentificationNumber] IS NULL OR (LEN([IdentificationNumber]) = 11 AND [IdentificationNumber] NOT LIKE '%[^0-9]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_LastName_TextOnly",
                table: "Users",
                sql: "[LastName] IS NULL OR (LEN(LTRIM(RTRIM([LastName]))) > 0 AND [LastName] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Name_TextOnly",
                table: "Users",
                sql: "[Name] IS NULL OR (LEN(LTRIM(RTRIM([Name]))) > 0 AND [Name] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Phone_ExactDigits",
                table: "Users",
                sql: "[Phone] IS NULL OR (LEN([Phone]) = 10 AND [Phone] NOT LIKE '%[^0-9]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Nurses_AccountNumber_DigitsOnly",
                table: "Nurses",
                sql: "[AccountNumber] IS NULL OR (LEN([AccountNumber]) > 0 AND [AccountNumber] NOT LIKE '%[^0-9]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Nurses_BankName_TextOnly",
                table: "Nurses",
                sql: "[BankName] IS NULL OR (LEN(LTRIM(RTRIM([BankName]))) > 0 AND [BankName] NOT LIKE '%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Nurses_LicenseId_DigitsOnly",
                table: "Nurses",
                sql: "[LicenseId] IS NULL OR (LEN([LicenseId]) > 0 AND [LicenseId] NOT LIKE '%[^0-9]%')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_IdentificationNumber_ExactDigits",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_LastName_TextOnly",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Name_TextOnly",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Phone_ExactDigits",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Nurses_AccountNumber_DigitsOnly",
                table: "Nurses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Nurses_BankName_TextOnly",
                table: "Nurses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Nurses_LicenseId_DigitsOnly",
                table: "Nurses");
        }
    }
}
