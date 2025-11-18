-- =============================================
-- Stored Procedure: Get Process History Including Functions
-- Description: Retrieves history for a Process AND all its related Functions
-- =============================================

-- SeleniumDB Schema
CREATE OR ALTER PROCEDURE [SeleniumDB].[usp_GetProcessHistoryWithFunctions]
    @ProcessIndex INT,
    @ProcessID DOUBLE PRECISION,
    @Top INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    -- Get combined history for Process and its Functions
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
    WHERE
        -- Process history by Index
        ([EntityType] = 'Process' AND [EntityId] = CAST(@ProcessIndex AS NVARCHAR(50)))
        OR
        -- Function history by ProcessID
        ([EntityType] = 'Function' AND [EntityId] IN (
            SELECT CAST([Index] AS NVARCHAR(50))
            FROM [SeleniumDB].[Function_WEB3] WITH (NOLOCK)
            WHERE [ProcessID] = @ProcessID
        ))
    ORDER BY [ChangedAt] DESC
    OPTION (MAXDOP 4);
END
GO

-- PRODUCTION_Selenium Schema
CREATE OR ALTER PROCEDURE [PRODUCTION_Selenium].[usp_GetProcessHistoryWithFunctions]
    @ProcessIndex INT,
    @ProcessID DOUBLE PRECISION,
    @Top INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    -- Get combined history for Process and its Functions
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
    WHERE
        -- Process history by Index
        ([EntityType] = 'Process' AND [EntityId] = CAST(@ProcessIndex AS NVARCHAR(50)))
        OR
        -- Function history by ProcessID
        ([EntityType] = 'Function' AND [EntityId] IN (
            SELECT CAST([Index] AS NVARCHAR(50))
            FROM [PRODUCTION_Selenium].[Function_WEB3] WITH (NOLOCK)
            WHERE [ProcessID] = @ProcessID
        ))
    ORDER BY [ChangedAt] DESC
    OPTION (MAXDOP 4);
END
GO

PRINT '✓ Stored procedures [usp_GetProcessHistoryWithFunctions] created successfully';
