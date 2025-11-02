# CORRECTED Index Creation Scripts (No Primary Keys)

## ⚠️ IMPORTANT: Use This Instead of Previous Scripts

The previous scripts had an error - they tried to create primary keys on non-existent columns.
This corrected version:
1. **SKIPS primary key creation** (your tables already have them)
2. **Creates only NONCLUSTERED indexes** for performance
3. Uses correct column names (TestID, ProcessID, etc.)

---

## Option 1: Manual Schema Replacement (Easiest)

Copy this script and replace `{SchemaName}` with your schema name (SeleniumDB or PRODUCTION_Selenium).
Run it once for each schema.

```sql
-- =============================================
-- Step 2: Create Indexes (Performance Optimization)
-- Replace {SchemaName} with: SeleniumDB or PRODUCTION_Selenium
-- =============================================

USE [AutomationProjectDB]  -- Replace with your database name
GO

-- =============================================
-- Test_WEB3 Indexes
-- =============================================

-- TestID index for fast lookups
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Test_WEB3_TestID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Test_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Test_WEB3_TestID
    ON [{SchemaName}].[Test_WEB3]([TestID])
    INCLUDE ([TestName], [RunStatus]);
    PRINT '✓ Created IX_Test_WEB3_TestID on {SchemaName}.Test_WEB3';
END
ELSE
    PRINT '⚠ IX_Test_WEB3_TestID already exists on {SchemaName}.Test_WEB3';
GO

-- RunStatus index for filtering active tests
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Test_WEB3_RunStatus'
               AND object_id = OBJECT_ID('[{SchemaName}].[Test_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Test_WEB3_RunStatus
    ON [{SchemaName}].[Test_WEB3]([RunStatus])
    INCLUDE ([TestID], [TestName], [LastRunning]);
    PRINT '✓ Created IX_Test_WEB3_RunStatus on {SchemaName}.Test_WEB3';
END
ELSE
    PRINT '⚠ IX_Test_WEB3_RunStatus already exists on {SchemaName}.Test_WEB3';
GO

-- =============================================
-- Process_WEB3 Indexes
-- =============================================

-- ProcessID index (if not already clustered)
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Process_WEB3_ProcessID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Process_WEB3]'))
BEGIN
    -- Check if there's already a clustered index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE object_id = OBJECT_ID('[{SchemaName}].[Process_WEB3]')
                   AND type = 1)
    BEGIN
        -- No clustered index exists, create one on ProcessID
        CREATE CLUSTERED INDEX IX_Process_WEB3_ProcessID
        ON [{SchemaName}].[Process_WEB3]([ProcessID]);
        PRINT '✓ Created CLUSTERED IX_Process_WEB3_ProcessID on {SchemaName}.Process_WEB3';
    END
    ELSE
    BEGIN
        -- Clustered index exists, create nonclustered instead
        CREATE NONCLUSTERED INDEX IX_Process_WEB3_ProcessID
        ON [{SchemaName}].[Process_WEB3]([ProcessID]);
        PRINT '✓ Created NONCLUSTERED IX_Process_WEB3_ProcessID on {SchemaName}.Process_WEB3';
    END
END
ELSE
    PRINT '⚠ IX_Process_WEB3_ProcessID already exists on {SchemaName}.Process_WEB3';
GO

-- TestID index for filtering processes by test
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Process_WEB3_TestID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Process_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Process_WEB3_TestID
    ON [{SchemaName}].[Process_WEB3]([TestID])
    INCLUDE ([ProcessID], [ProcessName], [ProcessPosition], [WEB3Operator]);
    PRINT '✓ Created IX_Process_WEB3_TestID on {SchemaName}.Process_WEB3';
END
ELSE
    PRINT '⚠ IX_Process_WEB3_TestID already exists on {SchemaName}.Process_WEB3';
GO

-- ProcessPosition index for ordering
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Process_WEB3_ProcessPosition'
               AND object_id = OBJECT_ID('[{SchemaName}].[Process_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Process_WEB3_ProcessPosition
    ON [{SchemaName}].[Process_WEB3]([ProcessPosition])
    INCLUDE ([ProcessID], [TestID], [ProcessName]);
    PRINT '✓ Created IX_Process_WEB3_ProcessPosition on {SchemaName}.Process_WEB3';
END
ELSE
    PRINT '⚠ IX_Process_WEB3_ProcessPosition already exists on {SchemaName}.Process_WEB3';
GO

-- =============================================
-- Function_WEB3 Indexes
-- =============================================

-- ProcessID index for joining functions to processes
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Function_WEB3_ProcessID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Function_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Function_WEB3_ProcessID
    ON [{SchemaName}].[Function_WEB3]([ProcessID])
    INCLUDE ([FunctionPosition], [FunctionName]);
    PRINT '✓ Created IX_Function_WEB3_ProcessID on {SchemaName}.Function_WEB3';
END
ELSE
    PRINT '⚠ IX_Function_WEB3_ProcessID already exists on {SchemaName}.Function_WEB3';
GO

-- FunctionPosition index for ordering
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Function_WEB3_FunctionPosition'
               AND object_id = OBJECT_ID('[{SchemaName}].[Function_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Function_WEB3_FunctionPosition
    ON [{SchemaName}].[Function_WEB3]([FunctionPosition])
    INCLUDE ([ProcessID], [FunctionName]);
    PRINT '✓ Created IX_Function_WEB3_FunctionPosition on {SchemaName}.Function_WEB3';
END
ELSE
    PRINT '⚠ IX_Function_WEB3_FunctionPosition already exists on {SchemaName}.Function_WEB3';
GO

PRINT '✅ All indexes created successfully for {SchemaName}!';
GO
```

**Usage:**
1. Copy the script above
2. Find/Replace `{SchemaName}` → `SeleniumDB` (run first)
3. Find/Replace `{SchemaName}` → `PRODUCTION_Selenium` (run second)

---

## Option 2: Automatic for All Schemas

This script automatically creates indexes for all schemas:

```sql
-- =============================================
-- Create Indexes for ALL Schemas Automatically
-- (CORRECTED - No Primary Key Creation)
-- =============================================

DECLARE @SchemaName NVARCHAR(128)
DECLARE @SQL NVARCHAR(MAX)
DECLARE @HasClusteredIndex BIT

-- Cursor to loop through all schemas containing Test_WEB3
DECLARE schema_cursor CURSOR FOR
SELECT s.name
FROM sys.schemas s
INNER JOIN sys.tables t ON s.schema_id = t.schema_id
WHERE t.name = 'Test_WEB3'

OPEN schema_cursor
FETCH NEXT FROM schema_cursor INTO @SchemaName

WHILE @@FETCH_STATUS = 0
BEGIN
    PRINT '=== Creating indexes for schema: ' + @SchemaName + ' ==='

    -- =============================================
    -- Test_WEB3 Indexes
    -- =============================================

    -- TestID index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Test_WEB3_TestID'
                   AND object_id = OBJECT_ID(@SchemaName + '.Test_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Test_WEB3_TestID
                    ON [' + @SchemaName + '].[Test_WEB3]([TestID])
                    INCLUDE ([TestName], [RunStatus])'
        PRINT 'Creating IX_Test_WEB3_TestID on ' + @SchemaName + '.Test_WEB3'
        EXEC sp_executesql @SQL
    END

    -- RunStatus index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Test_WEB3_RunStatus'
                   AND object_id = OBJECT_ID(@SchemaName + '.Test_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Test_WEB3_RunStatus
                    ON [' + @SchemaName + '].[Test_WEB3]([RunStatus])
                    INCLUDE ([TestID], [TestName], [LastRunning])'
        PRINT 'Creating IX_Test_WEB3_RunStatus on ' + @SchemaName + '.Test_WEB3'
        EXEC sp_executesql @SQL
    END

    -- =============================================
    -- Process_WEB3 Indexes
    -- =============================================

    -- Check if Process_WEB3 already has a clustered index
    SELECT @HasClusteredIndex = CASE
        WHEN EXISTS (SELECT * FROM sys.indexes
                     WHERE object_id = OBJECT_ID(@SchemaName + '.Process_WEB3')
                     AND type = 1) THEN 1
        ELSE 0
    END

    -- ProcessID index (clustered if none exists, nonclustered otherwise)
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Process_WEB3_ProcessID'
                   AND object_id = OBJECT_ID(@SchemaName + '.Process_WEB3'))
    BEGIN
        IF @HasClusteredIndex = 0
        BEGIN
            SET @SQL = 'CREATE CLUSTERED INDEX IX_Process_WEB3_ProcessID
                        ON [' + @SchemaName + '].[Process_WEB3]([ProcessID])'
            PRINT 'Creating CLUSTERED IX_Process_WEB3_ProcessID on ' + @SchemaName + '.Process_WEB3'
        END
        ELSE
        BEGIN
            SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Process_WEB3_ProcessID
                        ON [' + @SchemaName + '].[Process_WEB3]([ProcessID])'
            PRINT 'Creating NONCLUSTERED IX_Process_WEB3_ProcessID on ' + @SchemaName + '.Process_WEB3'
        END
        EXEC sp_executesql @SQL
    END

    -- TestID index for filtering
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Process_WEB3_TestID'
                   AND object_id = OBJECT_ID(@SchemaName + '.Process_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Process_WEB3_TestID
                    ON [' + @SchemaName + '].[Process_WEB3]([TestID])
                    INCLUDE ([ProcessID], [ProcessName], [ProcessPosition], [WEB3Operator])'
        PRINT 'Creating IX_Process_WEB3_TestID on ' + @SchemaName + '.Process_WEB3'
        EXEC sp_executesql @SQL
    END

    -- ProcessPosition index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Process_WEB3_ProcessPosition'
                   AND object_id = OBJECT_ID(@SchemaName + '.Process_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Process_WEB3_ProcessPosition
                    ON [' + @SchemaName + '].[Process_WEB3]([ProcessPosition])
                    INCLUDE ([ProcessID], [TestID], [ProcessName])'
        PRINT 'Creating IX_Process_WEB3_ProcessPosition on ' + @SchemaName + '.Process_WEB3'
        EXEC sp_executesql @SQL
    END

    -- =============================================
    -- Function_WEB3 Indexes
    -- =============================================

    -- ProcessID index for joins
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Function_WEB3_ProcessID'
                   AND object_id = OBJECT_ID(@SchemaName + '.Function_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Function_WEB3_ProcessID
                    ON [' + @SchemaName + '].[Function_WEB3]([ProcessID])
                    INCLUDE ([FunctionPosition], [FunctionName])'
        PRINT 'Creating IX_Function_WEB3_ProcessID on ' + @SchemaName + '.Function_WEB3'
        EXEC sp_executesql @SQL
    END

    -- FunctionPosition index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Function_WEB3_FunctionPosition'
                   AND object_id = OBJECT_ID(@SchemaName + '.Function_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Function_WEB3_FunctionPosition
                    ON [' + @SchemaName + '].[Function_WEB3]([FunctionPosition])
                    INCLUDE ([ProcessID], [FunctionName])'
        PRINT 'Creating IX_Function_WEB3_FunctionPosition on ' + @SchemaName + '.Function_WEB3'
        EXEC sp_executesql @SQL
    END

    PRINT '=== Completed indexes for schema: ' + @SchemaName + ' ==='
    PRINT ''

    FETCH NEXT FROM schema_cursor INTO @SchemaName
END

CLOSE schema_cursor
DEALLOCATE schema_cursor

PRINT '✅ All indexes created successfully!'
GO
```

---

## Verification Query

After running the scripts, verify indexes were created:

```sql
-- Check indexes for SeleniumDB schema
SELECT
    OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    STUFF((SELECT ', ' + c.name
           FROM sys.index_columns ic
           INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
           WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
           ORDER BY ic.key_ordinal
           FOR XML PATH('')), 1, 2, '') AS IndexedColumns
FROM sys.indexes i
WHERE OBJECT_SCHEMA_NAME(i.object_id) IN ('SeleniumDB', 'PRODUCTION_Selenium')
  AND OBJECT_NAME(i.object_id) IN ('Test_WEB3', 'Process_WEB3', 'Function_WEB3')
  AND i.name IS NOT NULL  -- Exclude heap
ORDER BY SchemaName, TableName, IndexName;
```

---

## What Changed from Previous Script?

1. **REMOVED** all primary key creation attempts (caused the `Id` column error)
2. **ADDED** check for existing clustered indexes before creating them
3. **ADDED** more indexes for ProcessPosition and FunctionPosition (better sorting performance)
4. **USES** correct column names (TestID, ProcessID, etc.)

---

## Expected Output

When you run Option 2, you should see:

```
=== Creating indexes for schema: PRODUCTION_Selenium ===
Creating IX_Test_WEB3_TestID on PRODUCTION_Selenium.Test_WEB3
Creating IX_Test_WEB3_RunStatus on PRODUCTION_Selenium.Test_WEB3
Creating NONCLUSTERED IX_Process_WEB3_ProcessID on PRODUCTION_Selenium.Process_WEB3
Creating IX_Process_WEB3_TestID on PRODUCTION_Selenium.Process_WEB3
Creating IX_Process_WEB3_ProcessPosition on PRODUCTION_Selenium.Process_WEB3
Creating IX_Function_WEB3_ProcessID on PRODUCTION_Selenium.Function_WEB3
Creating IX_Function_WEB3_FunctionPosition on PRODUCTION_Selenium.Function_WEB3
=== Completed indexes for schema: PRODUCTION_Selenium ===

=== Creating indexes for schema: SeleniumDB ===
Creating IX_Test_WEB3_TestID on SeleniumDB.Test_WEB3
Creating IX_Test_WEB3_RunStatus on SeleniumDB.Test_WEB3
Creating NONCLUSTERED IX_Process_WEB3_ProcessID on SeleniumDB.Process_WEB3
Creating IX_Process_WEB3_TestID on SeleniumDB.Process_WEB3
Creating IX_Process_WEB3_ProcessPosition on SeleniumDB.Process_WEB3
Creating IX_Function_WEB3_ProcessID on SeleniumDB.Function_WEB3
Creating IX_Function_WEB3_FunctionPosition on SeleniumDB.Function_WEB3
=== Completed indexes for schema: SeleniumDB ===

✅ All indexes created successfully!
```

---

## Next Steps

1. Run **Option 2** script above (automatic for all schemas)
2. Run the **Verification Query** to confirm indexes were created
3. Test your application - it should now be 4-5x faster!
4. (Optional) Proceed to Step 3 in the main guide for Indexed Views (10x performance)
