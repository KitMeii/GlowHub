-- ============================================================
-- 29/05/2026
-- Script Sprint 7: Flash Sale + Voucher Nâng Cao + Compare + UX
-- Chạy nhiều lần không lỗi (IF NOT EXISTS / IF COL_LENGTH)
-- ============================================================

USE BaseCoreDB
GO

-- ─────────────────────────────────────────────────────────────
-- 1. Products — thêm cột ViewCount
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ViewCount' AND Object_ID = OBJECT_ID('Products'))
    ALTER TABLE [dbo].[Products] ADD [ViewCount] INT NOT NULL DEFAULT 0;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. FlashSales
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FlashSales]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[FlashSales] (
        [Id]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name]      NVARCHAR(200)     NOT NULL,
        [StartTime] DATETIME2(7)      NOT NULL,
        [EndTime]   DATETIME2(7)      NOT NULL,
        [IsActive]  BIT               NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(450)     NULL
    );
END
GO

-- ─────────────────────────────────────────────────────────────
-- 3. FlashSaleProducts
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FlashSaleProducts]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[FlashSaleProducts] (
        [Id]            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [FlashSaleId]   INT               NOT NULL,
        [ProductId]     INT               NOT NULL,
        [SalePrice]     DECIMAL(18,2)     NOT NULL,
        [OriginalPrice] DECIMAL(18,2)     NOT NULL,
        [Quantity]      INT               NOT NULL DEFAULT 0,
        [SoldCount]     INT               NOT NULL DEFAULT 0,
        [IsActive]      BIT               NOT NULL DEFAULT 1,
        CONSTRAINT [FK_FlashSaleProducts_FlashSales]
            FOREIGN KEY ([FlashSaleId]) REFERENCES [dbo].[FlashSales]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_FlashSaleProducts_Products]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE
    );
END
GO

-- Index FlashSaleProducts
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE Name = 'IX_FlashSaleProducts_FlashSaleId' AND object_id = OBJECT_ID('FlashSaleProducts'))
    CREATE INDEX [IX_FlashSaleProducts_FlashSaleId] ON [dbo].[FlashSaleProducts] ([FlashSaleId]);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE Name = 'IX_FlashSaleProducts_ProductId' AND object_id = OBJECT_ID('FlashSaleProducts'))
    CREATE INDEX [IX_FlashSaleProducts_ProductId] ON [dbo].[FlashSaleProducts] ([ProductId]);
GO

-- ─────────────────────────────────────────────────────────────
-- 4. RecentlyViewed
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RecentlyViewed]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[RecentlyViewed] (
        [Id]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId]    NVARCHAR(450)     NOT NULL,
        [ProductId] INT               NOT NULL,
        [ViewedAt]  DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_RecentlyViewed_Users]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RecentlyViewed_Products]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE Name = 'IX_RecentlyViewed_UserId_ViewedAt' AND object_id = OBJECT_ID('RecentlyViewed'))
    CREATE INDEX [IX_RecentlyViewed_UserId_ViewedAt] ON [dbo].[RecentlyViewed] ([UserId], [ViewedAt] DESC);
GO

-- ─────────────────────────────────────────────────────────────
-- 5. CustomerVouchers
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CustomerVouchers]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[CustomerVouchers] (
        [Id]        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserId]    NVARCHAR(450)     NOT NULL,
        [VoucherId] INT               NOT NULL,
        [SavedAt]   DATETIME2(7)      NOT NULL DEFAULT GETUTCDATE(),
        [IsUsed]    BIT               NOT NULL DEFAULT 0,
        CONSTRAINT [FK_CustomerVouchers_Users]
            FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CustomerVouchers_Vouchers]
            FOREIGN KEY ([VoucherId]) REFERENCES [dbo].[Vouchers]([Id]) ON DELETE CASCADE
    );
END
GO

-- Unique index: 1 user chỉ lưu 1 voucher
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE Name = 'UX_CustomerVouchers_UserId_VoucherId' AND object_id = OBJECT_ID('CustomerVouchers'))
    CREATE UNIQUE INDEX [UX_CustomerVouchers_UserId_VoucherId] ON [dbo].[CustomerVouchers] ([UserId], [VoucherId]);
GO

-- ─────────────────────────────────────────────────────────────
-- 6. Seed Data — Flash Sale Demo
-- ─────────────────────────────────────────────────────────────

-- Flash Sale đang diễn ra (24 giờ tới)
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[FlashSales] WHERE [Name] = N'Flash Sale Cuối Tuần')
BEGIN
    INSERT INTO [dbo].[FlashSales] ([Name], [StartTime], [EndTime], [IsActive], [CreatedAt])
    VALUES
    (
        N'Flash Sale Cuối Tuần',
        DATEADD(HOUR, -1, GETUTCDATE()),   -- bắt đầu 1 giờ trước
        DATEADD(HOUR, 23, GETUTCDATE()),   -- kết thúc sau 23 giờ
        1,
        GETUTCDATE()
    );

    DECLARE @saleId INT = SCOPE_IDENTITY();

    -- Thêm 4 sản phẩm đầu tiên vào flash sale (nếu có)
    INSERT INTO [dbo].[FlashSaleProducts] ([FlashSaleId], [ProductId], [SalePrice], [OriginalPrice], [Quantity], [SoldCount], [IsActive])
    SELECT TOP 4
        @saleId,
        p.[Id],
        ROUND(p.[Price] * 0.7, 0),    -- giảm 30%
        p.[Price],
        50,
        CAST(RAND(CHECKSUM(NEWID())) * 35 AS INT),
        1
    FROM [dbo].[Products] p
    WHERE p.[IsActive] = 1
    ORDER BY NEWID();
END
GO

-- Flash Sale sắp diễn ra (ngày mai)
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[FlashSales] WHERE [Name] = N'Flash Sale Thứ Hai')
BEGIN
    INSERT INTO [dbo].[FlashSales] ([Name], [StartTime], [EndTime], [IsActive], [CreatedAt])
    VALUES
    (
        N'Flash Sale Thứ Hai',
        DATEADD(HOUR, 6, GETUTCDATE()),    -- bắt đầu sau 6 giờ
        DATEADD(HOUR, 18, GETUTCDATE()),   -- kéo dài 12 giờ
        1,
        GETUTCDATE()
    );
END
GO

-- ─────────────────────────────────────────────────────────────
-- 7. Seed Data — Vouchers bổ sung
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Vouchers] WHERE [Code] = 'GLOW20')
    INSERT INTO [dbo].[Vouchers] ([Code], [Description], [DiscountType], [DiscountValue], [MaxDiscount], [MinOrderAmount], [UsageLimit], [UsedCount], [IsActive], [CreatedAt])
    VALUES ('GLOW20', N'Giảm 20% tối đa 100.000₫', 'percent', 20, 100000, 300000, 200, 0, 1, GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Vouchers] WHERE [Code] = 'BEAUTY50K')
    INSERT INTO [dbo].[Vouchers] ([Code], [Description], [DiscountType], [DiscountValue], [MaxDiscount], [MinOrderAmount], [UsageLimit], [UsedCount], [IsActive], [CreatedAt])
    VALUES ('BEAUTY50K', N'Giảm thẳng 50.000₫', 'fixed', 50000, NULL, 200000, 500, 0, 1, GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[Vouchers] WHERE [Code] = 'NEWBIE15')
    INSERT INTO [dbo].[Vouchers] ([Code], [Description], [DiscountType], [DiscountValue], [MaxDiscount], [MinOrderAmount], [UsageLimit], [UsedCount], [IsActive], [CreatedAt])
    VALUES ('NEWBIE15', N'Khách hàng mới — Giảm 15%', 'percent', 15, 80000, 150000, 100, 0, 1, GETUTCDATE());
GO

-- ─────────────────────────────────────────────────────────────
-- 8. Check kết quả
-- ─────────────────────────────────────────────────────────────
SELECT 'FlashSales'        AS TableName, COUNT(*) AS Rows FROM [dbo].[FlashSales]        UNION ALL
SELECT 'FlashSaleProducts',             COUNT(*)            FROM [dbo].[FlashSaleProducts]  UNION ALL
SELECT 'RecentlyViewed',                COUNT(*)            FROM [dbo].[RecentlyViewed]      UNION ALL
SELECT 'CustomerVouchers',              COUNT(*)            FROM [dbo].[CustomerVouchers]    UNION ALL
SELECT 'Vouchers',                      COUNT(*)            FROM [dbo].[Vouchers];
GO
