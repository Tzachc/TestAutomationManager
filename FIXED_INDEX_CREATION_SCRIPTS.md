# FIXED - Index Creation Scripts for Dynamic Schemas

## Option 1: Manual Schema Replacement (EASIEST)

**Replace `{SchemaName}` with your actual schema (SeleniumDB or PRODUCTION_Selenium)**

```sql
-- =============================================
-- Indexes for Test_WEB3
-- REPLACE {SchemaName} with: SeleniumDB or PRODUCTION_Selenium
-- =============================================

-- Primary key index (if not exists)
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'PK_Test_WEB3_Id'
               AND object_id = OBJECT_ID('[{SchemaName}].[Test_WEB3]'))
BEGIN
    ALTER TABLE [{SchemaName}].[Test_WEB3]
    ADD CONSTRAINT PK_Test_WEB3_Id PRIMARY KEY CLUSTERED ([Id]);
END
GO

-- Index for TestID lookups
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Test_WEB3_TestID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Test_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Test_WEB3_TestID
    ON [{SchemaName}].[Test_WEB3]([TestID])
    INCLUDE ([TestName], [RunStatus]);
END
GO

-- =============================================
-- Indexes for Process_WEB3
-- =============================================

-- Clustered index on ProcessID (main key)
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Process_WEB3_ProcessID'
               AND type = 1
               AND object_id = OBJECT_ID('[{SchemaName}].[Process_WEB3]'))
BEGIN
    CREATE CLUSTERED INDEX IX_Process_WEB3_ProcessID
    ON [{SchemaName}].[Process_WEB3]([ProcessID]);
END
GO

-- Non-clustered index for TestID filtering
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Process_WEB3_TestID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Process_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Process_WEB3_TestID
    ON [{SchemaName}].[Process_WEB3]([TestID])
    INCLUDE ([ProcessName], [ProcessPosition], [WEB3Operator]);
END
GO

-- =============================================
-- Indexes for Function_WEB3
-- =============================================

-- Index on ProcessID for fast joins
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_Function_WEB3_ProcessID'
               AND object_id = OBJECT_ID('[{SchemaName}].[Function_WEB3]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Function_WEB3_ProcessID
    ON [{SchemaName}].[Function_WEB3]([ProcessID])
    INCLUDE ([FunctionPosition], [FunctionName]);
END
GO
```

---

## Option 2: Create Indexes for ALL Schemas (ADVANCED)

**This script automatically creates indexes for SeleniumDB AND PRODUCTION_Selenium**

```sql
-- =============================================
-- Create Indexes for ALL Schemas Automatically
-- =============================================

DECLARE @SchemaName NVARCHAR(128)
DECLARE @SQL NVARCHAR(MAX)

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

    -- Primary key index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'PK_Test_WEB3_Id'
                   AND object_id = OBJECT_ID(@SchemaName + '.Test_WEB3'))
    BEGIN
        SET @SQL = 'ALTER TABLE [' + @SchemaName + '].[Test_WEB3]
                    ADD CONSTRAINT PK_Test_WEB3_Id_' + @SchemaName + '
                    PRIMARY KEY CLUSTERED ([Id])'
        PRINT 'Creating PK on ' + @SchemaName + '.Test_WEB3'
        EXEC sp_executesql @SQL
    END

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

    -- =============================================
    -- Process_WEB3 Indexes
    -- =============================================

    -- ProcessID clustered index
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Process_WEB3_ProcessID'
                   AND type = 1
                   AND object_id = OBJECT_ID(@SchemaName + '.Process_WEB3'))
    BEGIN
        SET @SQL = 'CREATE CLUSTERED INDEX IX_Process_WEB3_ProcessID
                    ON [' + @SchemaName + '].[Process_WEB3]([ProcessID])'
        PRINT 'Creating IX_Process_WEB3_ProcessID on ' + @SchemaName + '.Process_WEB3'
        EXEC sp_executesql @SQL
    END

    -- TestID index for filtering
    IF NOT EXISTS (SELECT * FROM sys.indexes
                   WHERE name = 'IX_Process_WEB3_TestID'
                   AND object_id = OBJECT_ID(@SchemaName + '.Process_WEB3'))
    BEGIN
        SET @SQL = 'CREATE NONCLUSTERED INDEX IX_Process_WEB3_TestID
                    ON [' + @SchemaName + '].[Process_WEB3]([TestID])
                    INCLUDE ([ProcessName], [ProcessPosition], [WEB3Operator])'
        PRINT 'Creating IX_Process_WEB3_TestID on ' + @SchemaName + '.Process_WEB3'
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

## Quick Reference: Which Option to Use?

### Use Option 1 if:
- ✅ You want to create indexes for ONE schema at a time
- ✅ You want full control over which schema gets indexes
- ✅ You're comfortable doing find/replace

**Steps:**
1. Copy Option 1 script
2. Replace `{SchemaName}` with `SeleniumDB`
3. Run the script
4. Repeat for `PRODUCTION_Selenium` if needed

### Use Option 2 if:
- ✅ You want to create indexes for ALL schemas at once
- ✅ You have multiple schemas and want to automate
- ✅ You want future schemas to be handled automatically

**Steps:**
1. Copy Option 2 script
2. Run it once
3. Done! Indexes created for all schemas

---

## Verify Indexes Were Created

After running either script, verify indexes exist:

```sql
-- Check indexes for SeleniumDB schema
SELECT
    OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType
FROM sys.indexes i
WHERE OBJECT_SCHEMA_NAME(i.object_id) IN ('SeleniumDB', 'PRODUCTION_Selenium')
  AND i.name IS NOT NULL
ORDER BY SchemaName, TableName, IndexName
```

Expected output:
```
SchemaName          TableName       IndexName                       IndexType
-----------         -----------     -----------                     ---------
SeleniumDB          Test_WEB3       PK_Test_WEB3_Id                CLUSTERED
SeleniumDB          Test_WEB3       IX_Test_WEB3_TestID            NONCLUSTERED
SeleniumDB          Process_WEB3    IX_Process_WEB3_ProcessID      CLUSTERED
SeleniumDB          Process_WEB3    IX_Process_WEB3_TestID         NONCLUSTERED
SeleniumDB          Function_WEB3   IX_Function_WEB3_ProcessID     NONCLUSTERED
```

---

## Troubleshooting

### Error: "Cannot find the object"
**Cause:** Schema name not included or wrong schema
**Fix:** Use Option 2 (automatic) or verify schema name in Option 1

### Error: "There is already an object named 'PK_Test_WEB3_Id'"
**Cause:** Primary key already exists
**Solution:** This is OK! The IF NOT EXISTS check prevents duplicates. Script will skip it.

### Error: "Cannot create more than one clustered index"
**Cause:** Table already has a clustered index
**Solution:** Check existing indexes first:
```sql
SELECT name, type_desc
FROM sys.indexes
WHERE object_id = OBJECT_ID('[SeleniumDB].[Process_WEB3]')
  AND type_desc = 'CLUSTERED'
```
If one exists, comment out that CREATE CLUSTERED INDEX line.
