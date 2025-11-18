-- =============================================
-- HISTORY FEATURE COMPLETE SETUP SCRIPT
-- Description: Master script to set up complete History tracking feature
-- Run this script to create all tables and stored procedures
-- Created: 2025-11-18
-- =============================================

PRINT '========================================';
PRINT 'HISTORY FEATURE SETUP - Starting...';
PRINT '========================================';
PRINT '';

-- =============================================
-- STEP 1: CREATE HISTORY LOG TABLE
-- =============================================

PRINT 'STEP 1: Creating HistoryLog tables...';

-- Create table in SeleniumDB schema
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SeleniumDB].[HistoryLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [SeleniumDB].[HistoryLog] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EntityType] NVARCHAR(50) NOT NULL,              -- 'Test', 'Process', 'Function'
        [EntityId] NVARCHAR(50) NOT NULL,                -- TestID, Process.Index, Function.Index
        [EntityName] NVARCHAR(500),                      -- TestName, ProcessName, FunctionName (for display)
        [OperationType] NVARCHAR(20) NOT NULL,           -- 'INSERT', 'UPDATE', 'DELETE'
        [FieldName] NVARCHAR(100),                       -- Specific field that changed (NULL for DELETE/INSERT)
        [OldValue] NVARCHAR(MAX),                        -- Previous value
        [NewValue] NVARCHAR(MAX),                        -- New value
        [ChangedBy] NVARCHAR(100) DEFAULT 'System',      -- Username (future feature)
        [ChangedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [ChangeDescription] NVARCHAR(500),               -- Human-readable summary

        INDEX IX_EntityType_EntityId_ChangedAt NONCLUSTERED (EntityType, EntityId, ChangedAt DESC),
        INDEX IX_ChangedAt NONCLUSTERED (ChangedAt DESC)
    );

    PRINT '  ✓ Table [SeleniumDB].[HistoryLog] created successfully';
END
ELSE
BEGIN
    PRINT '  ! Table [SeleniumDB].[HistoryLog] already exists';
END
GO

-- Create table in PRODUCTION_Selenium schema
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PRODUCTION_Selenium].[HistoryLog]') AND type in (N'U'))
BEGIN
    CREATE TABLE [PRODUCTION_Selenium].[HistoryLog] (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [EntityType] NVARCHAR(50) NOT NULL,
        [EntityId] NVARCHAR(50) NOT NULL,
        [EntityName] NVARCHAR(500),
        [OperationType] NVARCHAR(20) NOT NULL,
        [FieldName] NVARCHAR(100),
        [OldValue] NVARCHAR(MAX),
        [NewValue] NVARCHAR(MAX),
        [ChangedBy] NVARCHAR(100) DEFAULT 'System',
        [ChangedAt] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [ChangeDescription] NVARCHAR(500),

        INDEX IX_EntityType_EntityId_ChangedAt NONCLUSTERED (EntityType, EntityId, ChangedAt DESC),
        INDEX IX_ChangedAt NONCLUSTERED (ChangedAt DESC)
    );

    PRINT '  ✓ Table [PRODUCTION_Selenium].[HistoryLog] created successfully';
END
ELSE
BEGIN
    PRINT '  ! Table [PRODUCTION_Selenium].[HistoryLog] already exists';
END
GO

PRINT '';
PRINT 'STEP 2: Creating stored procedures...';
PRINT '';

-- =============================================
-- STEP 2: CREATE STORED PROCEDURE - INSERT
-- =============================================

PRINT '  Creating [usp_InsertHistoryLog]...';

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

PRINT '    ✓ [usp_InsertHistoryLog] created in both schemas';

-- =============================================
-- STEP 3: CREATE STORED PROCEDURE - GET HISTORY
-- =============================================

PRINT '  Creating [usp_GetHistoryLog]...';

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

PRINT '    ✓ [usp_GetHistoryLog] created in both schemas';

-- =============================================
-- STEP 4: CREATE STORED PROCEDURE - GET PROCESS HISTORY WITH FUNCTIONS
-- =============================================

PRINT '  Creating [usp_GetProcessHistoryWithFunctions]...';

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

PRINT '    ✓ [usp_GetProcessHistoryWithFunctions] created in both schemas';
PRINT '';

-- =============================================
-- COMPLETION MESSAGE
-- =============================================

PRINT '========================================';
PRINT 'HISTORY FEATURE SETUP - COMPLETE! ✓';
PRINT '========================================';
PRINT '';
PRINT 'Summary:';
PRINT '  • HistoryLog tables created in SeleniumDB and PRODUCTION_Selenium';
PRINT '  • 3 stored procedures created in both schemas:';
PRINT '    - usp_InsertHistoryLog';
PRINT '    - usp_GetHistoryLog';
PRINT '    - usp_GetProcessHistoryWithFunctions';
PRINT '';
PRINT 'The History feature is now ready to use!';
PRINT 'You can now right-click on Tests or Processes to view their change history.';
PRINT '';
