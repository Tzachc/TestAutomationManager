-- Quick verification script to check if History feature is set up
-- Run this in SQL Server Management Studio

PRINT '========================================';
PRINT 'HISTORY FEATURE VERIFICATION';
PRINT '========================================';
PRINT '';

-- Check if HistoryLog table exists in SeleniumDB
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SeleniumDB].[HistoryLog]') AND type in (N'U'))
    PRINT '✓ Table [SeleniumDB].[HistoryLog] exists'
ELSE
    PRINT '✗ Table [SeleniumDB].[HistoryLog] MISSING!'

-- Check if HistoryLog table exists in PRODUCTION_Selenium
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PRODUCTION_Selenium].[HistoryLog]') AND type in (N'U'))
    PRINT '✓ Table [PRODUCTION_Selenium].[HistoryLog] exists'
ELSE
    PRINT '✗ Table [PRODUCTION_Selenium].[HistoryLog] MISSING!'

PRINT '';

-- Check stored procedures in SeleniumDB
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SeleniumDB].[usp_InsertHistoryLog]') AND type in (N'P', N'PC'))
    PRINT '✓ Procedure [SeleniumDB].[usp_InsertHistoryLog] exists'
ELSE
    PRINT '✗ Procedure [SeleniumDB].[usp_InsertHistoryLog] MISSING!'

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SeleniumDB].[usp_GetHistoryLog]') AND type in (N'P', N'PC'))
    PRINT '✓ Procedure [SeleniumDB].[usp_GetHistoryLog] exists'
ELSE
    PRINT '✗ Procedure [SeleniumDB].[usp_GetHistoryLog] MISSING!'

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SeleniumDB].[usp_GetProcessHistoryWithFunctions]') AND type in (N'P', N'PC'))
    PRINT '✓ Procedure [SeleniumDB].[usp_GetProcessHistoryWithFunctions] exists'
ELSE
    PRINT '✗ Procedure [SeleniumDB].[usp_GetProcessHistoryWithFunctions] MISSING!'

PRINT '';

-- Check table structure
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SeleniumDB].[HistoryLog]') AND type in (N'U'))
BEGIN
    PRINT 'HistoryLog table structure:';
    SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'SeleniumDB' AND TABLE_NAME = 'HistoryLog'
    ORDER BY ORDINAL_POSITION;

    PRINT '';
    PRINT 'Row count in HistoryLog:';
    DECLARE @rowCount INT;
    SELECT @rowCount = COUNT(*) FROM [SeleniumDB].[HistoryLog];
    PRINT CAST(@rowCount AS VARCHAR(10)) + ' rows';
END

PRINT '';
PRINT '========================================';
