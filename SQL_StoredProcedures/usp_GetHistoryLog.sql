-- =============================================
-- Stored Procedure: Get History Log Entries
-- Description: Retrieves history entries for a specific entity
-- =============================================

-- SeleniumDB Schema
CREATE OR ALTER PROCEDURE [SeleniumDB].[usp_GetHistoryLog]
    @EntityType NVARCHAR(50),
    @EntityId NVARCHAR(50),
    @Top INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        [Id],
        [EntityType],
        [EntityId],
        [EntityName],
        [OperationType],
        [FieldName],
        [OldValue],
        [NewValue],
        [ChangedBy],
        [ChangedAt],
        [ChangeDescription]
    FROM [SeleniumDB].[HistoryLog] WITH (NOLOCK)
    WHERE [EntityType] = @EntityType
        AND [EntityId] = @EntityId
    ORDER BY [ChangedAt] DESC
    OPTION (MAXDOP 4);
END
GO

-- PRODUCTION_Selenium Schema
CREATE OR ALTER PROCEDURE [PRODUCTION_Selenium].[usp_GetHistoryLog]
    @EntityType NVARCHAR(50),
    @EntityId NVARCHAR(50),
    @Top INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        [Id],
        [EntityType],
        [EntityId],
        [EntityName],
        [OperationType],
        [FieldName],
        [OldValue],
        [NewValue],
        [ChangedBy],
        [ChangedAt],
        [ChangeDescription]
    FROM [PRODUCTION_Selenium].[HistoryLog] WITH (NOLOCK)
    WHERE [EntityType] = @EntityType
        AND [EntityId] = @EntityId
    ORDER BY [ChangedAt] DESC
    OPTION (MAXDOP 4);
END
GO

PRINT '✓ Stored procedures [usp_GetHistoryLog] created successfully';
