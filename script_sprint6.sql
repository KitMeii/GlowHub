-- ============================================================
-- 29/05/2026
-- Script Sprint 6: Customer - Đặt Hàng Nâng Cao + Theo Dõi
-- Chạy nhiều lần không lỗi (IF NOT EXISTS / IF COL_LENGTH)
-- ============================================================

USE BaseCoreDB
GO

-- ─────────────────────────────────────────────────────────────
-- 1. Orders — thêm cột mới (Sprint 6)
-- ─────────────────────────────────────────────────────────────

-- Thêm các cột mới vào Orders
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'OrderCode' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [OrderCode] NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'FinalAmount' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [FinalAmount] DECIMAL(18,2) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'ShippingFee' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [ShippingFee] DECIMAL(18,2) DEFAULT 30000;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'PaymentMethod' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [PaymentMethod] INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'PaymentStatus' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [PaymentStatus] INT DEFAULT 0;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'EstimatedDelivery' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [EstimatedDelivery] DATETIME2 NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CancelReason' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [CancelReason] NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'TrackingCode' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [TrackingCode] NVARCHAR(100) NULL;
GO

-- ─────────────────────────────────────────────────────────────
-- 2. OrderStatusHistories — bảng lịch sử trạng thái đơn hàng
-- ─────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT * FROM sys.objects WHERE Name = 'OrderStatusHistories' AND Type = 'U')
BEGIN
    CREATE TABLE [dbo].[OrderStatusHistories] (
        [Id]        INT            NOT NULL IDENTITY(1,1),
        [OrderId]   INT            NOT NULL,
        [Status]    NVARCHAR(20)   NOT NULL,
        [Note]      NVARCHAR(500)  NULL,
        [ChangedBy] NVARCHAR(450)  NULL,
        [ChangedAt] DATETIME2      NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrderStatusHistories_Orders]
            FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id])
            ON DELETE CASCADE
    );
    PRINT 'Created table OrderStatusHistories';
END
ELSE
    PRINT 'Table OrderStatusHistories already exists — skipped';

-- ─────────────────────────────────────────────────────────────
-- 3. Vouchers — thêm cột ShopId nếu chưa có
-- ─────────────────────────────────────────────────────────────

IF COL_LENGTH('dbo.Vouchers', 'ShopId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Vouchers] ADD [ShopId] NVARCHAR(450) NULL;
    ALTER TABLE [dbo].[Vouchers]
        ADD CONSTRAINT [FK_Vouchers_Shops]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id])
        ON DELETE SET NULL;
    PRINT 'Added ShopId column to Vouchers';
END

-- ─────────────────────────────────────────────────────────────
-- 4. Backfill FinalAmount cho đơn cũ
-- ─────────────────────────────────────────────────────────────

UPDATE [dbo].[Orders]
SET [FinalAmount] = [TotalAmount] + ISNULL([ShippingFee], 30000)
WHERE [FinalAmount] IS NULL OR [FinalAmount] = 0;

-- ─────────────────────────────────────────────────────────────
-- 5. Tạo OrderCode cho đơn cũ chưa có
-- ─────────────────────────────────────────────────────────────

-- Backfill OrderCode
UPDATE [dbo].[Orders]
SET [OrderCode] = 'ORD-' + RIGHT('000000' + CAST([Id] AS VARCHAR), 6)
WHERE [OrderCode] IS NULL;

PRINT 'Backfill completed!';
GO
-- ─────────────────────────────────────────────────────────────
-- 6. Seed dữ liệu mẫu Voucher nếu chưa có
-- ─────────────────────────────────────────────────────────────

IF NOT EXISTS (SELECT 1 FROM [dbo].[Vouchers] WHERE [Code] = 'GLOW10')
    INSERT INTO [dbo].[Vouchers] ([Code],[Description],[DiscountType],[DiscountValue],[MinOrderAmount],[MaxDiscount],[UsageLimit],[UsedCount],[IsActive],[CreatedAt])
    VALUES ('GLOW10', N'Giảm 10% tối đa 50.000đ', 'percent', 10, 200000, 50000, 100, 0, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Vouchers] WHERE [Code] = 'FREESHIP')
    INSERT INTO [dbo].[Vouchers] ([Code],[Description],[DiscountType],[DiscountValue],[MinOrderAmount],[MaxDiscount],[UsageLimit],[UsedCount],[IsActive],[CreatedAt])
    VALUES ('FREESHIP', N'Miễn phí vận chuyển', 'fixed', 30000, 150000, NULL, 200, 0, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Vouchers] WHERE [Code] = 'GLOW50K')
    INSERT INTO [dbo].[Vouchers] ([Code],[Description],[DiscountType],[DiscountValue],[MinOrderAmount],[MaxDiscount],[UsageLimit],[UsedCount],[IsActive],[CreatedAt])
    VALUES ('GLOW50K', N'Giảm 50.000đ đơn từ 500.000đ', 'fixed', 50000, 500000, NULL, 50, 0, 1, GETUTCDATE());

PRINT 'Sprint 6 migration completed successfully!';

SELECT TOP 5 Id, UserId, OrderCode, Status, TotalAmount 
FROM [dbo].[Orders]
ORDER BY Id DESC

SELECT Id, UserName, UserType 
FROM [dbo].[Users]
WHERE Id = '3B8804A2-D9E0-4420-B0BC-5DEE8F0056D8'


UPDATE [dbo].[Users]
SET   Password = '+FdPdpNQPt+S79WIe69RCOtV3djP3rgqgbD3w9BpqIU=',
      Salt     = 0x926468AE0B5CAB9A331C28DB6B7FBEF6
WHERE UserName = 'lan.nguyen';

--29/5/2026
-- Xóa default constraint của PaymentStatus
DECLARE @constraint NVARCHAR(200)
SELECT @constraint = name 
FROM sys.default_constraints 
WHERE parent_object_id = OBJECT_ID('Orders')
AND col_name(parent_object_id, parent_column_id) = 'PaymentStatus'

IF @constraint IS NOT NULL
  EXEC('ALTER TABLE [dbo].[Orders] DROP CONSTRAINT ' + @constraint)

-- Xóa default constraint của PaymentMethod  
DECLARE @constraint2 NVARCHAR(200)
SELECT @constraint2 = name 
FROM sys.default_constraints 
WHERE parent_object_id = OBJECT_ID('Orders')
AND col_name(parent_object_id, parent_column_id) = 'PaymentMethod'

IF @constraint2 IS NOT NULL
  EXEC('ALTER TABLE [dbo].[Orders] DROP CONSTRAINT ' + @constraint2)
GO

-- Giờ mới đổi kiểu cột
ALTER TABLE [dbo].[Orders] 
ALTER COLUMN [PaymentMethod] NVARCHAR(20) NULL;

ALTER TABLE [dbo].[Orders] 
ALTER COLUMN [PaymentStatus] NVARCHAR(20) NULL;
GO

-- Update data
UPDATE [dbo].[Orders]
SET PaymentMethod = 'COD', PaymentStatus = 'UNPAID'
WHERE PaymentMethod IS NULL OR PaymentStatus IS NULL;

SELECT Id, OrderCode, PaymentMethod, PaymentStatus 
FROM [dbo].[Orders]
GO