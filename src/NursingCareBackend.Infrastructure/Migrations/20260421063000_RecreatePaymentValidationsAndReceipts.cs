using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NursingCareBackend.Infrastructure.Persistence;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations;

/// <summary>
/// Recreates the PaymentValidations and Receipts tables that were accidentally dropped
/// by the SyncBillingModelChanges migration (20260420022721). The admin care request
/// detail endpoint queries these tables and returns 500 when they are missing.
/// </summary>
[DbContext(typeof(NursingCareDbContext))]
[Migration("20260421063000_RecreatePaymentValidationsAndReceipts")]
public partial class RecreatePaymentValidationsAndReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: only create if table does not already exist
        migrationBuilder.Sql(@"
IF OBJECT_ID('PaymentValidations', 'U') IS NULL
BEGIN
    CREATE TABLE [PaymentValidations] (
        [Id]                uniqueidentifier NOT NULL,
        [BankReference]     nvarchar(100)    NOT NULL,
        [CareRequestId]     uniqueidentifier NOT NULL,
        [CreatedAtUtc]      datetime2        NOT NULL,
        [InvoiceReference]  nvarchar(50)     NOT NULL,
        [SystemTotal]       decimal(10,2)    NOT NULL,
        [ValidatedAtUtc]    datetime2        NOT NULL,
        [ValidatedByUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_PaymentValidations] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_PaymentValidations_CareRequestId]
        ON [PaymentValidations] ([CareRequestId]);
END
");

        migrationBuilder.Sql(@"
IF OBJECT_ID('Receipts', 'U') IS NULL
BEGIN
    CREATE TABLE [Receipts] (
        [Id]                uniqueidentifier NOT NULL,
        [CareRequestId]     uniqueidentifier NOT NULL,
        [GeneratedAtUtc]    datetime2        NOT NULL,
        [GeneratedByUserId] uniqueidentifier NOT NULL,
        [ReceiptContent]    varbinary(max)   NOT NULL,
        [ReceiptNumber]     nvarchar(30)     NOT NULL,
        CONSTRAINT [PK_Receipts] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_Receipts_CareRequestId]
        ON [Receipts] ([CareRequestId]);

    CREATE UNIQUE INDEX [IX_Receipts_ReceiptNumber]
        ON [Receipts] ([ReceiptNumber]);
END
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF OBJECT_ID('PaymentValidations', 'U') IS NOT NULL DROP TABLE [PaymentValidations];");
        migrationBuilder.Sql("IF OBJECT_ID('Receipts', 'U') IS NOT NULL DROP TABLE [Receipts];");
    }
}
