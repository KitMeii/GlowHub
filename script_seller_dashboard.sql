-- ============================================================
-- 28/05/2026
-- MIGRATION: Seller Dashboard — Sprint 3
-- Run on existing BaseCoreDB after Sprint 2 migration
-- ============================================================

USE [BaseCoreDB]
GO

-- Products: add ShopId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ShopId')
BEGIN
    ALTER TABLE [dbo].[Products] ADD [ShopId] [nvarchar](450) NULL
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Products_Shops_ShopId')
BEGIN
    ALTER TABLE [dbo].[Products]
        ADD CONSTRAINT [FK_Products_Shops_ShopId]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE SET NULL
END
GO

-- Orders: add CancelReason + TrackingCode
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'CancelReason')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [CancelReason] [nvarchar](500) NULL
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Orders') AND name = 'TrackingCode')
BEGIN
    ALTER TABLE [dbo].[Orders] ADD [TrackingCode] [nvarchar](100) NULL
END
GO

-- Vouchers: add ShopId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Vouchers') AND name = 'ShopId')
BEGIN
    ALTER TABLE [dbo].[Vouchers] ADD [ShopId] [nvarchar](450) NULL
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Vouchers_Shops_ShopId')
BEGIN
    ALTER TABLE [dbo].[Vouchers]
        ADD CONSTRAINT [FK_Vouchers_Shops_ShopId]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE SET NULL
END
GO

PRINT 'Sprint 3 migration completed.'
GO
