-- ============================================================
-- 29/05/2026
--  SPRINT 9 — Database Migration Script
--  Tables: AuditLogs, Disputes, PayoutHistories
-- ============================================================

USE BaseCoreDB
GO


-- 1. AuditLogs
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id]         INT           IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId]     NVARCHAR(450) NULL,
        [UserName]   NVARCHAR(256) NULL,
        [Action]     NVARCHAR(100) NOT NULL,
        [EntityType] NVARCHAR(100) NULL,
        [EntityId]   NVARCHAR(450) NULL,
        [OldValue]   NVARCHAR(MAX) NULL,
        [NewValue]   NVARCHAR(MAX) NULL,
        [IpAddress]  NVARCHAR(50)  NULL,
        [CreatedAt]  DATETIME2     NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Created table AuditLogs';
END
ELSE
    PRINT 'Table AuditLogs already exists';
GO

-- 2. PayoutHistories
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PayoutHistories')
BEGIN
    CREATE TABLE [dbo].[PayoutHistories] (
        [Id]          INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ShopId]      NVARCHAR(450)  NOT NULL,
        [Amount]      DECIMAL(18,2)  NOT NULL,
        [Note]        NVARCHAR(500)  NULL,
        [PayoutDate]  DATETIME2      NOT NULL,
        [ProcessedBy] NVARCHAR(450)  NULL,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_PayoutHistories_Shops] FOREIGN KEY ([ShopId])
            REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE
    );
    PRINT 'Created table PayoutHistories';
END
ELSE
    PRINT 'Table PayoutHistories already exists';
GO

-- 3. Disputes
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Disputes')
BEGIN
    CREATE TABLE [dbo].[Disputes] (
        [Id]            INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [OrderId]       INT            NOT NULL,
        [CustomerId]    NVARCHAR(450)  NOT NULL,
        [Reason]        NVARCHAR(200)  NOT NULL,
        [Description]   NVARCHAR(MAX)  NULL,
        [Evidence]      NVARCHAR(MAX)  NULL,
        [Status]        NVARCHAR(20)   NOT NULL DEFAULT 'OPEN',
        [Resolution]    NVARCHAR(MAX)  NULL,
        [RefundAmount]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [FavorCustomer] BIT            NOT NULL DEFAULT 0,
        [CreatedAt]     DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        [ResolvedAt]    DATETIME2      NULL,
        [ResolvedBy]    NVARCHAR(450)  NULL,
        CONSTRAINT [FK_Disputes_Orders] FOREIGN KEY ([OrderId])
            REFERENCES [dbo].[Orders]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Disputes_Users] FOREIGN KEY ([CustomerId])
            REFERENCES [dbo].[Users]([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table Disputes';
END
ELSE
    PRINT 'Table Disputes already exists';
GO

-- 4. Index on AuditLogs.CreatedAt for fast date-range queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AuditLogs_CreatedAt' AND object_id = OBJECT_ID('AuditLogs'))
BEGIN
    CREATE INDEX [IX_AuditLogs_CreatedAt] ON [dbo].[AuditLogs] ([CreatedAt] DESC);
    PRINT 'Created index IX_AuditLogs_CreatedAt';
END
GO

-- 5. Index on Disputes.Status for fast filter queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Disputes_Status' AND object_id = OBJECT_ID('Disputes'))
BEGIN
    CREATE INDEX [IX_Disputes_Status] ON [dbo].[Disputes] ([Status]);
    PRINT 'Created index IX_Disputes_Status';
END
GO

-- 6. Seed sample audit log entry (optional, for testing)
-- INSERT INTO [dbo].[AuditLogs] ([UserId],[UserName],[Action],[EntityType],[EntityId],[NewValue],[CreatedAt])
-- VALUES ('admin-id', 'admin', 'SYSTEM_INIT', 'System', '0', '{"init":"sprint9"}', GETUTCDATE());

PRINT 'Sprint 9 migration completed successfully.';
GO
