# FIXED usp_GetFunctionsByProcessID Stored Procedure

## ⚠️ CRITICAL FIX - Correct Column Names

The Function_WEB3 table has:
- `Index` (NOT `FunctionID`)
- Param1 through Param30 (NOT Param1-Param50)

---

## Run This Script to Fix Both Schemas

```sql
-- =============================================
-- Fix usp_GetFunctionsByProcessID in BOTH Schemas
-- =============================================

-- Drop and recreate for SeleniumDB schema
IF OBJECT_ID('[SeleniumDB].[usp_GetFunctionsByProcessID]', 'P') IS NOT NULL
    DROP PROCEDURE [SeleniumDB].[usp_GetFunctionsByProcessID];
GO

CREATE PROCEDURE [SeleniumDB].[usp_GetFunctionsByProcessID]
    @ProcessID FLOAT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [FunctionName],
        [FunctionPosition],
        [ActualValue],
        [BreakPoint],
        [Comments],
        [FunctionDescription],
        [Pass_Fail_WEB3Operator],
        [WEB3Operator],
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30]
    FROM [SeleniumDB].[Function_WEB3] WITH (NOLOCK)
    WHERE [ProcessID] = @ProcessID
    ORDER BY [FunctionPosition]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

PRINT '✅ Fixed usp_GetFunctionsByProcessID in SeleniumDB schema';
GO

-- Drop and recreate for PRODUCTION_Selenium schema
IF OBJECT_ID('[PRODUCTION_Selenium].[usp_GetFunctionsByProcessID]', 'P') IS NOT NULL
    DROP PROCEDURE [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID];
GO

CREATE PROCEDURE [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID]
    @ProcessID FLOAT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [Index],
        [ProcessID],
        [FunctionName],
        [FunctionPosition],
        [ActualValue],
        [BreakPoint],
        [Comments],
        [FunctionDescription],
        [Pass_Fail_WEB3Operator],
        [WEB3Operator],
        [Param1], [Param2], [Param3], [Param4], [Param5],
        [Param6], [Param7], [Param8], [Param9], [Param10],
        [Param11], [Param12], [Param13], [Param14], [Param15],
        [Param16], [Param17], [Param18], [Param19], [Param20],
        [Param21], [Param22], [Param23], [Param24], [Param25],
        [Param26], [Param27], [Param28], [Param29], [Param30]
    FROM [PRODUCTION_Selenium].[Function_WEB3] WITH (NOLOCK)
    WHERE [ProcessID] = @ProcessID
    ORDER BY [FunctionPosition]
    OPTION (MAXDOP 4, RECOMPILE);
END
GO

PRINT '✅ Fixed usp_GetFunctionsByProcessID in PRODUCTION_Selenium schema';
GO

PRINT '';
PRINT '✅✅✅ Both schemas fixed successfully!';
PRINT '';
PRINT 'Test with:';
PRINT '  EXEC [SeleniumDB].[usp_GetFunctionsByProcessID] @ProcessID = 1';
PRINT '  EXEC [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID] @ProcessID = 1';
GO
```

---

## Test After Running

```sql
-- Test both schemas
SET STATISTICS TIME ON;

EXEC [SeleniumDB].[usp_GetFunctionsByProcessID] @ProcessID = 1;
EXEC [PRODUCTION_Selenium].[usp_GetFunctionsByProcessID] @ProcessID = 1;

SET STATISTICS TIME OFF;
```

Should show results with NO ERRORS!

---

## What Changed

### Before (WRONG):
```sql
SELECT
    [FunctionID],  -- ❌ Column doesn't exist!
    [Param31], [Param32], ..., [Param50]  -- ❌ Columns don't exist!
FROM [Function_WEB3]
```

### After (CORRECT):
```sql
SELECT
    [Index],  -- ✅ Correct primary key column
    [Param1], [Param2], ..., [Param30]  -- ✅ Only 30 params exist
FROM [SeleniumDB].[Function_WEB3]
```

---

## Columns Included

Based on the Function model (DataModels.cs:522-635):
- `Index` - Primary key (NOT FunctionID)
- `ProcessID` - Foreign key
- `FunctionName` - Function name
- `FunctionPosition` - Ordering
- `ActualValue`, `BreakPoint`, `Comments`, `FunctionDescription`
- `Pass_Fail_WEB3Operator`, `WEB3Operator`
- `Param1` through `Param30` (30 parameters, NOT 50)

This matches the actual C# model exactly!
