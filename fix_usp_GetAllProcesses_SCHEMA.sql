-- =============================================
-- FIX: Add missing columns to schema-qualified usp_GetAllProcesses
-- Critical: Index (Primary Key) was missing, causing SqlNullValueException
-- =============================================
-- IMPORTANT: Replace 'PRODUCTION_Selenium' with your actual schema name
-- =============================================

USE [TestAutomationManager]
GO

-- Drop the existing stored procedure in the PRODUCTION_Selenium schema
-- REPLACE 'PRODUCTION_Selenium' with your actual schema name!
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'usp_GetAllProcesses' AND schema_id = SCHEMA_ID('PRODUCTION_Selenium'))
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetAllProcesses]
GO

-- Recreate with ALL columns that the Process entity expects
CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetAllProcesses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        -- Primary Key (CRITICAL - was missing!)
        [Index],

        -- Core columns
        [ProcessID],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [TestID],
        [ProcessName],
        [ProcessPosition],
        [LastRunning],

        -- Additional columns that were missing
        [Comments],
        [Module],
        [Repeat],
        [TempParam],
        [TempParam1],
        [TempParam11],
        [TempParam111],
        [TempParam1111],
        [TempParam11111],

        -- All Param columns (Param1-Param46)
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35],
        [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]

    FROM [PRODUCTION_Selenium].[Process_WEB3] WITH (NOLOCK)
    ORDER BY [ProcessID]
    OPTION (MAXDOP 4, RECOMPILE);  -- Parallel + adaptive query plan
END
GO

PRINT 'Schema-qualified stored procedure [PRODUCTION_Selenium].[usp_GetAllProcesses] has been fixed!'
PRINT 'Critical: Added missing Index (Primary Key) column'
PRINT 'Also added: Comments, Module, Repeat, TempParam, TempParam1, TempParam11, TempParam111, TempParam1111, TempParam11111'
