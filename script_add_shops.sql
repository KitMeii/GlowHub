-- ============================================================
-- MIGRATION: Add Shops + ShopProducts tables to BaseCoreDB
-- Run this script on your existing BaseCoreDB database
-- ============================================================

USE [BaseCoreDB]
GO

-- ============================================================
-- BANG Shops
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Shops')
BEGIN
    CREATE TABLE [dbo].[Shops] (
        [Id]             [nvarchar](450)   NOT NULL,
        [SellerId]       [nvarchar](450)   NOT NULL,
        [ShopName]       [nvarchar](100)   NOT NULL,
        [Description]    [nvarchar](500)   NOT NULL DEFAULT (''),
        [Logo]           [nvarchar](500)   NOT NULL DEFAULT (''),
        [Address]        [nvarchar](300)   NOT NULL DEFAULT (''),
        [Phone]          [nvarchar](20)    NOT NULL DEFAULT (''),
        [Status]         [int]             NOT NULL DEFAULT (0),
        [CommissionRate] [decimal](5,2)    NOT NULL DEFAULT (10.00),
        [CreatedAt]      [datetime2](7)    NOT NULL DEFAULT (GETUTCDATE()),
        [UpdatedAt]      [datetime2](7)    NULL,
        CONSTRAINT [PK_Shops] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY]
END
GO

-- ============================================================
-- BANG ShopProducts
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ShopProducts')
BEGIN
    CREATE TABLE [dbo].[ShopProducts] (
        [Id]        [int]           IDENTITY(1,1) NOT NULL,
        [ShopId]    [nvarchar](450) NOT NULL,
        [ProductId] [int]           NOT NULL,
        [AddedAt]   [datetime2](7)  NOT NULL DEFAULT (GETUTCDATE()),
        CONSTRAINT [PK_ShopProducts] PRIMARY KEY CLUSTERED ([Id] ASC)
    ) ON [PRIMARY]
END
GO

-- Unique: moi san pham chi thuoc 1 shop 1 lan
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ShopProducts_ShopId_ProductId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_ShopProducts_ShopId_ProductId]
        ON [dbo].[ShopProducts]([ShopId] ASC, [ProductId] ASC)
END
GO

-- ============================================================
-- FOREIGN KEYS
-- ============================================================

-- Shops -> Users (SellerId)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Shops_Users_SellerId')
BEGIN
    ALTER TABLE [dbo].[Shops]
        ADD CONSTRAINT [FK_Shops_Users_SellerId]
        FOREIGN KEY ([SellerId]) REFERENCES [dbo].[Users]([Id])
END
GO

-- ShopProducts -> Shops (CASCADE: xoa shop => xoa het san pham cua shop)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShopProducts_Shops_ShopId')
BEGIN
    ALTER TABLE [dbo].[ShopProducts]
        ADD CONSTRAINT [FK_ShopProducts_Shops_ShopId]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE
END
GO

-- ShopProducts -> Products
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ShopProducts_Products_ProductId')
BEGIN
    ALTER TABLE [dbo].[ShopProducts]
        ADD CONSTRAINT [FK_ShopProducts_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
END
GO

PRINT 'Tables Shops and ShopProducts created successfully.'
GO
