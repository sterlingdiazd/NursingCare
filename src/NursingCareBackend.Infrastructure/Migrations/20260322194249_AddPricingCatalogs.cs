using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NursingCareBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CategoryFactorSnapshot",
                table: "CareRequests",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ComplexityMultiplierSnapshot",
                table: "CareRequests",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceFactorMultiplierSnapshot",
                table: "CareRequests",
                type: "decimal(10,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingCategoryCode",
                table: "CareRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VolumeDiscountPercentSnapshot",
                table: "CareRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CareRequestCategoryCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryFactor = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareRequestCategoryCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CareRequestTypeCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CareRequestCategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UnitTypeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareRequestTypeCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplexityLevelCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplexityLevelCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DistanceFactorCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistanceFactorCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NurseCategoryCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AlternativeCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NurseCategoryCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NurseSpecialtyCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AlternativeCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NurseSpecialtyCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTypeCatalogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTypeCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VolumeDiscountRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinimumCount = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeDiscountRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CareRequestCategoryCatalogs_Code",
                table: "CareRequestCategoryCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CareRequestTypeCatalogs_Code",
                table: "CareRequestTypeCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplexityLevelCatalogs_Code",
                table: "ComplexityLevelCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DistanceFactorCatalogs_Code",
                table: "DistanceFactorCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NurseCategoryCatalogs_Code",
                table: "NurseCategoryCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NurseSpecialtyCatalogs_Code",
                table: "NurseSpecialtyCatalogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitTypeCatalogs_Code",
                table: "UnitTypeCatalogs",
                column: "Code",
                unique: true);

            SeedCatalogDefaults(migrationBuilder);
            BackfillCareRequestSnapshots(migrationBuilder);
        }

        private static void SeedCatalogDefaults(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CareRequestCategoryCatalogs",
                columns: new[] { "Id", "Code", "DisplayName", "CategoryFactor", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "hogar", "Hogar", 1.0m, true, 1 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "domicilio", "Domicilio", 1.2m, true, 2 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "medicos", "Medicos", 1.5m, true, 3 },
                });

            migrationBuilder.InsertData(
                table: "UnitTypeCatalogs",
                columns: new[] { "Id", "Code", "DisplayName", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "dia_completo", "Dia completo", true, 1 },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "mes", "Mes", true, 2 },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "medio_dia", "Medio dia", true, 3 },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "sesion", "Sesion", true, 4 },
                });

            migrationBuilder.InsertData(
                table: "CareRequestTypeCatalogs",
                columns: new[]
                {
                    "Id", "Code", "DisplayName", "CareRequestCategoryCode", "UnitTypeCode", "BasePrice", "IsActive", "DisplayOrder",
                },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), "hogar_diario", "Hogar diario", "hogar", "dia_completo", 2500m, true, 1 },
                    { new Guid("30000000-0000-0000-0000-000000000002"), "hogar_basico", "Hogar basico", "hogar", "mes", 55000m, true, 2 },
                    { new Guid("30000000-0000-0000-0000-000000000003"), "hogar_estandar", "Hogar estandar", "hogar", "mes", 60000m, true, 3 },
                    { new Guid("30000000-0000-0000-0000-000000000004"), "hogar_premium", "Hogar premium", "hogar", "mes", 65000m, true, 4 },
                    { new Guid("30000000-0000-0000-0000-000000000005"), "domicilio_dia_12h", "Domicilio dia 12h", "domicilio", "medio_dia", 2500m, true, 5 },
                    { new Guid("30000000-0000-0000-0000-000000000006"), "domicilio_noche_12h", "Domicilio noche 12h", "domicilio", "medio_dia", 2500m, true, 6 },
                    { new Guid("30000000-0000-0000-0000-000000000007"), "domicilio_24h", "Domicilio 24h", "domicilio", "dia_completo", 3500m, true, 7 },
                    { new Guid("30000000-0000-0000-0000-000000000008"), "suero", "Suero", "medicos", "sesion", 2000m, true, 8 },
                    { new Guid("30000000-0000-0000-0000-000000000009"), "medicamentos", "Medicamentos", "medicos", "sesion", 2000m, true, 9 },
                    { new Guid("30000000-0000-0000-0000-000000000010"), "sonda_vesical", "Sonda vesical", "medicos", "sesion", 2000m, true, 10 },
                    { new Guid("30000000-0000-0000-0000-000000000011"), "sonda_nasogastrica", "Sonda nasogastrica", "medicos", "sesion", 3000m, true, 11 },
                    { new Guid("30000000-0000-0000-0000-000000000012"), "sonda_peg", "Sonda PEG", "medicos", "sesion", 4000m, true, 12 },
                    { new Guid("30000000-0000-0000-0000-000000000013"), "curas", "Curas", "medicos", "sesion", 2000m, true, 13 },
                });

            migrationBuilder.InsertData(
                table: "DistanceFactorCatalogs",
                columns: new[] { "Id", "Code", "DisplayName", "Multiplier", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), "local", "Local", 1.0m, true, 1 },
                    { new Guid("40000000-0000-0000-0000-000000000002"), "cercana", "Cercana", 1.1m, true, 2 },
                    { new Guid("40000000-0000-0000-0000-000000000003"), "media", "Media", 1.2m, true, 3 },
                    { new Guid("40000000-0000-0000-0000-000000000004"), "lejana", "Lejana", 1.3m, true, 4 },
                });

            migrationBuilder.InsertData(
                table: "ComplexityLevelCatalogs",
                columns: new[] { "Id", "Code", "DisplayName", "Multiplier", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), "estandar", "Estandar", 1.0m, true, 1 },
                    { new Guid("50000000-0000-0000-0000-000000000002"), "moderada", "Moderada", 1.1m, true, 2 },
                    { new Guid("50000000-0000-0000-0000-000000000003"), "alta", "Alta", 1.2m, true, 3 },
                    { new Guid("50000000-0000-0000-0000-000000000004"), "critica", "Critica", 1.3m, true, 4 },
                });

            migrationBuilder.InsertData(
                table: "VolumeDiscountRules",
                columns: new[] { "Id", "MinimumCount", "DiscountPercent", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000001"), 1, 0, true, 1 },
                    { new Guid("60000000-0000-0000-0000-000000000002"), 5, 5, true, 2 },
                    { new Guid("60000000-0000-0000-0000-000000000003"), 10, 10, true, 3 },
                    { new Guid("60000000-0000-0000-0000-000000000004"), 20, 15, true, 4 },
                    { new Guid("60000000-0000-0000-0000-000000000005"), 50, 20, true, 5 },
                });

            migrationBuilder.InsertData(
                table: "NurseSpecialtyCatalogs",
                columns: new[] { "Id", "Code", "DisplayName", "AlternativeCodes", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("70000000-0000-0000-0000-000000000001"), "Cuidado de adultos", "Cuidado de adultos", "Adult Care", true, 1 },
                    { new Guid("70000000-0000-0000-0000-000000000002"), "Cuidado pediatrico", "Cuidado pediatrico", "Pediatric Care", true, 2 },
                    { new Guid("70000000-0000-0000-0000-000000000003"), "Cuidado geriatrico", "Cuidado geriatrico", "Geriatric Care", true, 3 },
                    { new Guid("70000000-0000-0000-0000-000000000004"), "Cuidados intensivos", "Cuidados intensivos", "Critical Care", true, 4 },
                    { new Guid("70000000-0000-0000-0000-000000000005"), "Atencion domiciliaria", "Atencion domiciliaria", "Home Care", true, 5 },
                });

            migrationBuilder.InsertData(
                table: "NurseCategoryCatalogs",
                columns: new[] { "Id", "Code", "DisplayName", "AlternativeCodes", "IsActive", "DisplayOrder" },
                values: new object[,]
                {
                    { new Guid("80000000-0000-0000-0000-000000000001"), "Junior", "Junior", null, true, 1 },
                    { new Guid("80000000-0000-0000-0000-000000000002"), "Semisenior", "Semisenior", "Semi Senior", true, 2 },
                    { new Guid("80000000-0000-0000-0000-000000000003"), "Senior", "Senior", null, true, 3 },
                    { new Guid("80000000-0000-0000-0000-000000000004"), "Lider", "Lider", "Lead", true, 4 },
                });
        }

        private static void BackfillCareRequestSnapshots(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE cr
                SET
                  PricingCategoryCode = rtc.CareRequestCategoryCode,
                  CategoryFactorSnapshot = cat.CategoryFactor,
                  DistanceFactorMultiplierSnapshot =
                    CASE
                      WHEN rtc.CareRequestCategoryCode = N'domicilio' THEN ISNULL(dfc.Multiplier, 1.0)
                      ELSE 1.0
                    END,
                  ComplexityMultiplierSnapshot =
                    CASE
                      WHEN rtc.CareRequestCategoryCode IN (N'hogar', N'domicilio') THEN ISNULL(clc.Multiplier, 1.0)
                      ELSE 1.0
                    END,
                  VolumeDiscountPercentSnapshot =
                    CASE
                      WHEN cr.[Unit] <= 0 THEN 0
                      WHEN (
                        cr.[Price] * cat.CategoryFactor *
                        CASE WHEN rtc.CareRequestCategoryCode = N'domicilio' THEN ISNULL(dfc.Multiplier, 1.0) ELSE 1.0 END *
                        CASE WHEN rtc.CareRequestCategoryCode IN (N'hogar', N'domicilio') THEN ISNULL(clc.Multiplier, 1.0) ELSE 1.0 END *
                        cr.[Unit]
                      ) = 0 THEN 0
                      ELSE CAST(ROUND(100.0 * (
                        1.0 - (
                          (cr.[Total] - ISNULL(cr.[MedicalSuppliesCost], 0)) /
                          NULLIF(
                            cr.[Price] * cat.CategoryFactor *
                            CASE WHEN rtc.CareRequestCategoryCode = N'domicilio' THEN ISNULL(dfc.Multiplier, 1.0) ELSE 1.0 END *
                            CASE WHEN rtc.CareRequestCategoryCode IN (N'hogar', N'domicilio') THEN ISNULL(clc.Multiplier, 1.0) ELSE 1.0 END *
                            cr.[Unit],
                            0)
                        )
                      ), 0) AS int)
                    END
                FROM [CareRequests] AS cr
                INNER JOIN [CareRequestTypeCatalogs] AS rtc ON rtc.[Code] = cr.[CareRequestType]
                INNER JOIN [CareRequestCategoryCatalogs] AS cat ON cat.[Code] = rtc.[CareRequestCategoryCode]
                LEFT JOIN [DistanceFactorCatalogs] AS dfc ON dfc.[Code] = cr.[DistanceFactor]
                LEFT JOIN [ComplexityLevelCatalogs] AS clc ON clc.[Code] = cr.[ComplexityLevel];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CareRequestCategoryCatalogs");

            migrationBuilder.DropTable(
                name: "CareRequestTypeCatalogs");

            migrationBuilder.DropTable(
                name: "ComplexityLevelCatalogs");

            migrationBuilder.DropTable(
                name: "DistanceFactorCatalogs");

            migrationBuilder.DropTable(
                name: "NurseCategoryCatalogs");

            migrationBuilder.DropTable(
                name: "NurseSpecialtyCatalogs");

            migrationBuilder.DropTable(
                name: "UnitTypeCatalogs");

            migrationBuilder.DropTable(
                name: "VolumeDiscountRules");

            migrationBuilder.DropColumn(
                name: "CategoryFactorSnapshot",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "ComplexityMultiplierSnapshot",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "DistanceFactorMultiplierSnapshot",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "PricingCategoryCode",
                table: "CareRequests");

            migrationBuilder.DropColumn(
                name: "VolumeDiscountPercentSnapshot",
                table: "CareRequests");
        }
    }
}
