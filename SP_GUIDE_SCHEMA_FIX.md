# CORRECTED Stored Procedures (Schema-Aware)

## ⚠️ CRITICAL FIX - Use Schema Names

The original stored procedures were created in `[dbo]` schema and referenced tables without schema names.
Your tables are in `SeleniumDB` and `PRODUCTION_Selenium` schemas, so we need to create the SPs in EACH schema.

---

## Option 1: Create SPs in BOTH Schemas (RECOMMENDED)

Run this script to create stored procedures in both SeleniumDB and PRODUCTION_Selenium schemas:

```sql
USE [AutomationProjectDB]  -- Replace with your database name
GO

-- =============================================
-- Create Stored Procedures in SeleniumDB Schema
-- =============================================

-- Drop existing procedures if they exist
IF OBJECT_ID('[SeleniumDB].[usp_GetAllTests]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetAllTests];
GO

IF OBJECT_ID('[SeleniumDB].[usp_GetAllProcesses]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetAllProcesses];
GO

IF OBJECT_ID('[SeleniumDB].[usp_GetProcessesByTestID]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetProcessesByTestID];
GO

IF OBJECT_ID('[SeleniumDB].[usp_GetFunctionsByProcessID]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetFunctionsByProcessID];
GO

-- =============================================
-- Procedure: usp_GetAllTests (SeleniumDB)
-- =============================================
CREATE PROCEDURE [SeleniumDB].[usp_GetAllTests]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [TestID], [TestName], [Bugs], [DisableKillDriver],
        [EmailOnFailureOnly], [ExceptionMessage], [ExitTestOnFailure],
        [LastRunning], [LastTimePass], [RecipientsEmailsList],
        [RunStatus], [SendEmailReport], [SnapshotMultipleFailure],
        [TestRunAgainTimes]
    FROM [SeleniumDB].[Test_WEB3] WITH (NOLOCK)
    ORDER BY [TestID]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- =============================================
-- Procedure: usp_GetAllProcesses (SeleniumDB)
-- =============================================
CREATE PROCEDURE [SeleniumDB].[usp_GetAllProcesses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [ProcessID], [WEB3Operator], [Pass_Fail_WEB3Operator],
        [TestID], [ProcessName], [ProcessPosition], [LastRunning],
        [Param1], [Param2], [Param3], [Param4], [Param5], [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15], [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25], [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35], [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]
    FROM [SeleniumDB].[Process_WEB3] WITH (NOLOCK)
    ORDER BY [ProcessID]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- =============================================
-- Procedure: usp_GetProcessesByTestID (SeleniumDB)
-- =============================================
CREATE PROCEDURE [SeleniumDB].[usp_GetProcessesByTestID]
    @TestID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [ProcessID], [WEB3Operator], [Pass_Fail_WEB3Operator],
        [TestID], [ProcessName], [ProcessPosition], [LastRunning],
        [Param1], [Param2], [Param3], [Param4], [Param5], [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15], [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25], [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35], [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]
    FROM [SeleniumDB].[Process_WEB3] WITH (NOLOCK)
    WHERE [TestID] = @TestID
    ORDER BY [ProcessPosition], [ProcessID], [ProcessName]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- =============================================
-- Procedure: usp_GetFunctionsByProcessID (SeleniumDB)
-- =============================================
CREATE PROCEDURE [SeleniumDB].[usp_GetFunctionsByProcessID]
    @ProcessID FLOAT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [FunctionID], [ProcessID], [FunctionName], [FunctionPosition],
        [Param1], [Param2], [Param3], [Param4], [Param5], [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15], [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25], [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35], [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46], [Param47], [Param48], [Param49], [Param50]
    FROM [SeleniumDB].[Function_WEB3] WITH (NOLOCK)
    WHERE [ProcessID] = @ProcessID
    ORDER BY [FunctionPosition]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

PRINT '✅ Created stored procedures in SeleniumDB schema';
GO

-- =============================================
-- Create Stored Procedures in PRODUCTION_Selenium Schema
-- =============================================

-- Drop existing procedures if they exist
IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetAllTests]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetAllTests];
GO

IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetAllProcesses]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetAllProcesses];
GO

IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetProcessesByTestID]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetProcessesByTestID];
GO

IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetFunctionsByProcessID]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID];
GO

-- =============================================
-- Procedure: usp_GetAllTests (PRODUCTION_Selenium)
-- =============================================
CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetAllTests]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [TestID], [TestName], [Bugs], [DisableKillDriver],
        [EmailOnFailureOnly], [ExceptionMessage], [ExitTestOnFailure],
        [LastRunning], [LastTimePass], [RecipientsEmailsList],
        [RunStatus], [SendEmailReport], [SnapshotMultipleFailure],
        [TestRunAgainTimes]
    FROM [PRODUCTION_Selenium].[Test_WEB3] WITH (NOLOCK)
    ORDER BY [TestID]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- =============================================
-- Procedure: usp_GetAllProcesses (PRODUCTION_Selenium)
-- =============================================
CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetAllProcesses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [ProcessID], [WEB3Operator], [Pass_Fail_WEB3Operator],
        [TestID], [ProcessName], [ProcessPosition], [LastRunning],
        [Param1], [Param2], [Param3], [Param4], [Param5], [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15], [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25], [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35], [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]
    FROM [PRODUCTION_Selenium].[Process_WEB3] WITH (NOLOCK)
    ORDER BY [ProcessID]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- =============================================
-- Procedure: usp_GetProcessesByTestID (PRODUCTION_Selenium)
-- =============================================
CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetProcessesByTestID]
    @TestID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [ProcessID], [WEB3Operator], [Pass_Fail_WEB3Operator],
        [TestID], [ProcessName], [ProcessPosition], [LastRunning],
        [Param1], [Param2], [Param3], [Param4], [Param5], [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15], [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25], [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35], [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]
    FROM [PRODUCTION_Selenium].[Process_WEB3] WITH (NOLOCK)
    WHERE [TestID] = @TestID
    ORDER BY [ProcessPosition], [ProcessID], [ProcessName]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- =============================================
-- Procedure: usp_GetFunctionsByProcessID (PRODUCTION_Selenium)
-- =============================================
CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID]
    @ProcessID FLOAT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [FunctionID], [ProcessID], [FunctionName], [FunctionPosition],
        [Param1], [Param2], [Param3], [Param4], [Param5], [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15], [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25], [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35], [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46], [Param47], [Param48], [Param49], [Param50]
    FROM [PRODUCTION_Selenium].[Function_WEB3] WITH (NOLOCK)
    WHERE [ProcessID] = @ProcessID
    ORDER BY [FunctionPosition]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

PRINT '✅ Created stored procedures in PRODUCTION_Selenium schema';
GO

PRINT '';
PRINT '✅✅✅ All stored procedures created successfully in BOTH schemas!';
PRINT '';
PRINT 'Test the procedures with:';
PRINT '  EXEC [SeleniumDB].[usp_GetAllTests]';
PRINT '  EXEC [PRODUCTION_Selenium].[usp_GetAllTests]';
GO
```

---

## Test the Fixed Stored Procedures

```sql
-- Test SeleniumDB schema procedures
SET STATISTICS TIME ON;

EXEC [SeleniumDB].[usp_GetAllTests];
EXEC [SeleniumDB].[usp_GetAllProcesses];
EXEC [SeleniumDB].[usp_GetProcessesByTestID] @TestID = 1;
EXEC [SeleniumDB].[usp_GetFunctionsByProcessID] @ProcessID = 1;

-- Test PRODUCTION_Selenium schema procedures
EXEC [PRODUCTION_Selenium].[usp_GetAllTests];
EXEC [PRODUCTION_Selenium].[usp_GetAllProcesses];
EXEC [PRODUCTION_Selenium].[usp_GetProcessesByTestID] @TestID = 1;
EXEC [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID] @ProcessID = 1;

SET STATISTICS TIME OFF;
```

---

## What Changed?

### Before (WRONG):
```sql
CREATE PROCEDURE [dbo].[usp_GetAllTests]
AS
BEGIN
    SELECT * FROM [Test_WEB3]  -- ❌ No schema!
```

### After (CORRECT):
```sql
CREATE PROCEDURE [SeleniumDB].[usp_GetAllTests]  -- ✅ SP in SeleniumDB schema
AS
BEGIN
    SELECT * FROM [SeleniumDB].[Test_WEB3]  -- ✅ Schema qualified!
```

---

## Verify Stored Procedures Exist

```sql
-- Check stored procedures were created
SELECT
    s.name AS SchemaName,
    p.name AS ProcedureName,
    p.create_date AS CreatedDate
FROM sys.procedures p
INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
WHERE s.name IN ('SeleniumDB', 'PRODUCTION_Selenium')
ORDER BY SchemaName, ProcedureName;
```

Should show:
```
SchemaName              ProcedureName
PRODUCTION_Selenium     usp_GetAllProcesses
PRODUCTION_Selenium     usp_GetAllTests
PRODUCTION_Selenium     usp_GetFunctionsByProcessID
PRODUCTION_Selenium     usp_GetProcessesByTestID
SeleniumDB              usp_GetAllProcesses
SeleniumDB              usp_GetAllTests
SeleniumDB              usp_GetFunctionsByProcessID
SeleniumDB              usp_GetProcessesByTestID
```

---

I also need to update the C# code to call the schema-qualified stored procedure names. Let me do that now.
