-- =============================================
-- Stored Procedure: Insert History Log Entry
-- Description: Inserts a new history entry for tracking changes
-- =============================================

-- SeleniumDB Schema
CREATE OR ALTER PROCEDURE [SeleniumDB].[usp_InsertHistoryLog]
    @EntityType NVARCHAR(50),
    @EntityId NVARCHAR(50),
    @EntityName NVARCHAR(500),
    @OperationType NVARCHAR(20),
    @FieldName NVARCHAR(100) = NULL,
    @OldValue NVARCHAR(MAX) = NULL,
    @NewValue NVARCHAR(MAX) = NULL,
    @ChangedBy NVARCHAR(100) = 'System',
    @ChangeDescription NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [SeleniumDB].[HistoryLog]
    (
        [EntityType],
        [EntityId],
        [EntityName],
        [OperationType],
        [FieldName],
        [OldValue],
        [NewValue],
        [ChangedBy],
        [ChangeDescription]
    )
    VALUES
    (
        @EntityType,
        @EntityId,
        @EntityName,
        @OperationType,
        @FieldName,
        @OldValue,
        @NewValue,
        @ChangedBy,
        @ChangeDescription
    );

    SELECT SCOPE_IDENTITY() AS NewId;
END
GO

-- PRODUCTION_Selenium Schema
CREATE OR ALTER PROCEDURE [PRODUCTION_Selenium].[usp_InsertHistoryLog]
    @EntityType NVARCHAR(50),
    @EntityId NVARCHAR(50),
    @EntityName NVARCHAR(500),
    @OperationType NVARCHAR(20),
    @FieldName NVARCHAR(100) = NULL,
    @OldValue NVARCHAR(MAX) = NULL,
    @NewValue NVARCHAR(MAX) = NULL,
    @ChangedBy NVARCHAR(100) = 'System',
    @ChangeDescription NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [PRODUCTION_Selenium].[HistoryLog]
    (
        [EntityType],
        [EntityId],
        [EntityName],
        [OperationType],
        [FieldName],
        [OldValue],
        [NewValue],
        [ChangedBy],
        [ChangeDescription]
    )
    VALUES
    (
        @EntityType,
        @EntityId,
        @EntityName,
        @OperationType,
        @FieldName,
        @OldValue,
        @NewValue,
        @ChangedBy,
        @ChangeDescription
    );

    SELECT SCOPE_IDENTITY() AS NewId;
END
GO

PRINT '✓ Stored procedures [usp_InsertHistoryLog] created successfully';
