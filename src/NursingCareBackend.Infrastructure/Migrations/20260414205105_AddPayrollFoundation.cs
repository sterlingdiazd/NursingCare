using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompensationAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompensationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EmploymentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CareRequestCategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UnitTypeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    NurseCategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BaseCompensationPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    FixedAmountPerUnit = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TransportIncentivePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ComplexityBonusPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MedicalSuppliesPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    PartialServicePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ExpressServicePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    SuspendedServicePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeductionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeductionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceExecutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BaseCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TransportIncentive = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ComplexityBonus = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MedicalSuppliesCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AdjustmentsTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DeductionsTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NetCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompensationRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmploymentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Variant = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CareRequestType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<int>(type: "int", nullable: false),
                    PricingCategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DistanceFactorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ComplexityLevelCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CareRequestTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ClientBasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CategoryFactorSnapshot = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    DistanceMultiplierSnapshot = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ComplexityMultiplierSnapshot = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    VolumeDiscountPercentSnapshot = table.Column<int>(type: "int", nullable: false),
                    SubtotalBeforeSupplies = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MedicalSuppliesCost = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RuleBaseCompensationPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    RuleFixedAmountPerUnit = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    RuleTransportIncentivePercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    RuleComplexityBonusPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    RuleMedicalSuppliesPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    RuleVariantPercent = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    BaseCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TransportIncentive = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ComplexityBonus = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MedicalSuppliesCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AdjustmentsTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DeductionsTotal = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    GrossCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NetCompensation = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ManualOverrideAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousNurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NewNurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CareRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NurseUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScheduledStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_ServiceExecutionId",
                table: "PayrollLines",
                column: "ServiceExecutionId",
                unique: true,
                filter: "[ServiceExecutionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_StartDate_EndDate",
                table: "PayrollPeriods",
                columns: new[] { "StartDate", "EndDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceExecutions_CareRequestId",
                table: "ServiceExecutions",
                column: "CareRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompensationAdjustments");

            migrationBuilder.DropTable(
                name: "CompensationRules");

            migrationBuilder.DropTable(
                name: "DeductionRecords");

            migrationBuilder.DropTable(
                name: "PayrollLines");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");

            migrationBuilder.DropTable(
                name: "ServiceExecutions");

            migrationBuilder.DropTable(
                name: "ShiftChanges");

            migrationBuilder.DropTable(
                name: "ShiftRecords");
        }
    }
}
