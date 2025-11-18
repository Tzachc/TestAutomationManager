# History Feature - Database Maintenance Guide

## Table Growth Analysis

### Should you be worried about the HistoryLog table growing over time?

**Short answer: No, not significantly.**

### Growth Estimation

Based on typical usage patterns for a test automation system:

| Scenario | Daily Entries | 90 Days | 1 Year | 5 Years |
|----------|---------------|---------|--------|---------|
| Light usage (10 edits/day) | 10 | ~900 | ~3,600 | ~18,000 |
| Moderate usage (30 edits/day) | 30 | ~2,700 | ~11,000 | ~55,000 |
| Heavy usage (50 edits/day) | 50 | ~4,500 | ~18,000 | ~90,000 |

**Conclusion**: Even with heavy usage over 5 years, you'd only have ~90K rows, which is negligible for modern databases.

### Database Impact

- **Storage**: Each history entry is ~500-1000 bytes. Even 100K rows = only ~50-100 MB
- **Performance**: Indexed queries remain fast up to millions of rows
- **Indexes**: The table has two indexes for optimal query performance:
  - `IX_EntityType_EntityId_ChangedAt` - For entity-specific lookups
  - `IX_ChangedAt` - For time-based queries

## Maintenance Options

### Option 1: No Maintenance (Recommended for most users)
If you're fine keeping all history indefinitely:
- **No action required**
- Table will grow slowly over time
- Performance impact: negligible

### Option 2: Periodic Cleanup (Recommended for production)
If you want to limit table size:

1. **Run the cleanup script** (`MaintenanceCleanupHistory.sql`)
   - Frequency: Monthly or quarterly
   - Default retention: 90 days
   - Adjust `@DaysToKeep` variable as needed

2. **Retention recommendations**:
   - **Development/Test**: 30-90 days
   - **Production**: 180-365 days
   - **Compliance/Audit**: Keep as long as required by policy

### Option 3: Automated Cleanup (Advanced)
Create a SQL Server Agent job to run the cleanup automatically:

```sql
-- Example: Create monthly cleanup job
USE msdb;
GO

EXEC dbo.sp_add_job
    @job_name = N'Cleanup History Log - Monthly';

EXEC sp_add_jobstep
    @job_name = N'Cleanup History Log - Monthly',
    @step_name = N'Delete old entries',
    @command = N'EXEC sp_executesql N''
        DECLARE @DaysToKeep INT = 90;
        DELETE FROM [SeleniumDB].[HistoryLog] WHERE [ChangedAt] < DATEADD(DAY, -@DaysToKeep, GETDATE());
        DELETE FROM [PRODUCTION_Selenium].[HistoryLog] WHERE [ChangedAt] < DATEADD(DAY, -@DaysToKeep, GETDATE());
    ''';

EXEC sp_add_schedule
    @schedule_name = N'Monthly',
    @freq_type = 16, -- Monthly
    @freq_interval = 1; -- First day of month

EXEC sp_attach_schedule
    @job_name = N'Cleanup History Log - Monthly',
    @schedule_name = N'Monthly';
```

## Monitoring Table Size

Use the verification script to check current status:

```bash
# Run this in SQL Server Management Studio
SQL_StoredProcedures/VerifyHistorySetup.sql
```

Or query directly:

```sql
-- Check row count
SELECT COUNT(*) AS TotalRows FROM [SeleniumDB].[HistoryLog];

-- Check date range
SELECT
    MIN(ChangedAt) AS OldestEntry,
    MAX(ChangedAt) AS NewestEntry,
    DATEDIFF(DAY, MIN(ChangedAt), MAX(ChangedAt)) AS DaysOfHistory
FROM [SeleniumDB].[HistoryLog];

-- Check table size
EXEC sp_spaceused '[SeleniumDB].[HistoryLog]';
```

## Best Practices

1. **Don't over-clean**: Keep at least 30-90 days for troubleshooting
2. **Test first**: Run cleanup on dev/test environment before production
3. **Backup before cleanup**: Always backup before deleting data
4. **Monitor growth**: Check table size quarterly
5. **Adjust retention**: Increase if you need historical audit trails

## Summary

**You don't need to worry about table growth unless:**
- You're making 100+ edits per day
- You have strict storage constraints
- Company policy requires data retention limits

**For most users:**
- No maintenance needed
- Run cleanup annually if desired
- Table will remain performant even with years of data
