# COMPLETE FIX - Process Stored Procedures (All Columns)

## ⚠️ CRITICAL FIX - Missing Index and Other Columns

The Process_WEB3 stored procedures are missing several columns that EF Core expects:
- **`Index`** (primary key - CRITICAL!)
- `Comments`, `Module`, `Repeat`, `TempParam`
- `TempParam1`, `TempParam11`, `TempParam111`, `TempParam1111`, `TempParam11111`

---

## Run This Script to Fix BOTH Process Stored Procedures

```sql
-- =============================================
-- Fix usp_GetAllProcesses and usp_GetProcessesByTestID
-- Include ALL columns that EF Core expects
-- =============================================

-- =============================================
-- SeleniumDB Schema
-- =============================================

-- Drop and recreate usp_GetAllProcesses
IF OBJECT_ID('[SeleniumDB].[usp_GetAllProcesses]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetAllProcesses];
GO

CREATE PROCEDURE [SeleniumDB].[usp_GetAllProcesses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [TestID],
        [ProcessName],
        [ProcessPosition],
        [LastRunning],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [Comments],
        [Module],
        [Repeat],
        [TempParam],
        [TempParam1],
        [TempParam11],
        [TempParam111],
        [TempParam1111],
        [TempParam11111],
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35],
        [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]
    FROM [SeleniumDB].[Process_WEB3] WITH (NOLOCK)
    ORDER BY [ProcessID]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- Drop and recreate usp_GetProcessesByTestID
IF OBJECT_ID('[SeleniumDB].[usp_GetProcessesByTestID]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetProcessesByTestID];
GO

CREATE PROCEDURE [SeleniumDB].[usp_GetProcessesByTestID]
    @TestID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [TestID],
        [ProcessName],
        [ProcessPosition],
        [LastRunning],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [Comments],
        [Module],
        [Repeat],
        [TempParam],
        [TempParam1],
        [TempParam11],
        [TempParam111],
        [TempParam1111],
        [TempParam11111],
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30],
        [Param31], [Param32], [Param33], [Param34], [Param35],
        [Param36], [Param37], [Param38], [Param39], [Param40],
        [Param41], [Param42], [Param43], [Param44], [Param45], [Param46]
    FROM [SeleniumDB].[Process_WEB3] WITH (NOLOCK)
    WHERE [TestID] = @TestID
    ORDER BY [ProcessPosition], [ProcessID], [ProcessName]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

PRINT '✅ Fixed process stored procedures in SeleniumDB schema';
GO

-- =============================================
-- PRODUCTION_Selenium Schema
-- =============================================

-- Drop and recreate usp_GetAllProcesses
IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetAllProcesses]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetAllProcesses];
GO

CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetAllProcesses]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [TestID],
        [ProcessName],
        [ProcessPosition],
        [LastRunning],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [Comments],
        [Module],
        [Repeat],
        [TempParam],
        [TempParam1],
        [TempParam11],
        [TempParam111],
        [TempParam1111],
        [TempParam11111],
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
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

-- Drop and recreate usp_GetProcessesByTestID
IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetProcessesByTestID]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetProcessesByTestID];
GO

CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetProcessesByTestID]
    @TestID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [TestID],
        [ProcessName],
        [ProcessPosition],
        [LastRunning],
        [WEB3Operator],
        [Pass_Fail_WEB3Operator],
        [Comments],
        [Module],
        [Repeat],
        [TempParam],
        [TempParam1],
        [TempParam11],
        [TempParam111],
        [TempParam1111],
        [TempParam11111],
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
    WHERE [TestID] = @TestID
    ORDER BY [ProcessPosition], [ProcessID], [ProcessName]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

PRINT '✅ Fixed process stored procedures in PRODUCTION_Selenium schema';
GO

PRINT '';
PRINT '✅✅✅ Both schemas fixed successfully!';
PRINT '';
PRINT 'Test with:';
PRINT '  EXEC [SeleniumDB].[usp_GetAllProcesses]';
PRINT '  EXEC [SeleniumDB].[usp_GetProcessesByTestID] @TestID = 1';
PRINT '  EXEC [PRODUCTION_Selenium].[usp_GetAllProcesses]';
PRINT '  EXEC [PRODUCTION_Selenium].[usp_GetProcessesByTestID] @TestID = 1';
GO
```

---

## Test After Running

```sql
SET STATISTICS TIME ON;

-- Test SeleniumDB
EXEC [SeleniumDB].[usp_GetAllProcesses];
EXEC [SeleniumDB].[usp_GetProcessesByTestID] @TestID = 12;

-- Test PRODUCTION_Selenium
EXEC [PRODUCTION_Selenium].[usp_GetAllProcesses];
EXEC [PRODUCTION_Selenium].[usp_GetProcessesByTestID] @TestID = 12;

SET STATISTICS TIME OFF;
```

---

## What Was Added

### Previously Missing Columns:
- ✅ **`Index`** - Primary key (was causing the error!)
- ✅ `Comments` - Process comments
- ✅ `Module` - Module name
- ✅ `Repeat` - Repeat configuration
- ✅ `TempParam` - Temporary parameter
- ✅ `TempParam1`, `TempParam11`, `TempParam111`, `TempParam1111`, `TempParam11111` - Additional temp params

### Complete Column List (Process Entity):
1. `Index` - Primary key
2. `ProcessID` - Process identifier
3. `TestID` - Foreign key to Test
4. `ProcessName` - Name of process
5. `ProcessPosition` - Order position
6. `LastRunning` - Last run timestamp
7. `WEB3Operator` - Web3 operator
8. `Pass_Fail_WEB3Operator` - Pass/fail operator
9. `Comments` - Comments
10. `Module` - Module
11. `Repeat` - Repeat config
12. `TempParam` - Temp parameter
13. `TempParam1`, `TempParam11`, `TempParam111`, `TempParam1111`, `TempParam11111` - Extra temp params
14. `Param1` through `Param46` - 46 parameter columns

This matches the Process model exactly (DataModels.cs:277-441)!
