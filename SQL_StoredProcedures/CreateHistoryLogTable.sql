-- =============================================
-- History Log Table Creation Script
-- Description: Tracks all changes to Tests, Processes, and Functions
-- Created: 2025-11-18
-- =============================================

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

    PRINT '✓ Table [SeleniumDB].[HistoryLog] created successfully';
END
ELSE
BEGIN
    PRINT '! Table [SeleniumDB].[HistoryLog] already exists';
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

    PRINT '✓ Table [PRODUCTION_Selenium].[HistoryLog] created successfully';
END
ELSE
BEGIN
    PRINT '! Table [PRODUCTION_Selenium].[HistoryLog] already exists';
END
GO
