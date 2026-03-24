IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE TABLE [CareRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ResidentId] uniqueidentifier NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [ServiceReason] nvarchar(max) NULL,
        [ServiceType] nvarchar(max) NOT NULL,
        [UnitType] nvarchar(max) NOT NULL,
        [Unit] int NOT NULL,
        [Price] decimal(10,2) NOT NULL,
        [Total] decimal(10,2) NOT NULL,
        [DistanceFactor] nvarchar(max) NULL,
        [ComplexityLevel] nvarchar(max) NULL,
        [ClientBasePrice] decimal(10,2) NULL,
        [MedicalSuppliesCost] decimal(10,2) NULL,
        [ServiceDate] date NULL,
        [NurseId] uniqueidentifier NULL,
        [SuggestedNurse] nvarchar(max) NULL,
        [AssignedNurse] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [RejectedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_CareRequests] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(256) NULL,
        [GoogleSubjectId] nvarchar(256) NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(512) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_GoogleSubjectId] ON [Users] ([GoogleSubjectId]) WHERE [GoogleSubjectId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320011653_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320011653_InitialCreate', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320012916_RenameColumnsToMatchEntities'
)
BEGIN
    EXEC sp_rename N'[CareRequests].[ServiceType]', N'CareRequestType', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320012916_RenameColumnsToMatchEntities'
)
BEGIN
    EXEC sp_rename N'[CareRequests].[ServiceReason]', N'CareRequestReason', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320012916_RenameColumnsToMatchEntities'
)
BEGIN
    EXEC sp_rename N'[CareRequests].[ServiceDate]', N'CareRequestDate', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320012916_RenameColumnsToMatchEntities'
)
BEGIN
    EXEC sp_rename N'[CareRequests].[ResidentId]', N'UserID', 'COLUMN';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260320012916_RenameColumnsToMatchEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260320012916_RenameColumnsToMatchEntities', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321015055_AddUserRegistrationFields'
)
BEGIN
    ALTER TABLE [Users] ADD [IdentificationNumber] nvarchar(50) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321015055_AddUserRegistrationFields'
)
BEGIN
    ALTER TABLE [Users] ADD [LastName] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321015055_AddUserRegistrationFields'
)
BEGIN
    ALTER TABLE [Users] ADD [Name] nvarchar(150) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321015055_AddUserRegistrationFields'
)
BEGIN
    ALTER TABLE [Users] ADD [Phone] nvarchar(30) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321015055_AddUserRegistrationFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321015055_AddUserRegistrationFields', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    ALTER TABLE [Users] ADD [ProfileType] nvarchar(20) NOT NULL DEFAULT N'Client';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    CREATE TABLE [Clients] (
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_Clients_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    CREATE TABLE [Nurses] (
        [UserId] uniqueidentifier NOT NULL,
        [HireDate] date NULL,
        [Specialty] nvarchar(150) NULL,
        [LicenseId] nvarchar(100) NULL,
        [BankName] nvarchar(150) NULL,
        [AccountNumber] nvarchar(50) NULL,
        [Category] nvarchar(100) NULL,
        CONSTRAINT [PK_Nurses] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_Nurses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    UPDATE [Users]
    SET [ProfileType] = 'Nurse'
    WHERE EXISTS (
        SELECT 1
        FROM [UserRoles] ur
        INNER JOIN [Roles] r ON r.[Id] = ur.[RoleId]
        WHERE ur.[UserId] = [Users].[Id]
          AND r.[Name] = 'Nurse'
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    INSERT INTO [Nurses] ([UserId])
    SELECT [Id]
    FROM [Users] u
    WHERE u.[ProfileType] = 'Nurse'
      AND NOT EXISTS (
          SELECT 1
          FROM [Nurses] n
          WHERE n.[UserId] = u.[Id]
      );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    INSERT INTO [Clients] ([UserId])
    SELECT [Id]
    FROM [Users] u
    WHERE u.[ProfileType] = 'Client'
      AND NOT EXISTS (
          SELECT 1
          FROM [Clients] c
          WHERE c.[UserId] = u.[Id]
      );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321035200_AddProfileTypeAndProfileTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321035200_AddProfileTypeAndProfileTables', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321041412_AddNurseReviewState'
)
BEGIN
    ALTER TABLE [Nurses] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321041412_AddNurseReviewState'
)
BEGIN
    UPDATE n
    SET n.[IsActive] = u.[IsActive]
    FROM [Nurses] n
    INNER JOIN [Users] u ON u.[Id] = n.[UserId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321041412_AddNurseReviewState'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321041412_AddNurseReviewState', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321090233_ReplaceCareRequestNurseIdWithAssignedNurse'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CareRequests]') AND [c].[name] = N'NurseId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [CareRequests] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [CareRequests] DROP COLUMN [NurseId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321090233_ReplaceCareRequestNurseIdWithAssignedNurse'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CareRequests]') AND [c].[name] = N'AssignedNurse');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [CareRequests] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [CareRequests] ALTER COLUMN [AssignedNurse] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321090233_ReplaceCareRequestNurseIdWithAssignedNurse'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321090233_ReplaceCareRequestNurseIdWithAssignedNurse', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    UPDATE [Users]
    SET [IdentificationNumber] = NULLIF(
        REPLACE(REPLACE(REPLACE(REPLACE([IdentificationNumber], '-', ''), ' ', ''), '/', ''), '.', ''),
        '')
    WHERE [IdentificationNumber] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    UPDATE [Users]
    SET [Phone] = NULLIF(
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE([Phone], '-', ''), ' ', ''), '/', ''), '.', ''), '(', ''), ')', ''),
        '')
    WHERE [Phone] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    UPDATE [Nurses]
    SET [LicenseId] = NULLIF(
        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER([LicenseId]), 'LIC', ''), '-', ''), ' ', ''), '/', ''), '.', ''), '#', ''),
        '')
    WHERE [LicenseId] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    UPDATE [Nurses]
    SET [AccountNumber] = NULLIF(
        REPLACE(REPLACE(REPLACE(REPLACE([AccountNumber], '-', ''), ' ', ''), '/', ''), '.', ''),
        '')
    WHERE [AccountNumber] IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_IdentificationNumber_ExactDigits] CHECK ([IdentificationNumber] IS NULL OR (LEN([IdentificationNumber]) = 11 AND [IdentificationNumber] NOT LIKE ''%[^0-9]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_LastName_TextOnly] CHECK ([LastName] IS NULL OR (LEN(LTRIM(RTRIM([LastName]))) > 0 AND [LastName] NOT LIKE ''%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_Name_TextOnly] CHECK ([Name] IS NULL OR (LEN(LTRIM(RTRIM([Name]))) > 0 AND [Name] NOT LIKE ''%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_Phone_ExactDigits] CHECK ([Phone] IS NULL OR (LEN([Phone]) = 10 AND [Phone] NOT LIKE ''%[^0-9]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Nurses] ADD CONSTRAINT [CK_Nurses_AccountNumber_DigitsOnly] CHECK ([AccountNumber] IS NULL OR (LEN([AccountNumber]) > 0 AND [AccountNumber] NOT LIKE ''%[^0-9]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Nurses] ADD CONSTRAINT [CK_Nurses_BankName_TextOnly] CHECK ([BankName] IS NULL OR (LEN(LTRIM(RTRIM([BankName]))) > 0 AND [BankName] NOT LIKE ''%[^A-Za-zÁÉÍÓÚáéíóúÑñÜü ]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    EXEC(N'ALTER TABLE [Nurses] ADD CONSTRAINT [CK_Nurses_LicenseId_DigitsOnly] CHECK ([LicenseId] IS NULL OR (LEN([LicenseId]) > 0 AND [LicenseId] NOT LIKE ''%[^0-9]%''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260321221620_EnforceIdentityFieldValidation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260321221620_EnforceIdentityFieldValidation', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322153000_AddAdminAuditLogs'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NULL,
        [ActorRole] nvarchar(100) NOT NULL,
        [Action] nvarchar(150) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(150) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [MetadataJson] nvarchar(max) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322153000_AddAdminAuditLogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260322153000_AddAdminAuditLogs', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    ALTER TABLE [CareRequests] ADD [CategoryFactorSnapshot] decimal(10,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    ALTER TABLE [CareRequests] ADD [ComplexityMultiplierSnapshot] decimal(10,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    ALTER TABLE [CareRequests] ADD [DistanceFactorMultiplierSnapshot] decimal(10,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    ALTER TABLE [CareRequests] ADD [PricingCategoryCode] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    ALTER TABLE [CareRequests] ADD [VolumeDiscountPercentSnapshot] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [CareRequestCategoryCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [CategoryFactor] decimal(10,4) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_CareRequestCategoryCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [CareRequestTypeCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [CareRequestCategoryCode] nvarchar(64) NOT NULL,
        [UnitTypeCode] nvarchar(64) NOT NULL,
        [BasePrice] decimal(12,2) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_CareRequestTypeCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [ComplexityLevelCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Multiplier] decimal(10,4) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_ComplexityLevelCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [DistanceFactorCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Multiplier] decimal(10,4) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_DistanceFactorCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [NurseCategoryCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [AlternativeCodes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_NurseCategoryCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [NurseSpecialtyCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(150) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [AlternativeCodes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_NurseSpecialtyCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [UnitTypeCatalogs] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_UnitTypeCatalogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE TABLE [VolumeDiscountRules] (
        [Id] uniqueidentifier NOT NULL,
        [MinimumCount] int NOT NULL,
        [DiscountPercent] int NOT NULL,
        [IsActive] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        CONSTRAINT [PK_VolumeDiscountRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CareRequestCategoryCatalogs_Code] ON [CareRequestCategoryCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CareRequestTypeCatalogs_Code] ON [CareRequestTypeCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ComplexityLevelCatalogs_Code] ON [ComplexityLevelCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DistanceFactorCatalogs_Code] ON [DistanceFactorCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NurseCategoryCatalogs_Code] ON [NurseCategoryCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NurseSpecialtyCatalogs_Code] ON [NurseSpecialtyCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UnitTypeCatalogs_Code] ON [UnitTypeCatalogs] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'CategoryFactor', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[CareRequestCategoryCatalogs]'))
        SET IDENTITY_INSERT [CareRequestCategoryCatalogs] ON;
    EXEC(N'INSERT INTO [CareRequestCategoryCatalogs] ([Id], [Code], [DisplayName], [CategoryFactor], [IsActive], [DisplayOrder])
    VALUES (''10000000-0000-0000-0000-000000000001'', N''hogar'', N''Hogar'', 1.0, CAST(1 AS bit), 1),
    (''10000000-0000-0000-0000-000000000002'', N''domicilio'', N''Domicilio'', 1.2, CAST(1 AS bit), 2),
    (''10000000-0000-0000-0000-000000000003'', N''medicos'', N''Medicos'', 1.5, CAST(1 AS bit), 3)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'CategoryFactor', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[CareRequestCategoryCatalogs]'))
        SET IDENTITY_INSERT [CareRequestCategoryCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[UnitTypeCatalogs]'))
        SET IDENTITY_INSERT [UnitTypeCatalogs] ON;
    EXEC(N'INSERT INTO [UnitTypeCatalogs] ([Id], [Code], [DisplayName], [IsActive], [DisplayOrder])
    VALUES (''20000000-0000-0000-0000-000000000001'', N''dia_completo'', N''Dia completo'', CAST(1 AS bit), 1),
    (''20000000-0000-0000-0000-000000000002'', N''mes'', N''Mes'', CAST(1 AS bit), 2),
    (''20000000-0000-0000-0000-000000000003'', N''medio_dia'', N''Medio dia'', CAST(1 AS bit), 3),
    (''20000000-0000-0000-0000-000000000004'', N''sesion'', N''Sesion'', CAST(1 AS bit), 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[UnitTypeCatalogs]'))
        SET IDENTITY_INSERT [UnitTypeCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'CareRequestCategoryCode', N'UnitTypeCode', N'BasePrice', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[CareRequestTypeCatalogs]'))
        SET IDENTITY_INSERT [CareRequestTypeCatalogs] ON;
    EXEC(N'INSERT INTO [CareRequestTypeCatalogs] ([Id], [Code], [DisplayName], [CareRequestCategoryCode], [UnitTypeCode], [BasePrice], [IsActive], [DisplayOrder])
    VALUES (''30000000-0000-0000-0000-000000000001'', N''hogar_diario'', N''Hogar diario'', N''hogar'', N''dia_completo'', 2500.0, CAST(1 AS bit), 1),
    (''30000000-0000-0000-0000-000000000002'', N''hogar_basico'', N''Hogar basico'', N''hogar'', N''mes'', 55000.0, CAST(1 AS bit), 2),
    (''30000000-0000-0000-0000-000000000003'', N''hogar_estandar'', N''Hogar estandar'', N''hogar'', N''mes'', 60000.0, CAST(1 AS bit), 3),
    (''30000000-0000-0000-0000-000000000004'', N''hogar_premium'', N''Hogar premium'', N''hogar'', N''mes'', 65000.0, CAST(1 AS bit), 4),
    (''30000000-0000-0000-0000-000000000005'', N''domicilio_dia_12h'', N''Domicilio dia 12h'', N''domicilio'', N''medio_dia'', 2500.0, CAST(1 AS bit), 5),
    (''30000000-0000-0000-0000-000000000006'', N''domicilio_noche_12h'', N''Domicilio noche 12h'', N''domicilio'', N''medio_dia'', 2500.0, CAST(1 AS bit), 6),
    (''30000000-0000-0000-0000-000000000007'', N''domicilio_24h'', N''Domicilio 24h'', N''domicilio'', N''dia_completo'', 3500.0, CAST(1 AS bit), 7),
    (''30000000-0000-0000-0000-000000000008'', N''suero'', N''Suero'', N''medicos'', N''sesion'', 2000.0, CAST(1 AS bit), 8),
    (''30000000-0000-0000-0000-000000000009'', N''medicamentos'', N''Medicamentos'', N''medicos'', N''sesion'', 2000.0, CAST(1 AS bit), 9),
    (''30000000-0000-0000-0000-000000000010'', N''sonda_vesical'', N''Sonda vesical'', N''medicos'', N''sesion'', 2000.0, CAST(1 AS bit), 10),
    (''30000000-0000-0000-0000-000000000011'', N''sonda_nasogastrica'', N''Sonda nasogastrica'', N''medicos'', N''sesion'', 3000.0, CAST(1 AS bit), 11),
    (''30000000-0000-0000-0000-000000000012'', N''sonda_peg'', N''Sonda PEG'', N''medicos'', N''sesion'', 4000.0, CAST(1 AS bit), 12),
    (''30000000-0000-0000-0000-000000000013'', N''curas'', N''Curas'', N''medicos'', N''sesion'', 2000.0, CAST(1 AS bit), 13)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'CareRequestCategoryCode', N'UnitTypeCode', N'BasePrice', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[CareRequestTypeCatalogs]'))
        SET IDENTITY_INSERT [CareRequestTypeCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'Multiplier', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[DistanceFactorCatalogs]'))
        SET IDENTITY_INSERT [DistanceFactorCatalogs] ON;
    EXEC(N'INSERT INTO [DistanceFactorCatalogs] ([Id], [Code], [DisplayName], [Multiplier], [IsActive], [DisplayOrder])
    VALUES (''40000000-0000-0000-0000-000000000001'', N''local'', N''Local'', 1.0, CAST(1 AS bit), 1),
    (''40000000-0000-0000-0000-000000000002'', N''cercana'', N''Cercana'', 1.1, CAST(1 AS bit), 2),
    (''40000000-0000-0000-0000-000000000003'', N''media'', N''Media'', 1.2, CAST(1 AS bit), 3),
    (''40000000-0000-0000-0000-000000000004'', N''lejana'', N''Lejana'', 1.3, CAST(1 AS bit), 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'Multiplier', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[DistanceFactorCatalogs]'))
        SET IDENTITY_INSERT [DistanceFactorCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'Multiplier', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[ComplexityLevelCatalogs]'))
        SET IDENTITY_INSERT [ComplexityLevelCatalogs] ON;
    EXEC(N'INSERT INTO [ComplexityLevelCatalogs] ([Id], [Code], [DisplayName], [Multiplier], [IsActive], [DisplayOrder])
    VALUES (''50000000-0000-0000-0000-000000000001'', N''estandar'', N''Estandar'', 1.0, CAST(1 AS bit), 1),
    (''50000000-0000-0000-0000-000000000002'', N''moderada'', N''Moderada'', 1.1, CAST(1 AS bit), 2),
    (''50000000-0000-0000-0000-000000000003'', N''alta'', N''Alta'', 1.2, CAST(1 AS bit), 3),
    (''50000000-0000-0000-0000-000000000004'', N''critica'', N''Critica'', 1.3, CAST(1 AS bit), 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'Multiplier', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[ComplexityLevelCatalogs]'))
        SET IDENTITY_INSERT [ComplexityLevelCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'MinimumCount', N'DiscountPercent', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[VolumeDiscountRules]'))
        SET IDENTITY_INSERT [VolumeDiscountRules] ON;
    EXEC(N'INSERT INTO [VolumeDiscountRules] ([Id], [MinimumCount], [DiscountPercent], [IsActive], [DisplayOrder])
    VALUES (''60000000-0000-0000-0000-000000000001'', 1, 0, CAST(1 AS bit), 1),
    (''60000000-0000-0000-0000-000000000002'', 5, 5, CAST(1 AS bit), 2),
    (''60000000-0000-0000-0000-000000000003'', 10, 10, CAST(1 AS bit), 3),
    (''60000000-0000-0000-0000-000000000004'', 20, 15, CAST(1 AS bit), 4),
    (''60000000-0000-0000-0000-000000000005'', 50, 20, CAST(1 AS bit), 5)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'MinimumCount', N'DiscountPercent', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[VolumeDiscountRules]'))
        SET IDENTITY_INSERT [VolumeDiscountRules] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'AlternativeCodes', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[NurseSpecialtyCatalogs]'))
        SET IDENTITY_INSERT [NurseSpecialtyCatalogs] ON;
    EXEC(N'INSERT INTO [NurseSpecialtyCatalogs] ([Id], [Code], [DisplayName], [AlternativeCodes], [IsActive], [DisplayOrder])
    VALUES (''70000000-0000-0000-0000-000000000001'', N''Cuidado de adultos'', N''Cuidado de adultos'', N''Adult Care'', CAST(1 AS bit), 1),
    (''70000000-0000-0000-0000-000000000002'', N''Cuidado pediatrico'', N''Cuidado pediatrico'', N''Pediatric Care'', CAST(1 AS bit), 2),
    (''70000000-0000-0000-0000-000000000003'', N''Cuidado geriatrico'', N''Cuidado geriatrico'', N''Geriatric Care'', CAST(1 AS bit), 3),
    (''70000000-0000-0000-0000-000000000004'', N''Cuidados intensivos'', N''Cuidados intensivos'', N''Critical Care'', CAST(1 AS bit), 4),
    (''70000000-0000-0000-0000-000000000005'', N''Atencion domiciliaria'', N''Atencion domiciliaria'', N''Home Care'', CAST(1 AS bit), 5)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'AlternativeCodes', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[NurseSpecialtyCatalogs]'))
        SET IDENTITY_INSERT [NurseSpecialtyCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'AlternativeCodes', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[NurseCategoryCatalogs]'))
        SET IDENTITY_INSERT [NurseCategoryCatalogs] ON;
    EXEC(N'INSERT INTO [NurseCategoryCatalogs] ([Id], [Code], [DisplayName], [AlternativeCodes], [IsActive], [DisplayOrder])
    VALUES (''80000000-0000-0000-0000-000000000001'', N''Junior'', N''Junior'', NULL, CAST(1 AS bit), 1),
    (''80000000-0000-0000-0000-000000000002'', N''Semisenior'', N''Semisenior'', N''Semi Senior'', CAST(1 AS bit), 2),
    (''80000000-0000-0000-0000-000000000003'', N''Senior'', N''Senior'', NULL, CAST(1 AS bit), 3),
    (''80000000-0000-0000-0000-000000000004'', N''Lider'', N''Lider'', N''Lead'', CAST(1 AS bit), 4)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'DisplayName', N'AlternativeCodes', N'IsActive', N'DisplayOrder') AND [object_id] = OBJECT_ID(N'[NurseCategoryCatalogs]'))
        SET IDENTITY_INSERT [NurseCategoryCatalogs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260322194249_AddPricingCatalogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260322194249_AddPricingCatalogs', N'10.0.2');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323190754_AddAdminNotifications'
)
BEGIN
    CREATE TABLE [AdminNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientUserId] uniqueidentifier NOT NULL,
        [Category] nvarchar(80) NOT NULL,
        [Severity] nvarchar(20) NOT NULL,
        [Title] nvarchar(220) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [EntityType] nvarchar(80) NULL,
        [EntityId] nvarchar(120) NULL,
        [DeepLinkPath] nvarchar(600) NULL,
        [Source] nvarchar(180) NULL,
        [RequiresAction] bit NOT NULL,
        [IsDismissed] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [ReadAtUtc] datetime2 NULL,
        [ArchivedAtUtc] datetime2 NULL,
        [CreatedBySystem] bit NOT NULL,
        CONSTRAINT [PK_AdminNotifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323190754_AddAdminNotifications'
)
BEGIN
    CREATE INDEX [IX_AdminNotifications_RecipientUserId_ArchivedAtUtc_ReadAtUtc] ON [AdminNotifications] ([RecipientUserId], [ArchivedAtUtc], [ReadAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260323190754_AddAdminNotifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260323190754_AddAdminNotifications', N'10.0.2');
END;

COMMIT;
GO

