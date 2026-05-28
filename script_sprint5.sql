-- ================================================================
-- 28/05/2026
--  GlowHub Sprint 5 — SQL Migration
--  Chạy file này trong SSMS sau khi deploy Sprint 5
-- ================================================================

USE BaseCoreDB
GO

-- ── 1. Bảng Wishlist ──────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE Name = 'Wishlist' AND Type = 'U')
BEGIN
    CREATE TABLE [dbo].[Wishlist] (
        [Id]         INT            NOT NULL IDENTITY(1,1),
        [CustomerId] INT            NOT NULL,
        [ProductId]  INT            NOT NULL,
        [CreatedAt]  DATETIME2      NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_Wishlist] PRIMARY KEY ([Id]),

        CONSTRAINT [FK_Wishlist_Users]
            FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Users]([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_Wishlist_Products]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
            ON DELETE CASCADE,

        CONSTRAINT [UQ_Wishlist_Customer_Product]
            UNIQUE ([CustomerId], [ProductId])
    );

    PRINT 'Created table Wishlist';
END
ELSE
    PRINT 'Table Wishlist already exists — skipped';
GO

-- ── 2. Cột Products.Images ────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = 'Images' AND Object_ID = OBJECT_ID('Products'))
BEGIN
    ALTER TABLE [dbo].[Products]
        ADD [Images] NVARCHAR(2000) NULL;
    PRINT 'Added column Products.Images';
END
ELSE
    PRINT 'Column Products.Images already exists — skipped';
GO

-- ── 3. Cột Products.Specifications ───────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = 'Specifications' AND Object_ID = OBJECT_ID('Products'))
BEGIN
    ALTER TABLE [dbo].[Products]
        ADD [Specifications] NVARCHAR(2000) NULL;
    PRINT 'Added column Products.Specifications';
END
ELSE
    PRINT 'Column Products.Specifications already exists — skipped';
GO

-- ── 4. Cột Products.SoldCount ─────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE Name = 'SoldCount' AND Object_ID = OBJECT_ID('Products'))
BEGIN
    ALTER TABLE [dbo].[Products]
        ADD [SoldCount] INT NOT NULL DEFAULT 0;
    PRINT 'Added column Products.SoldCount';
END
ELSE
    PRINT 'Column Products.SoldCount already exists — skipped';
GO

PRINT '=== Sprint 5 migration complete ===';
GO
