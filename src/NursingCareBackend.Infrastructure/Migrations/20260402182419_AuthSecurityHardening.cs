using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuthSecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetPasswordCode",
                table: "Users");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttemptCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedLoginWindowStartedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedOutUntilUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResetPasswordCodeHash",
                table: "Users",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetPasswordCodeIssuedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResetPasswordFailedAttemptCount",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetPasswordResendAvailableAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginAttemptCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLoginWindowStartedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockedOutUntilUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetPasswordCodeHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetPasswordCodeIssuedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetPasswordFailedAttemptCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetPasswordResendAvailableAtUtc",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "ResetPasswordCode",
                table: "Users",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }
    }
}
