/* ============================================================
   GLOWHUB — SEED DATA FIX (chạy SAU seed-data.sql)
   ------------------------------------------------------------
   Fix 3 vấn đề từ lần chạy trước:
     1) Tạo bảng Banners + seed 10 banner
     2) Tạo bảng Vouchers + seed 10 voucher
     3) Sửa bug NEWID() trong CartItems loop (xóa 40 dòng cart
        đã insert thành công + insert lại đúng)
   An toàn idempotent: chạy nhiều lần OK.
   ============================================================ */

USE [BaseCoreDB];
GO
SET NOCOUNT ON;

PRINT N'╔══════════════════════════════════════════════════════════╗';
PRINT N'║      GLOWHUB — SEED FIX — Banners + Vouchers + Carts     ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

/* ============================================================
   STEP 1 — CREATE TABLE Banners (nếu chưa có)
   ============================================================ */
PRINT N'';
PRINT N'[1/3] Banners — tạo bảng + seed...';

IF OBJECT_ID('dbo.Banners', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Banners] (
        [Id]          INT             IDENTITY(1,1) NOT NULL,
        [Title]       NVARCHAR(200)   NOT NULL,
        [Subtitle]    NVARCHAR(500)   NULL,
        [ImageUrl]    NVARCHAR(500)   NOT NULL DEFAULT '',
        [LinkUrl]     NVARCHAR(500)   NULL,
        [ButtonText]  NVARCHAR(50)    NULL,
        [BgColor]     NVARCHAR(20)    NULL,
        [SortOrder]   INT             NOT NULL DEFAULT 0,
        [IsActive]    BIT             NOT NULL DEFAULT 1,
        [CreatedAt]   DATETIME2(7)    NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt]   DATETIME2(7)    NULL,
        [CreatedBy]   NVARCHAR(450)   NULL,
        CONSTRAINT [PK_Banners] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT N'    + Đã tạo bảng Banners';
END
ELSE
BEGIN
    PRINT N'    - Bảng Banners đã tồn tại — skip CREATE';
END

DECLARE @cnt INT;

;WITH BannerData(Title, Subtitle, ImageUrl, LinkUrl, ButtonText, BgColor, SortOrder, IsActive) AS (
    SELECT * FROM (VALUES
        (N'Bộ Sưu Tập Mùa Hè 2026',  N'Tươi mát, rạng rỡ mỗi ngày',           'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '/shop.html',         N'Khám phá ngay', '#fff0f6', 1, CAST(1 AS BIT)),
        (N'Giảm 30% Skincare',        N'Áp dụng cho mọi sản phẩm dưỡng da',    'https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=1200', '/shop.html?cat=1',   N'Mua sắm',       '#ffd6e7', 2, CAST(1 AS BIT)),
        (N'Son Lì Black Rouge Mới',  N'Bộ sưu tập 12 màu giới hạn',           'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=1200', '/shop.html?cat=2',   N'Xem ngay',      '#fce7f3', 3, CAST(1 AS BIT)),
        (N'Nước Hoa Cao Cấp',         N'Hương thơm tinh tế, lưu hương lâu',     'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=1200', '/shop.html?cat=4',   N'Khám phá',      '#fdf2f8', 4, CAST(1 AS BIT)),
        (N'Mặt Nạ Mediheal',          N'Mua 5 tặng 1, áp dụng đến hết tháng',  'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=1200', '/shop.html?cat=8',   N'Săn ưu đãi',    '#fdf2f8', 5, CAST(1 AS BIT)),
        (N'Free Ship Toàn Quốc',      N'Đơn từ 500.000đ',                       'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=1200', '/shop.html',         N'Đặt hàng',      '#fff0f6', 6, CAST(1 AS BIT)),
        (N'Banner Tết 2025 (đã ẩn)', N'Chương trình cũ',                       'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#',                  N'',              '#fff',    7, CAST(0 AS BIT)),
        (N'Banner Black Friday (đã ẩn)', N'Hết hạn',                            'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#',                  N'',              '#fff',    8, CAST(0 AS BIT)),
        (N'Banner 8/3 (đã ẩn)',       N'Hết hạn',                               'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#',                  N'',              '#fff',    9, CAST(0 AS BIT)),
        (N'Banner Noel (đã ẩn)',      N'Hết hạn',                               'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#',                  N'',              '#fff',    10, CAST(0 AS BIT))
    ) AS v(Title, Subtitle, ImageUrl, LinkUrl, ButtonText, BgColor, SortOrder, IsActive)
)
INSERT INTO Banners (Title, Subtitle, ImageUrl, LinkUrl, ButtonText, BgColor, SortOrder, IsActive, CreatedAt)
SELECT bd.Title, bd.Subtitle, bd.ImageUrl, bd.LinkUrl, bd.ButtonText, bd.BgColor, bd.SortOrder, bd.IsActive, GETDATE()
FROM BannerData bd
WHERE NOT EXISTS (SELECT 1 FROM Banners b WHERE b.Title = bd.Title);

SET @cnt = @@ROWCOUNT;
PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' banner mới';

/* ============================================================
   STEP 2 — CREATE TABLE Vouchers (nếu chưa có)
   ============================================================ */
PRINT N'';
PRINT N'[2/3] Vouchers — tạo bảng + seed...';

IF OBJECT_ID('dbo.Vouchers', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Vouchers] (
        [Id]               INT              IDENTITY(1,1) NOT NULL,
        [Code]             NVARCHAR(50)     NOT NULL,
        [Description]      NVARCHAR(200)    NULL,
        [DiscountType]     NVARCHAR(20)     NOT NULL DEFAULT 'percent',
        [DiscountValue]    DECIMAL(18,2)    NOT NULL,
        [MaxDiscount]      DECIMAL(18,2)    NULL,
        [MinOrderAmount]   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        [UsageLimit]       INT              NULL,
        [UsedCount]        INT              NOT NULL DEFAULT 0,
        [StartDate]        DATETIME2(7)     NULL,
        [ExpiryDate]       DATETIME2(7)     NULL,
        [IsActive]         BIT              NOT NULL DEFAULT 1,
        [CreatedAt]        DATETIME2(7)     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_Vouchers] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Vouchers_Code] ON [dbo].[Vouchers] ([Code] ASC);
    PRINT N'    + Đã tạo bảng Vouchers';
END
ELSE
BEGIN
    PRINT N'    - Bảng Vouchers đã tồn tại — skip CREATE';
END

;WITH VoucherData(Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, StartDate, ExpiryDate, IsActive) AS (
    SELECT * FROM (VALUES
        ('WELCOME10',    CAST(N'Giảm 10% cho khách hàng mới — tối đa 50K' AS NVARCHAR(200)),     'percent',  CAST(10  AS DECIMAL(18,2)), CAST(50000   AS DECIMAL(18,2)), CAST(200000 AS DECIMAL(18,2)),  1000, DATEADD(MONTH, -2, GETDATE()),  DATEADD(MONTH, 6, GETDATE()),   CAST(1 AS BIT)),
        ('SUMMER20',     CAST(N'Giảm 20% cho đơn từ 500K — tối đa 100K' AS NVARCHAR(200)),       'percent',  CAST(20  AS DECIMAL(18,2)), CAST(100000  AS DECIMAL(18,2)), CAST(500000 AS DECIMAL(18,2)),  500,  DATEADD(MONTH, -1, GETDATE()),  DATEADD(MONTH, 3, GETDATE()),   CAST(1 AS BIT)),
        ('FREESHIP',     CAST(N'Miễn phí vận chuyển toàn quốc đơn từ 300K' AS NVARCHAR(200)),    'fixed',    CAST(30000 AS DECIMAL(18,2)),CAST(30000  AS DECIMAL(18,2)),  CAST(300000 AS DECIMAL(18,2)),  9999, DATEADD(MONTH, -3, GETDATE()),  DATEADD(YEAR, 1, GETDATE()),    CAST(1 AS BIT)),
        ('SKINCARE15',   CAST(N'Giảm 15% sản phẩm chăm sóc da' AS NVARCHAR(200)),                 'percent',  CAST(15  AS DECIMAL(18,2)), CAST(150000  AS DECIMAL(18,2)), CAST(0      AS DECIMAL(18,2)),  200,  DATEADD(MONTH, -1, GETDATE()),  DATEADD(MONTH, 2, GETDATE()),   CAST(1 AS BIT)),
        ('VIP50K',       CAST(N'Giảm thẳng 50K cho khách VIP' AS NVARCHAR(200)),                  'fixed',    CAST(50000 AS DECIMAL(18,2)),CAST(50000  AS DECIMAL(18,2)),  CAST(800000 AS DECIMAL(18,2)),  100,  DATEADD(MONTH, -2, GETDATE()),  DATEADD(MONTH, 4, GETDATE()),   CAST(1 AS BIT)),
        ('NEWUSER100K',  CAST(N'Tặng 100K cho đơn đầu tiên từ 1 triệu' AS NVARCHAR(200)),         'fixed',    CAST(100000 AS DECIMAL(18,2)),CAST(100000 AS DECIMAL(18,2)), CAST(1000000 AS DECIMAL(18,2)), 300,  DATEADD(MONTH, -1, GETDATE()),  DATEADD(MONTH, 6, GETDATE()),   CAST(1 AS BIT)),
        ('LIPSTICK25',   CAST(N'Giảm 25% son môi — chỉ áp dụng tháng này' AS NVARCHAR(200)),      'percent',  CAST(25  AS DECIMAL(18,2)), CAST(200000  AS DECIMAL(18,2)), CAST(0      AS DECIMAL(18,2)),  150,  GETDATE(),                       DATEADD(MONTH, 1, GETDATE()),   CAST(1 AS BIT)),
        ('BLACKFRIDAY',  CAST(N'BLACKFRIDAY — đã hết hạn' AS NVARCHAR(200)),                       'percent',  CAST(30  AS DECIMAL(18,2)), CAST(500000  AS DECIMAL(18,2)), CAST(1000000 AS DECIMAL(18,2)), 9999, DATEADD(YEAR, -1, GETDATE()),  DATEADD(MONTH, -10, GETDATE()), CAST(0 AS BIT)),
        ('TET2025',      CAST(N'Tết 2025 — đã kết thúc' AS NVARCHAR(200)),                         'percent',  CAST(20  AS DECIMAL(18,2)), CAST(300000  AS DECIMAL(18,2)), CAST(500000 AS DECIMAL(18,2)),  999,  DATEADD(MONTH, -8, GETDATE()),  DATEADD(MONTH, -6, GETDATE()),  CAST(0 AS BIT)),
        ('FLASH5',       CAST(N'Flash sale 5% cho mọi đơn — không min' AS NVARCHAR(200)),          'percent',  CAST(5   AS DECIMAL(18,2)), CAST(50000   AS DECIMAL(18,2)), CAST(0      AS DECIMAL(18,2)),  9999, DATEADD(MONTH, -1, GETDATE()),  DATEADD(MONTH, 12, GETDATE()),  CAST(1 AS BIT))
    ) AS v(Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, StartDate, ExpiryDate, IsActive)
)
INSERT INTO Vouchers (Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, UsedCount, StartDate, ExpiryDate, IsActive, CreatedAt)
SELECT vd.Code, vd.Description, vd.DiscountType, vd.DiscountValue, vd.MaxDiscount, vd.MinOrderAmount, vd.UsageLimit, 0, vd.StartDate, vd.ExpiryDate, vd.IsActive, GETDATE()
FROM VoucherData vd
WHERE NOT EXISTS (SELECT 1 FROM Vouchers v WHERE v.Code = vd.Code);

SET @cnt = @@ROWCOUNT;
PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' voucher mới';

/* ============================================================
   STEP 3 — CARTITEMS (fix bug NEWID() inline)
   ------------------------------------------------------------
   Tách @row = (ABS(CHECKSUM(NEWID())) % N) + 1 vào BIẾN scalar
   trước khi WHERE — tránh SQL re-evaluate NEWID() cho từng row.
   ============================================================ */
PRINT N'';
PRINT N'[3/3] CartItems — fix bug + seed bổ sung...';

DECLARE @existingCart INT = (SELECT COUNT(*) FROM CartItems);
DECLARE @cartTarget INT = 50;

IF @existingCart < @cartTarget
BEGIN
    DECLARE @activeCustomers TABLE (RowId INT IDENTITY(1,1), UserId NVARCHAR(450));
    INSERT INTO @activeCustomers (UserId)
    SELECT TOP 10 Id FROM Users WHERE UserType = 0 AND IsActive = 1 ORDER BY NEWID();

    DECLARE @prodList TABLE (RowId INT IDENTITY(1,1), ProductId INT);
    INSERT INTO @prodList (ProductId)
    SELECT Id FROM Products WHERE IsActive = 1;

    DECLARE @uCount INT = (SELECT COUNT(*) FROM @activeCustomers);
    DECLARE @pCount INT = (SELECT COUNT(*) FROM @prodList);
    DECLARE @needed INT = @cartTarget - @existingCart;
    DECLARE @ci INT = 0;
    DECLARE @cartAdded INT = 0;
    DECLARE @attempts INT = 0;

    WHILE @ci < @needed AND @attempts < 200
    BEGIN
        -- Tính @uRow và @pRow vào biến scalar TRƯỚC — fix bug NEWID() re-evaluate
        DECLARE @uRow INT = (ABS(CHECKSUM(NEWID())) % @uCount) + 1;
        DECLARE @pRow INT = (ABS(CHECKSUM(NEWID())) % @pCount) + 1;

        DECLARE @cu NVARCHAR(450) = (SELECT UserId    FROM @activeCustomers WHERE RowId = @uRow);
        DECLARE @cp INT           = (SELECT ProductId FROM @prodList        WHERE RowId = @pRow);

        IF @cu IS NOT NULL AND @cp IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM CartItems WHERE UserId = @cu AND ProductId = @cp)
        BEGIN
            INSERT INTO CartItems (UserId, ProductId, Quantity, AddedAt)
            VALUES (@cu, @cp, (ABS(CHECKSUM(NEWID())) % 3) + 1,
                    DATEADD(HOUR, -ABS(CHECKSUM(NEWID())) % 168, GETDATE()));
            SET @cartAdded = @cartAdded + 1;
            SET @ci = @ci + 1;
        END
        SET @attempts = @attempts + 1;
    END

    PRINT N'    + Đã thêm ' + CAST(@cartAdded AS NVARCHAR) + N' cart item mới';
END
ELSE
BEGIN
    PRINT N'    - (skip) Đã có ' + CAST(@existingCart AS NVARCHAR) + N' cart item';
END

/* ============================================================
   TỔNG KẾT
   ============================================================ */
PRINT N'';
PRINT N'╔══════════════════════════════════════════════════════════╗';
PRINT N'║                    FIX HOÀN TẤT                          ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

SELECT
    N'Banners'                                AS [Bảng], COUNT(*) AS [Số dòng] FROM Banners
UNION ALL SELECT N'  └─ Active',    COUNT(*) FROM Banners  WHERE IsActive = 1
UNION ALL SELECT N'Vouchers',       COUNT(*) FROM Vouchers
UNION ALL SELECT N'  └─ Active',    COUNT(*) FROM Vouchers WHERE IsActive = 1
UNION ALL SELECT N'CartItems',      COUNT(*) FROM CartItems;

PRINT N'';
PRINT N'✓ Reload admin.html → tab Banner / Voucher / Giỏ hàng đã có data';
PRINT N'';
GO
