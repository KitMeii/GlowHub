-- ============================================================
-- RENUMBER toàn bộ Id (IDENTITY int) về 1,2,3... liên tục
-- Giữ nguyên dữ liệu, chỉ đánh số lại.
-- Bỏ qua bảng Users vì Users.Id là GUID nvarchar(450).
--
-- LƯU Ý:
--  1. BACKUP DATABASE trước khi chạy (rủi ro mất dữ liệu nếu sai).
--  2. Đảm bảo app không đang ghi DB (dừng server) trong lúc chạy.
--  3. Chạy 1 lần, không idempotent — chạy lại trên DB đã renumber
--     vẫn OK vì kết quả là 1..N (không thay đổi).
-- ============================================================
USE [BaseCoreDB];
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

BEGIN TRAN;

-- ============================================================
-- 1) DROP các FK liên quan đến cột Id sẽ thay đổi
-- ============================================================
ALTER TABLE [dbo].[Products]        DROP CONSTRAINT [FK_Products_Categories_CategoryId];
ALTER TABLE [dbo].[OrderDetails]    DROP CONSTRAINT [FK_OrderDetails_Orders_OrderId];
ALTER TABLE [dbo].[OrderDetails]    DROP CONSTRAINT [FK_OrderDetails_Products_ProductId];
ALTER TABLE [dbo].[CartItems]       DROP CONSTRAINT [FK_CartItems_Products_ProductId];
ALTER TABLE [dbo].[OrderStatusLogs] DROP CONSTRAINT [FK_OrderStatusLogs_Orders_OrderId];
ALTER TABLE [dbo].[Reviews]         DROP CONSTRAINT [FK_Reviews_Products_ProductId];
-- Các FK trỏ tới Users.Id (string) không bị ảnh hưởng, giữ nguyên:
--   FK_Orders_Users_UserId, FK_CartItems_Users_UserId,
--   FK_OrderStatusLogs_Users_ChangedBy, FK_Reviews_Users_UserId

-- FK do EF tạo cho FeaturedProducts → Products (nếu tồn tại)
DECLARE @fkName sysname;
SELECT @fkName = fk.name
FROM   sys.foreign_keys fk
JOIN   sys.tables t ON fk.parent_object_id = t.object_id
WHERE  t.name = 'FeaturedProducts';
IF @fkName IS NOT NULL
    EXEC('ALTER TABLE [dbo].[FeaturedProducts] DROP CONSTRAINT [' + @fkName + ']');

-- ============================================================
-- 2) CATEGORIES (parent) → renumber + cập nhật Products.CategoryId
-- ============================================================
SELECT [Id] AS OldId,
       CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS NewId
INTO   #MapCat
FROM   [dbo].[Categories];

UPDATE p
SET    p.[CategoryId] = m.NewId
FROM   [dbo].[Products] p
JOIN   #MapCat m ON p.[CategoryId] = m.OldId;

SELECT m.NewId AS [Id], c.[Name], c.[Description]
INTO   #CatNew
FROM   [dbo].[Categories] c
JOIN   #MapCat m ON c.[Id] = m.OldId;

DELETE FROM [dbo].[Categories];
DBCC CHECKIDENT ('[dbo].[Categories]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[Categories] ON;
INSERT INTO [dbo].[Categories] ([Id], [Name], [Description])
SELECT [Id], [Name], [Description] FROM #CatNew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[Categories] OFF;

DBCC CHECKIDENT ('[dbo].[Categories]', RESEED);

DROP TABLE #CatNew;
DROP TABLE #MapCat;

-- ============================================================
-- 3) PRODUCTS (parent) → renumber + cập nhật OrderDetails, CartItems, Reviews
-- ============================================================
SELECT [Id] AS OldId,
       CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS NewId
INTO   #MapProd
FROM   [dbo].[Products];

UPDATE od SET od.[ProductId] = m.NewId
FROM   [dbo].[OrderDetails] od
JOIN   #MapProd m ON od.[ProductId] = m.OldId;

UPDATE ci SET ci.[ProductId] = m.NewId
FROM   [dbo].[CartItems] ci
JOIN   #MapProd m ON ci.[ProductId] = m.OldId;

UPDATE r  SET r.[ProductId]  = m.NewId
FROM   [dbo].[Reviews] r
JOIN   #MapProd m ON r.[ProductId] = m.OldId;

SELECT m.NewId AS [Id],
       p.[Name], p.[Price], p.[Stock], p.[ImageUrl],
       p.[Description], p.[CategoryId], p.[IsActive]
INTO   #ProdNew
FROM   [dbo].[Products] p
JOIN   #MapProd m ON p.[Id] = m.OldId;

DELETE FROM [dbo].[Products];
DBCC CHECKIDENT ('[dbo].[Products]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[Products] ON;
INSERT INTO [dbo].[Products]
    ([Id], [Name], [Price], [Stock], [ImageUrl], [Description], [CategoryId], [IsActive])
SELECT [Id], [Name], [Price], [Stock], [ImageUrl], [Description], [CategoryId], [IsActive]
FROM   #ProdNew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[Products] OFF;

DBCC CHECKIDENT ('[dbo].[Products]', RESEED);

DROP TABLE #ProdNew;
DROP TABLE #MapProd;

-- ============================================================
-- 4) ORDERS (parent) → renumber + cập nhật OrderDetails, OrderStatusLogs
-- ============================================================
SELECT [Id] AS OldId,
       CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS NewId
INTO   #MapOrd
FROM   [dbo].[Orders];

UPDATE od SET od.[OrderId] = m.NewId
FROM   [dbo].[OrderDetails] od
JOIN   #MapOrd m ON od.[OrderId] = m.OldId;

UPDATE l  SET l.[OrderId]  = m.NewId
FROM   [dbo].[OrderStatusLogs] l
JOIN   #MapOrd m ON l.[OrderId] = m.OldId;

SELECT m.NewId AS [Id],
       o.[UserId], o.[OrderDate], o.[UpdatedAt], o.[TotalAmount],
       o.[Status], o.[ShippingAddress], o.[Note]
INTO   #OrdNew
FROM   [dbo].[Orders] o
JOIN   #MapOrd m ON o.[Id] = m.OldId;

DELETE FROM [dbo].[Orders];
DBCC CHECKIDENT ('[dbo].[Orders]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[Orders] ON;
INSERT INTO [dbo].[Orders]
    ([Id], [UserId], [OrderDate], [UpdatedAt], [TotalAmount], [Status], [ShippingAddress], [Note])
SELECT [Id], [UserId], [OrderDate], [UpdatedAt], [TotalAmount], [Status], [ShippingAddress], [Note]
FROM   #OrdNew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[Orders] OFF;

DBCC CHECKIDENT ('[dbo].[Orders]', RESEED);

DROP TABLE #OrdNew;
DROP TABLE #MapOrd;

-- ============================================================
-- 5) Bảng leaf (không có ai tham chiếu Id) → chỉ renumber chính nó
--    OrderDetails, CartItems, OrderStatusLogs, Reviews
-- ============================================================

-- 5a) OrderDetails
SELECT CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS [Id],
       [OrderId], [ProductId], [Quantity], [UnitPrice]
INTO   #ODNew
FROM   [dbo].[OrderDetails];

DELETE FROM [dbo].[OrderDetails];
DBCC CHECKIDENT ('[dbo].[OrderDetails]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[OrderDetails] ON;
INSERT INTO [dbo].[OrderDetails] ([Id], [OrderId], [ProductId], [Quantity], [UnitPrice])
SELECT [Id], [OrderId], [ProductId], [Quantity], [UnitPrice]
FROM   #ODNew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[OrderDetails] OFF;

DBCC CHECKIDENT ('[dbo].[OrderDetails]', RESEED);
DROP TABLE #ODNew;

-- 5b) CartItems
SELECT CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS [Id],
       [UserId], [ProductId], [Quantity], [AddedAt]
INTO   #CINew
FROM   [dbo].[CartItems];

DELETE FROM [dbo].[CartItems];
DBCC CHECKIDENT ('[dbo].[CartItems]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[CartItems] ON;
INSERT INTO [dbo].[CartItems] ([Id], [UserId], [ProductId], [Quantity], [AddedAt])
SELECT [Id], [UserId], [ProductId], [Quantity], [AddedAt]
FROM   #CINew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[CartItems] OFF;

DBCC CHECKIDENT ('[dbo].[CartItems]', RESEED);
DROP TABLE #CINew;

-- 5c) OrderStatusLogs
SELECT CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS [Id],
       [OrderId], [OldStatus], [NewStatus], [ChangedBy], [Note], [CreatedAt]
INTO   #OSLNew
FROM   [dbo].[OrderStatusLogs];

DELETE FROM [dbo].[OrderStatusLogs];
DBCC CHECKIDENT ('[dbo].[OrderStatusLogs]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[OrderStatusLogs] ON;
INSERT INTO [dbo].[OrderStatusLogs]
    ([Id], [OrderId], [OldStatus], [NewStatus], [ChangedBy], [Note], [CreatedAt])
SELECT [Id], [OrderId], [OldStatus], [NewStatus], [ChangedBy], [Note], [CreatedAt]
FROM   #OSLNew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[OrderStatusLogs] OFF;

DBCC CHECKIDENT ('[dbo].[OrderStatusLogs]', RESEED);
DROP TABLE #OSLNew;

-- 5d) Reviews
SELECT CAST(ROW_NUMBER() OVER (ORDER BY [Id]) AS int) AS [Id],
       [ProductId], [UserId], [Rating], [Comment], [CreatedAt]
INTO   #RNew
FROM   [dbo].[Reviews];

DELETE FROM [dbo].[Reviews];
DBCC CHECKIDENT ('[dbo].[Reviews]', RESEED, 0);

SET IDENTITY_INSERT [dbo].[Reviews] ON;
INSERT INTO [dbo].[Reviews] ([Id], [ProductId], [UserId], [Rating], [Comment], [CreatedAt])
SELECT [Id], [ProductId], [UserId], [Rating], [Comment], [CreatedAt]
FROM   #RNew ORDER BY [Id];
SET IDENTITY_INSERT [dbo].[Reviews] OFF;

DBCC CHECKIDENT ('[dbo].[Reviews]', RESEED);
DROP TABLE #RNew;

-- ============================================================
-- 6) RECREATE FK
-- ============================================================
ALTER TABLE [dbo].[Products]
    ADD CONSTRAINT [FK_Products_Categories_CategoryId]
    FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]);

ALTER TABLE [dbo].[OrderDetails]
    ADD CONSTRAINT [FK_OrderDetails_Orders_OrderId]
    FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE;

ALTER TABLE [dbo].[OrderDetails]
    ADD CONSTRAINT [FK_OrderDetails_Products_ProductId]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]);

ALTER TABLE [dbo].[CartItems]
    ADD CONSTRAINT [FK_CartItems_Products_ProductId]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]);

ALTER TABLE [dbo].[OrderStatusLogs]
    ADD CONSTRAINT [FK_OrderStatusLogs_Orders_OrderId]
    FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE;

ALTER TABLE [dbo].[Reviews]
    ADD CONSTRAINT [FK_Reviews_Products_ProductId]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE;

COMMIT TRAN;
GO

-- ============================================================
-- 7) Verify — kết quả phải là MinId=1, MaxId=Rows cho mọi bảng
-- ============================================================
SELECT 'Categories'      AS [Table], COUNT(*) AS [Rows], MIN([Id]) AS MinId, MAX([Id]) AS MaxId FROM [dbo].[Categories]
UNION ALL
SELECT 'Products',        COUNT(*), MIN([Id]), MAX([Id]) FROM [dbo].[Products]
UNION ALL
SELECT 'Orders',          COUNT(*), MIN([Id]), MAX([Id]) FROM [dbo].[Orders]
UNION ALL
SELECT 'OrderDetails',    COUNT(*), MIN([Id]), MAX([Id]) FROM [dbo].[OrderDetails]
UNION ALL
SELECT 'CartItems',       COUNT(*), MIN([Id]), MAX([Id]) FROM [dbo].[CartItems]
UNION ALL
SELECT 'OrderStatusLogs', COUNT(*), MIN([Id]), MAX([Id]) FROM [dbo].[OrderStatusLogs]
UNION ALL
SELECT 'Reviews',         COUNT(*), MIN([Id]), MAX([Id]) FROM [dbo].[Reviews];

PRINT N'✓ Đã renumber toàn bộ ID về 1..N';
