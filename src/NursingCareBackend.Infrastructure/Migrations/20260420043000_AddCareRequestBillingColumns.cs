using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NursingCareBackend.Infrastructure.Persistence;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations;

/// <summary>
/// The CareRequest entity and DbContext mapping include billing lifecycle columns
/// (Invoice/Paid/Void fields), but they were missing from the migrations chain.
/// This migration brings the database schema in sync so app startup and tests
/// can seed care requests without "Invalid column name" errors.
/// </summary>
[DbContext(typeof(NursingCareDbContext))]
[Migration("20260420043000_AddCareRequestBillingColumns")]
public partial class AddCareRequestBillingColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // This migration is intentionally idempotent because local/dev automation databases
        // may already have these columns from prior schema sync runs.
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'InvoiceNumber') IS NULL
  ALTER TABLE [CareRequests] ADD [InvoiceNumber] nvarchar(50) NULL;
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'InvoicedAtUtc') IS NULL
  ALTER TABLE [CareRequests] ADD [InvoicedAtUtc] datetime2 NULL;
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'PaidAtUtc') IS NULL
  ALTER TABLE [CareRequests] ADD [PaidAtUtc] datetime2 NULL;
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'VoidedAtUtc') IS NULL
  ALTER TABLE [CareRequests] ADD [VoidedAtUtc] datetime2 NULL;
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'VoidReason') IS NULL
  ALTER TABLE [CareRequests] ADD [VoidReason] nvarchar(500) NULL;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'InvoiceNumber') IS NOT NULL
  ALTER TABLE [CareRequests] DROP COLUMN [InvoiceNumber];
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'InvoicedAtUtc') IS NOT NULL
  ALTER TABLE [CareRequests] DROP COLUMN [InvoicedAtUtc];
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'PaidAtUtc') IS NOT NULL
  ALTER TABLE [CareRequests] DROP COLUMN [PaidAtUtc];
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'VoidedAtUtc') IS NOT NULL
  ALTER TABLE [CareRequests] DROP COLUMN [VoidedAtUtc];
");

        migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.CareRequests', 'VoidReason') IS NOT NULL
  ALTER TABLE [CareRequests] DROP COLUMN [VoidReason];
");
    }
}
