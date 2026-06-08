-- ============================================================
-- 29/05/2026
--  SPRINT 10 — Database Migration Script
--  Fix Logic Tài Chính + Bảo Mật + Báo Cáo Tài Chính
--  Tables: ALTER Orders, SellerWallets, WalletTransactions,
--          ALTER Categories (soft delete)
-- ============================================================

USE BaseCoreDB
GO

-- ============================================================
-- 1. ALTER TABLE Orders — thêm các cột tài chính mới
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'ShopId')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [ShopId] NVARCHAR(450) NULL;
    PRINT 'Orders: Added column ShopId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'CommissionRate')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [CommissionRate] DECIMAL(5,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column CommissionRate';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'ProductRevenue')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [ProductRevenue] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column ProductRevenue';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'CommissionAmount')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [CommissionAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column CommissionAmount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'SellerPayoutAmount')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [SellerPayoutAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column SellerPayoutAmount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'ShopVoucherDiscount')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [ShopVoucherDiscount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column ShopVoucherDiscount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'SystemVoucherDiscount')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [SystemVoucherDiscount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column SystemVoucherDiscount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'FreeshipDiscount')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [FreeshipDiscount] DECIMAL(18,2) NOT NULL DEFAULT 0;
    PRINT 'Orders: Added column FreeshipDiscount';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'PayoutStatus')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [PayoutStatus] NVARCHAR(20) NOT NULL DEFAULT 'PENDING';
    PRINT 'Orders: Added column PayoutStatus';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'WalletReleaseAt')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [WalletReleaseAt] DATETIME2 NULL;
    PRINT 'Orders: Added column WalletReleaseAt';
END
GO

-- ============================================================
-- 2. ALTER TABLE Categories — soft delete
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Categories') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE [dbo].[Categories] ADD [IsDeleted] BIT NOT NULL DEFAULT 0;
    PRINT 'Categories: Added column IsDeleted';
END
GO

-- ============================================================
-- 3. SellerWallets — ví của từng shop
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SellerWallets')
BEGIN
    CREATE TABLE [dbo].[SellerWallets] (
        [ShopId]          NVARCHAR(450)  NOT NULL PRIMARY KEY,
        [Balance]         DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [TotalEarned]     DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [TotalWithdrawn]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [TotalRefunded]   DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [UpdatedAt]       DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_SellerWallets_Shops] FOREIGN KEY ([ShopId])
            REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE
    );
    PRINT 'Created table SellerWallets';
END
ELSE
    PRINT 'Table SellerWallets already exists';
GO

-- ============================================================
-- 4. WalletTransactions — lịch sử giao dịch ví
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WalletTransactions')
BEGIN
    CREATE TABLE [dbo].[WalletTransactions] (
        [Id]            INT            IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ShopId]        NVARCHAR(450)  NOT NULL,
        [OrderId]       INT            NULL,
        [Type]          NVARCHAR(20)   NOT NULL,   -- EARNING | REFUND | WITHDRAWAL | ADJUSTMENT
        [Amount]        DECIMAL(18,2)  NOT NULL,
        [BalanceBefore] DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [BalanceAfter]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
        [Note]          NVARCHAR(500)  NULL,
        [CreatedAt]     DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_WalletTx_Shops]  FOREIGN KEY ([ShopId])
            REFERENCES [dbo].[Shops]([Id])  ON DELETE CASCADE,
        CONSTRAINT [FK_WalletTx_Orders] FOREIGN KEY ([OrderId])
            REFERENCES [dbo].[Orders]([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table WalletTransactions';
END
ELSE
    PRINT 'Table WalletTransactions already exists';
GO

-- ============================================================
-- 5. Seed SellerWallets cho tất cả shop chưa có ví
-- ============================================================

INSERT INTO [dbo].[SellerWallets] ([ShopId], [Balance], [TotalEarned], [TotalWithdrawn], [TotalRefunded], [UpdatedAt])
SELECT s.[Id], 0, 0, 0, 0, GETUTCDATE()
FROM [dbo].[Shops] s
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[SellerWallets] sw WHERE sw.[ShopId] = s.[Id]
);
PRINT 'Seeded SellerWallets for existing shops';
GO

-- ============================================================
-- 6. Backfill Orders.ShopId từ OrderDetails → Products → Shops
-- ============================================================

UPDATE o
SET o.[ShopId] = (
    SELECT TOP 1 p.[ShopId]
    FROM [dbo].[OrderDetails] od
    JOIN [dbo].[Products] p ON p.[Id] = od.[ProductId]
    WHERE od.[OrderId] = o.[Id]
      AND p.[ShopId] IS NOT NULL
)
FROM [dbo].[Orders] o
WHERE o.[ShopId] IS NULL;

PRINT 'Backfilled Orders.ShopId';
GO

-- ============================================================
-- 7. Indexes
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_ShopId' AND object_id = OBJECT_ID('Orders'))
BEGIN
    CREATE INDEX [IX_Orders_ShopId] ON [dbo].[Orders] ([ShopId]);
    PRINT 'Created index IX_Orders_ShopId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Orders_PayoutStatus' AND object_id = OBJECT_ID('Orders'))
BEGIN
    CREATE INDEX [IX_Orders_PayoutStatus] ON [dbo].[Orders] ([PayoutStatus]);
    PRINT 'Created index IX_Orders_PayoutStatus';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WalletTx_ShopId' AND object_id = OBJECT_ID('WalletTransactions'))
BEGIN
    CREATE INDEX [IX_WalletTx_ShopId] ON [dbo].[WalletTransactions] ([ShopId], [CreatedAt] DESC);
    PRINT 'Created index IX_WalletTx_ShopId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WalletTx_OrderId' AND object_id = OBJECT_ID('WalletTransactions'))
BEGIN
    CREATE INDEX [IX_WalletTx_OrderId] ON [dbo].[WalletTransactions] ([OrderId]);
    PRINT 'Created index IX_WalletTx_OrderId';
END
GO

PRINT 'Sprint 10 migration completed successfully.';
GO
