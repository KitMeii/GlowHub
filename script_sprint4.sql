-- ================================================================
-- 28/05/2026
--  GlowHub Sprint 4 — SQL Migration
--  Chạy file này trong SSMS sau khi deploy Sprint 4
-- ================================================================

USE BaseCoreDB
GO

-- ── 1. Cập nhật bảng Reviews ──────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SellerReply' AND Object_ID = OBJECT_ID('Reviews'))
    ALTER TABLE [dbo].[Reviews] ADD [SellerReply] NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ReplyAt' AND Object_ID = OBJECT_ID('Reviews'))
    ALTER TABLE [dbo].[Reviews] ADD [ReplyAt] DATETIME2 NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Images' AND Object_ID = OBJECT_ID('Reviews'))
    ALTER TABLE [dbo].[Reviews] ADD [Images] NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'IsVerifiedPurchase' AND Object_ID = OBJECT_ID('Reviews'))
    ALTER TABLE [dbo].[Reviews] ADD [IsVerifiedPurchase] BIT NOT NULL DEFAULT 0;

-- ── 2. Tạo bảng QnA ──────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE Name = 'QnA' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[QnA] (
        [Id]          INT           IDENTITY(1,1) NOT NULL,
        [ProductId]   INT           NOT NULL,
        [CustomerId]  NVARCHAR(450) NOT NULL,
        [Question]    NVARCHAR(1000) NOT NULL,
        [AskedAt]     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        [Answer]      NVARCHAR(2000) NULL,
        [AnsweredAt]  DATETIME2     NULL,
        [IsActive]    BIT           NOT NULL DEFAULT 1,
        CONSTRAINT [PK_QnA]           PRIMARY KEY ([Id]),
        CONSTRAINT [FK_QnA_Products]  FOREIGN KEY ([ProductId])  REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_QnA_Users]     FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Users]([Id])
    );
    PRINT 'Created table QnA';
END
ELSE
    PRINT 'Table QnA already exists';

-- ── 3. Tạo bảng Notifications ────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE Name = 'Notifications' AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Notifications] (
        [Id]        INT           IDENTITY(1,1) NOT NULL,
        [UserId]    NVARCHAR(450) NOT NULL,
        [Type]      INT           NOT NULL DEFAULT 0,
        [Title]     NVARCHAR(200) NOT NULL,
        [Message]   NVARCHAR(500) NOT NULL,
        [IsRead]    BIT           NOT NULL DEFAULT 0,
        [Link]      NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_Notifications]        PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users]  FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Notifications_UserId_IsRead] ON [dbo].[Notifications] ([UserId], [IsRead]);
    PRINT 'Created table Notifications';
END
ELSE
    PRINT 'Table Notifications already exists';

PRINT 'Sprint 4 migration completed.';
