-- =============================================
-- Get All Functions from Function_WEB3 (Optimized for large datasets)
-- This SP should be created in BOTH schemas:
--   1. [SeleniumDB].[usp_GetAllFunctions]
--   2. [PRODUCTION_Selenium].[usp_GetAllFunctions]
-- =============================================

-- FOR SeleniumDB SCHEMA:
USE [YourDatabaseName]
GO

CREATE OR ALTER PROCEDURE [SeleniumDB].[usp_GetAllFunctions]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [FunctionName],
        [FunctionDescription],
        [FunctionPosition],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [Comments],
        [ActualValue],
        [BreakPoint],
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30]
    FROM [SeleniumDB].[Function_WEB3] WITH (NOLOCK)
    ORDER BY [ProcessID], [FunctionPosition]
    OPTION (MAXDOP 4, RECOMPILE);  -- Parallel + adaptive query plan
END
GO

-- FOR PRODUCTION_Selenium SCHEMA:
CREATE OR ALTER PROCEDURE [PRODUCTION_Selenium].[usp_GetAllFunctions]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [FunctionName],
        [FunctionDescription],
        [FunctionPosition],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [Comments],
        [ActualValue],
        [BreakPoint],
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30]
    FROM [PRODUCTION_Selenium].[Function_WEB3] WITH (NOLOCK)
    ORDER BY [ProcessID], [FunctionPosition]
    OPTION (MAXDOP 4, RECOMPILE);  -- Parallel + adaptive query plan
END
GO

-- TEST THE STORED PROCEDURES:
-- EXEC [SeleniumDB].[usp_GetAllFunctions];
-- EXEC [PRODUCTION_Selenium].[usp_GetAllFunctions];
