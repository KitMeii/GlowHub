-- ============================================================
-- Script Sprint 6: Customer - Đặt Hàng Nâng Cao + Theo Dõi
-- Chạy nhiều lần không lỗi (IF NOT EXISTS / IF COL_LENGTH)
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- 1. Orders — thêm cột mới (Sprint 6)
-- ─────────────────────────────────────────────────────────────

IF COL_LENGTH('dbo.Orders', 'ShippingFee') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ShippingFee] DECIMAL(18,2) NOT NULL DEFAULT 30000;

IF COL_LENGTH('dbo.Orders', 'Discount') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [Discount] DECIMAL(18,2) NOT NULL DEFAULT 0;

IF COL_LENGTH('dbo.Orders', 'FinalAmount') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [FinalAmount] DECIMAL(18,2) NOT NULL DEFAULT 0;

IF COL_LENGTH('dbo.Orders', 'PaymentMethod') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [PaymentMethod] INT NOT NULL DEFAULT 0;
    -- 0=COD, 1=Bank, 2=MoMo, 3=ZaloPay

IF COL_LENGTH('dbo.Orders', 'PaymentStatus') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [PaymentStatus] INT NOT NULL DEFAULT 0;
    -- 0=Unpaid, 1=Paid, 2=Refunded

IF COL_LENGTH('dbo.Orders', 'OrderCode') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [OrderCode] NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.Orders', 'ReceiverName') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ReceiverName] NVARCHAR(100) NULL;

IF COL_LENGTH('dbo.Orders', 'ReceiverPhone') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [ReceiverPhone] NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.Orders', 'EstimatedDelivery') IS NULL
    ALTER TABLE [dbo].[Orders] ADD [EstimatedDelivery] DATETIME2 NULL;

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
SET [FinalAmount] = [TotalAmount] + [ShippingFee] - [Discount]
WHERE [FinalAmount] = 0 AND [TotalAmount] > 0;

-- ─────────────────────────────────────────────────────────────
-- 5. Tạo OrderCode cho đơn cũ chưa có
-- ─────────────────────────────────────────────────────────────

UPDATE [dbo].[Orders]
SET [OrderCode] = 'ORD-' + RIGHT('000000' + CAST([Id] AS NVARCHAR), 6)
WHERE [OrderCode] IS NULL;

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
