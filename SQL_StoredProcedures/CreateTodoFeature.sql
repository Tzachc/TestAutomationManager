/*
==============================================================================
CREATE TODO FEATURE - Database Migration Script
==============================================================================
Purpose: Creates Todo and StickyNote tables for task management feature
         Includes Kanban board functionality and drag-drop sticky notes

Author:  Test Automation Manager
Date:    2025-11-18
==============================================================================
*/

USE [TestAutomationManager]
GO

-- ============================================
-- STEP 1: Create Todo Table
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PRODUCTION_Selenium].[Todo]') AND type in (N'U'))
BEGIN
    CREATE TABLE [PRODUCTION_Selenium].[Todo]
    (
        -- Primary Key
        [Id] INT PRIMARY KEY IDENTITY(1,1),

        -- Core Fields
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [DueDate] DATETIME NULL,
        [Priority] NVARCHAR(20) DEFAULT 'Medium' NOT NULL,         -- High, Medium, Low
        [Status] NVARCHAR(20) DEFAULT 'Todo' NOT NULL,             -- Todo, InProgress, Done

        -- Organization
        [Category] NVARCHAR(100) NULL,
        [Tags] NVARCHAR(500) NULL,                                 -- Comma-separated tags
        [Color] NVARCHAR(20) NULL,                                 -- Hex color for visual grouping

        -- Status Tracking
        [IsCompleted] BIT DEFAULT 0 NOT NULL,
        [SortOrder] INT DEFAULT 0 NOT NULL,                        -- For Kanban board ordering

        -- Time Tracking
        [EstimatedMinutes] INT NULL,
        [ActualMinutes] INT NULL,

        -- Recurring Tasks
        [IsRecurring] BIT DEFAULT 0 NOT NULL,
        [RecurrencePattern] NVARCHAR(50) NULL,                     -- Daily, Weekly, Monthly

        -- Attachments
        [AttachmentUrl] NVARCHAR(500) NULL,

        -- Audit Fields
        [CreatedAt] DATETIME DEFAULT GETDATE() NOT NULL,
        [UpdatedAt] DATETIME DEFAULT GETDATE() NOT NULL,
        [CreatedBy] NVARCHAR(100) NULL,

        -- Constraints
        CONSTRAINT [CK_Todo_Priority] CHECK ([Priority] IN ('High', 'Medium', 'Low')),
        CONSTRAINT [CK_Todo_Status] CHECK ([Status] IN ('Todo', 'InProgress', 'Done')),
        CONSTRAINT [CK_Todo_RecurrencePattern] CHECK ([RecurrencePattern] IN ('Daily', 'Weekly', 'Monthly', 'Yearly', NULL))
    )

    PRINT '✓ Todo table created successfully'
END
ELSE
BEGIN
    PRINT '⚠ Todo table already exists, skipping creation'
END
GO

-- ============================================
-- STEP 2: Create Indexes for Todo Table
-- ============================================

-- Index for Status queries (Kanban board columns)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Todo_Status' AND object_id = OBJECT_ID('[PRODUCTION_Selenium].[Todo]'))
BEGIN
    CREATE INDEX [IX_Todo_Status] ON [PRODUCTION_Selenium].[Todo]([Status])
    PRINT '✓ Index IX_Todo_Status created'
END

-- Index for DueDate queries (filtering overdue/upcoming tasks)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Todo_DueDate' AND object_id = OBJECT_ID('[PRODUCTION_Selenium].[Todo]'))
BEGIN
    CREATE INDEX [IX_Todo_DueDate] ON [PRODUCTION_Selenium].[Todo]([DueDate])
    PRINT '✓ Index IX_Todo_DueDate created'
END

-- Index for IsCompleted queries (active vs completed)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Todo_IsCompleted' AND object_id = OBJECT_ID('[PRODUCTION_Selenium].[Todo]'))
BEGIN
    CREATE INDEX [IX_Todo_IsCompleted] ON [PRODUCTION_Selenium].[Todo]([IsCompleted])
    PRINT '✓ Index IX_Todo_IsCompleted created'
END

-- Composite index for Status + SortOrder (optimal for Kanban board)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Todo_Status_SortOrder' AND object_id = OBJECT_ID('[PRODUCTION_Selenium].[Todo]'))
BEGIN
    CREATE INDEX [IX_Todo_Status_SortOrder] ON [PRODUCTION_Selenium].[Todo]([Status], [SortOrder])
    PRINT '✓ Index IX_Todo_Status_SortOrder created'
END

-- Index for Category queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Todo_Category' AND object_id = OBJECT_ID('[PRODUCTION_Selenium].[Todo]'))
BEGIN
    CREATE INDEX [IX_Todo_Category] ON [PRODUCTION_Selenium].[Todo]([Category])
    PRINT '✓ Index IX_Todo_Category created'
END
GO

-- ============================================
-- STEP 3: Create StickyNote Table
-- ============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PRODUCTION_Selenium].[StickyNote]') AND type in (N'U'))
BEGIN
    CREATE TABLE [PRODUCTION_Selenium].[StickyNote]
    (
        -- Primary Key
        [Id] INT PRIMARY KEY IDENTITY(1,1),

        -- Content
        [Content] NVARCHAR(2000) NULL,

        -- Appearance
        [Color] NVARCHAR(20) DEFAULT '#FFE57F' NOT NULL,           -- Hex color (yellow default)

        -- Position & Size (for drag-drop functionality)
        [PositionX] FLOAT DEFAULT 100.0 NOT NULL,
        [PositionY] FLOAT DEFAULT 100.0 NOT NULL,
        [Width] INT DEFAULT 200 NOT NULL,
        [Height] INT DEFAULT 200 NOT NULL,

        -- Layering
        [ZIndex] INT DEFAULT 1 NOT NULL,                           -- For bring to front

        -- Features
        [IsPinned] BIT DEFAULT 0 NOT NULL,                         -- Pin to always show

        -- Audit Fields
        [CreatedAt] DATETIME DEFAULT GETDATE() NOT NULL,
        [UpdatedAt] DATETIME DEFAULT GETDATE() NOT NULL,
        [CreatedBy] NVARCHAR(100) NULL
    )

    PRINT '✓ StickyNote table created successfully'
END
ELSE
BEGIN
    PRINT '⚠ StickyNote table already exists, skipping creation'
END
GO

-- ============================================
-- STEP 4: Create Indexes for StickyNote Table
-- ============================================

-- Index for ZIndex queries (layering order)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_StickyNote_ZIndex' AND object_id = OBJECT_ID('[PRODUCTION_Selenium].[StickyNote]'))
BEGIN
    CREATE INDEX [IX_StickyNote_ZIndex] ON [PRODUCTION_Selenium].[StickyNote]([ZIndex])
    PRINT '✓ Index IX_StickyNote_ZIndex created'
END
GO

-- ============================================
-- STEP 5: Insert Sample Data (Optional)
-- ============================================

-- Sample Todos
IF NOT EXISTS (SELECT * FROM [PRODUCTION_Selenium].[Todo])
BEGIN
    INSERT INTO [PRODUCTION_Selenium].[Todo]
    ([Title], [Description], [DueDate], [Priority], [Status], [Category], [Tags], [Color], [IsCompleted], [SortOrder], [EstimatedMinutes], [CreatedBy])
    VALUES
    (
        'Welcome to Todo Manager',
        'This is your first todo item! You can organize tasks using the Kanban board, set priorities, track time, and more.',
        DATEADD(DAY, 7, GETDATE()),
        'Medium',
        'Todo',
        'Getting Started',
        'welcome,tutorial',
        '#539BF5',
        0,
        0,
        30,
        'System'
    ),
    (
        'Try Drag and Drop',
        'Drag this task to the "In Progress" or "Done" column to see the Kanban board in action!',
        DATEADD(DAY, 3, GETDATE()),
        'High',
        'Todo',
        'Getting Started',
        'tutorial,kanban',
        '#EF5350',
        0,
        1,
        15,
        'System'
    ),
    (
        'Create Sticky Notes',
        'Click the sticky note button to create quick notes that you can position anywhere on your screen.',
        DATEADD(DAY, 5, GETDATE()),
        'Low',
        'Todo',
        'Getting Started',
        'tutorial,notes',
        '#66BB6A',
        0,
        2,
        10,
        'System'
    )

    PRINT '✓ Sample todo items inserted'
END
GO

-- Sample Sticky Notes
IF NOT EXISTS (SELECT * FROM [PRODUCTION_Selenium].[StickyNote])
BEGIN
    INSERT INTO [PRODUCTION_Selenium].[StickyNote]
    ([Content], [Color], [PositionX], [PositionY], [Width], [Height], [ZIndex], [IsPinned], [CreatedBy])
    VALUES
    (
        'Welcome to Sticky Notes! 📌

Drag me around the screen!
Double-click to edit.
Right-click for options.',
        '#FFE57F',
        150.0,
        150.0,
        220,
        180,
        1,
        1,
        'System'
    ),
    (
        '💡 Quick Tip:

Use different colors to organize your notes by category!',
        '#A7FFEB',
        400.0,
        150.0,
        200,
        150,
        2,
        0,
        'System'
    )

    PRINT '✓ Sample sticky notes inserted'
END
GO

-- ============================================
-- STEP 6: Create Trigger for UpdatedAt
-- ============================================

-- Trigger for Todo table
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_Todo_UpdatedAt')
    DROP TRIGGER [PRODUCTION_Selenium].[TR_Todo_UpdatedAt]
GO

CREATE TRIGGER [PRODUCTION_Selenium].[TR_Todo_UpdatedAt]
ON [PRODUCTION_Selenium].[Todo]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [PRODUCTION_Selenium].[Todo]
    SET [UpdatedAt] = GETDATE()
    FROM [PRODUCTION_Selenium].[Todo] t
    INNER JOIN inserted i ON t.Id = i.Id
END
GO
PRINT '✓ Trigger TR_Todo_UpdatedAt created'

-- Trigger for StickyNote table
IF EXISTS (SELECT * FROM sys.triggers WHERE name = 'TR_StickyNote_UpdatedAt')
    DROP TRIGGER [PRODUCTION_Selenium].[TR_StickyNote_UpdatedAt]
GO

CREATE TRIGGER [PRODUCTION_Selenium].[TR_StickyNote_UpdatedAt]
ON [PRODUCTION_Selenium].[StickyNote]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [PRODUCTION_Selenium].[StickyNote]
    SET [UpdatedAt] = GETDATE()
    FROM [PRODUCTION_Selenium].[StickyNote] n
    INNER JOIN inserted i ON n.Id = i.Id
END
GO
PRINT '✓ Trigger TR_StickyNote_UpdatedAt created'

-- ============================================
-- STEP 7: Verification
-- ============================================

PRINT ''
PRINT '============================================'
PRINT 'TODO FEATURE SETUP VERIFICATION'
PRINT '============================================'

-- Check Todo table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PRODUCTION_Selenium].[Todo]') AND type in (N'U'))
BEGIN
    DECLARE @TodoCount INT
    SELECT @TodoCount = COUNT(*) FROM [PRODUCTION_Selenium].[Todo]
    PRINT '✓ Todo table exists (' + CAST(@TodoCount AS VARCHAR) + ' records)'
END
ELSE
    PRINT '✗ Todo table NOT found!'

-- Check StickyNote table
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PRODUCTION_Selenium].[StickyNote]') AND type in (N'U'))
BEGIN
    DECLARE @NoteCount INT
    SELECT @NoteCount = COUNT(*) FROM [PRODUCTION_Selenium].[StickyNote]
    PRINT '✓ StickyNote table exists (' + CAST(@NoteCount AS VARCHAR) + ' records)'
END
ELSE
    PRINT '✗ StickyNote table NOT found!'

-- Check indexes
DECLARE @IndexCount INT
SELECT @IndexCount = COUNT(*)
FROM sys.indexes
WHERE object_id IN (OBJECT_ID('[PRODUCTION_Selenium].[Todo]'), OBJECT_ID('[PRODUCTION_Selenium].[StickyNote]'))
AND name IS NOT NULL
AND is_primary_key = 0

PRINT '✓ ' + CAST(@IndexCount AS VARCHAR) + ' indexes created'

-- Check triggers
DECLARE @TriggerCount INT
SELECT @TriggerCount = COUNT(*)
FROM sys.triggers
WHERE name IN ('TR_Todo_UpdatedAt', 'TR_StickyNote_UpdatedAt')

PRINT '✓ ' + CAST(@TriggerCount AS VARCHAR) + ' triggers created'

PRINT ''
PRINT '============================================'
PRINT 'TODO FEATURE SETUP COMPLETE!'
PRINT '============================================'
PRINT ''
PRINT 'Next Steps:'
PRINT '1. Launch the Test Automation Manager application'
PRINT '2. Navigate to the Todo section in the sidebar'
PRINT '3. Start organizing your tasks with the Kanban board'
PRINT '4. Create sticky notes for quick reminders'
PRINT ''
PRINT 'Features Available:'
PRINT '• Kanban Board (Todo, In Progress, Done columns)'
PRINT '• Drag & Drop task organization'
PRINT '• Priority levels (High, Medium, Low)'
PRINT '• Due dates and overdue tracking'
PRINT '• Categories and tags'
PRINT '• Time tracking (estimated vs actual)'
PRINT '• Sticky notes with drag & drop positioning'
PRINT '• Color coding for visual organization'
PRINT '• Full history logging'
PRINT ''

GO
