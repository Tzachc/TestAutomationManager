-- =============================================
-- HISTORY LOG MAINTENANCE SCRIPT
-- Description: Clean up old history entries to prevent unlimited table growth
--
-- RECOMMENDATIONS:
-- - Run this script monthly or quarterly as part of database maintenance
-- - Default: Keeps 90 days of history (adjust @DaysToKeep as needed)
-- - For production: Consider 180-365 days retention
-- - For development: 30-90 days is sufficient
--
-- GROWTH ESTIMATION:
-- - Typical usage: ~10-50 history entries per day
-- - 90 days: ~900-4,500 rows (negligible impact)
-- - 1 year: ~3,600-18,000 rows (still very manageable)
-- - Even 5 years of data would only be ~18K-90K rows
-- =============================================

PRINT '========================================';
PRINT 'HISTORY LOG CLEANUP - Starting...';
PRINT '========================================';
PRINT '';

-- Configure retention period (days)
DECLARE @DaysToKeep INT = 90;  -- Change this value as needed
DECLARE @CutoffDate DATETIME2 = DATEADD(DAY, -@DaysToKeep, GETDATE());

PRINT 'Configuration:';
PRINT '  Retention Period: ' + CAST(@DaysToKeep AS VARCHAR(10)) + ' days';
PRINT '  Cutoff Date: ' + CONVERT(VARCHAR(20), @CutoffDate, 120);
PRINT '';

-- =============================================
-- COUNT ENTRIES TO BE DELETED
-- =============================================

DECLARE @SeleniumDBCount INT;
DECLARE @ProductionCount INT;

SELECT @SeleniumDBCount = COUNT(*)
FROM [SeleniumDB].[HistoryLog]
WHERE [ChangedAt] < @CutoffDate;

SELECT @ProductionCount = COUNT(*)
FROM [PRODUCTION_Selenium].[HistoryLog]
WHERE [ChangedAt] < @CutoffDate;

PRINT 'Entries to be deleted:';
PRINT '  SeleniumDB: ' + CAST(@SeleniumDBCount AS VARCHAR(10)) + ' rows';
PRINT '  PRODUCTION_Selenium: ' + CAST(@ProductionCount AS VARCHAR(10)) + ' rows';
PRINT '';

-- =============================================
-- PERFORM CLEANUP
-- =============================================

IF @SeleniumDBCount > 0 OR @ProductionCount > 0
BEGIN
    PRINT 'Deleting old entries...';

    -- Delete from SeleniumDB
    DELETE FROM [SeleniumDB].[HistoryLog]
    WHERE [ChangedAt] < @CutoffDate;

    PRINT '  ✓ Deleted ' + CAST(@SeleniumDBCount AS VARCHAR(10)) + ' rows from SeleniumDB';

    -- Delete from PRODUCTION_Selenium
    DELETE FROM [PRODUCTION_Selenium].[HistoryLog]
    WHERE [ChangedAt] < @CutoffDate;

    PRINT '  ✓ Deleted ' + CAST(@ProductionCount AS VARCHAR(10)) + ' rows from PRODUCTION_Selenium';
    PRINT '';
END
ELSE
BEGIN
    PRINT 'No old entries to delete.';
    PRINT '';
END

-- =============================================
-- SHOW CURRENT TABLE STATISTICS
-- =============================================

PRINT 'Current table statistics:';
PRINT '';

-- SeleniumDB statistics
DECLARE @TotalRows INT;
DECLARE @OldestEntry DATETIME2;
DECLARE @NewestEntry DATETIME2;

SELECT
    @TotalRows = COUNT(*),
    @OldestEntry = MIN([ChangedAt]),
    @NewestEntry = MAX([ChangedAt])
FROM [SeleniumDB].[HistoryLog];

PRINT 'SeleniumDB.HistoryLog:';
PRINT '  Total Rows: ' + CAST(@TotalRows AS VARCHAR(10));
IF @OldestEntry IS NOT NULL
BEGIN
    PRINT '  Oldest Entry: ' + CONVERT(VARCHAR(20), @OldestEntry, 120);
    PRINT '  Newest Entry: ' + CONVERT(VARCHAR(20), @NewestEntry, 120);
    PRINT '  Date Range: ' + CAST(DATEDIFF(DAY, @OldestEntry, @NewestEntry) AS VARCHAR(10)) + ' days';
END
ELSE
BEGIN
    PRINT '  (Table is empty)';
END
PRINT '';

-- PRODUCTION_Selenium statistics
SELECT
    @TotalRows = COUNT(*),
    @OldestEntry = MIN([ChangedAt]),
    @NewestEntry = MAX([ChangedAt])
FROM [PRODUCTION_Selenium].[HistoryLog];

PRINT 'PRODUCTION_Selenium.HistoryLog:';
PRINT '  Total Rows: ' + CAST(@TotalRows AS VARCHAR(10));
IF @OldestEntry IS NOT NULL
BEGIN
    PRINT '  Oldest Entry: ' + CONVERT(VARCHAR(20), @OldestEntry, 120);
    PRINT '  Newest Entry: ' + CONVERT(VARCHAR(20), @NewestEntry, 120);
    PRINT '  Date Range: ' + CAST(DATEDIFF(DAY, @OldestEntry, @NewestEntry) AS VARCHAR(10)) + ' days';
END
ELSE
BEGIN
    PRINT '  (Table is empty)';
END
PRINT '';

PRINT '========================================';
PRINT 'HISTORY LOG CLEANUP - COMPLETE! ✓';
PRINT '========================================';
PRINT '';
PRINT 'Recommendations:';
PRINT '  • Run this script monthly or quarterly';
PRINT '  • Adjust @DaysToKeep based on your needs';
PRINT '  • Monitor table size with VerifyHistorySetup.sql';
PRINT '';
