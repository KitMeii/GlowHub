-- ============================================================================
-- GlowHub — Full test seed data
-- Target  : Microsoft SQL Server
-- DB Name : BaseCoreDB
-- Usage   : Run AFTER setup-database.sql.
--           Idempotent: only inserts rows that don't already exist.
-- Login   : All accounts use password = "123456"
--           (Salt = NULL, Password = MD5('123456') = e10adc3949ba59abbe56e057f20f883e)
-- ============================================================================

USE [BaseCoreDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY

-- ─────────────────────────────────────────────────────────────
-- Fixed IDs (so child rows can reference them deterministically)
-- ─────────────────────────────────────────────────────────────
DECLARE @AdminId    NVARCHAR(450) = 'admin-0000-0000-0000-000000000001';
DECLARE @Seller1Id  NVARCHAR(450) = 'seller-0000-0000-0000-00000000s001';
DECLARE @Seller2Id  NVARCHAR(450) = 'seller-0000-0000-0000-00000000s002';
DECLARE @Seller3Id  NVARCHAR(450) = 'seller-0000-0000-0000-00000000s003';
DECLARE @Cust1Id    NVARCHAR(450) = 'cust-00000-0000-0000-000000000c01';
DECLARE @Cust2Id    NVARCHAR(450) = 'cust-00000-0000-0000-000000000c02';
DECLARE @Cust3Id    NVARCHAR(450) = 'cust-00000-0000-0000-000000000c03';
DECLARE @Cust4Id    NVARCHAR(450) = 'cust-00000-0000-0000-000000000c04';
DECLARE @Cust5Id    NVARCHAR(450) = 'cust-00000-0000-0000-000000000c05';

DECLARE @Shop1Id    NVARCHAR(450) = 'shop-00000-0000-0000-0000000shop1';
DECLARE @Shop2Id    NVARCHAR(450) = 'shop-00000-0000-0000-0000000shop2';
DECLARE @Shop3Id    NVARCHAR(450) = 'shop-00000-0000-0000-0000000shop3';

DECLARE @Pwd NVARCHAR(255) = 'e10adc3949ba59abbe56e057f20f883e'; -- MD5('123456')

-- =============================================================================
-- 1. Users  (1 admin + 3 sellers + 5 customers)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @AdminId)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@AdminId, N'GlowHub Admin', 'admin', @Pwd, NULL, N'Quản trị viên', 'admin@glowhub.vn', '0901000001', N'Administrator', '', 1, 1, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Seller1Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Seller1Id, N'Trần Mai Anh', 'seller_mai', @Pwd, NULL, N'Chủ shop Glow Beauty', 'mai@glowhub.vn', '0902000001', N'Seller', '', 1, 2, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Seller2Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Seller2Id, N'Nguyễn Hữu Long', 'seller_long', @Pwd, NULL, N'Chủ shop Luxe House', 'long@glowhub.vn', '0902000002', N'Seller', '', 1, 2, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Seller3Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Seller3Id, N'Phạm Bích Ngọc', 'seller_ngoc', @Pwd, NULL, N'Chủ shop K-Beauty Korea', 'ngoc@glowhub.vn', '0902000003', N'Seller', '', 1, 2, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Cust1Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Cust1Id, N'Nguyễn Thị Lan', 'lan.nguyen', @Pwd, NULL, N'Khách hàng VIP', 'lan@gmail.com', '0912000001', N'Customer', '', 1, 0, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Cust2Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Cust2Id, N'Lê Minh Hà', 'ha.le', @Pwd, NULL, N'Khách hàng thân thiết', 'ha@gmail.com', '0912000002', N'Customer', '', 1, 0, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Cust3Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Cust3Id, N'Đỗ Hoàng Khoa', 'khoa.do', @Pwd, NULL, N'Khách hàng mới', 'khoa@gmail.com', '0912000003', N'Customer', '', 1, 0, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Cust4Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Cust4Id, N'Vũ Thanh Trúc', 'truc.vu', @Pwd, NULL, N'Khách hàng', 'truc@gmail.com', '0912000004', N'Customer', '', 1, 0, GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Id] = @Cust5Id)
    INSERT INTO [dbo].[Users] ([Id],[Name],[UserName],[Password],[Salt],[Contact],[Email],[Phone],[Position],[Image],[IsActive],[UserType],[Created])
    VALUES (@Cust5Id, N'Bùi Thu Hằng', 'hang.bui', @Pwd, NULL, N'Khách hàng', 'hang@gmail.com', '0912000005', N'Customer', '', 1, 0, GETDATE());

PRINT N'OK Users  - 1 admin + 3 sellers + 5 customers';

-- =============================================================================
-- 2. Categories
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Categories])
BEGIN
    SET IDENTITY_INSERT [dbo].[Categories] ON;
    INSERT INTO [dbo].[Categories] ([Id],[Name],[Description]) VALUES
    (1, N'Chăm sóc da mặt',  N'Serum, kem dưỡng, toner, sữa rửa mặt cho mọi loại da'),
    (2, N'Trang điểm',       N'Son môi, kem nền, phấn má, mascara và các sản phẩm makeup'),
    (3, N'Chăm sóc cơ thể', N'Kem dưỡng thể, tẩy tế bào chết, dầu dưỡng da toàn thân'),
    (4, N'Nước hoa',         N'Nước hoa nữ, nam, unisex tinh tế cho mọi dịp'),
    (5, N'Chăm sóc tóc',    N'Dầu gội, dầu xả, serum tóc, mặt nạ ủ tóc'),
    (6, N'Chống nắng',       N'Kem chống nắng vật lý và hóa học SPF 30-50+');
    SET IDENTITY_INSERT [dbo].[Categories] OFF;
END
PRINT N'OK Categories - 6';

-- =============================================================================
-- 3. Shops (3 shops)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Shops] WHERE [Id] = @Shop1Id)
    INSERT INTO [dbo].[Shops] ([Id],[SellerId],[ShopName],[Description],[Logo],[Address],[Phone],[Status],[CommissionRate],[Province],[Region])
    VALUES (@Shop1Id, @Seller1Id, N'Glow Beauty Boutique', N'Mỹ phẩm chính hãng nhập khẩu', '/uploads/shop-glow.png', N'123 Nguyễn Huệ, Q.1', '0902000001', 1, 10.00, N'Ho Chi Minh', 'SOUTH');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Shops] WHERE [Id] = @Shop2Id)
    INSERT INTO [dbo].[Shops] ([Id],[SellerId],[ShopName],[Description],[Logo],[Address],[Phone],[Status],[CommissionRate],[Province],[Region])
    VALUES (@Shop2Id, @Seller2Id, N'Luxe Beauty House', N'Mỹ phẩm cao cấp châu Âu', '/uploads/shop-luxe.png', N'456 Bà Triệu, Hai Bà Trưng', '0902000002', 1, 12.00, N'Ha Noi', 'NORTH');

IF NOT EXISTS (SELECT 1 FROM [dbo].[Shops] WHERE [Id] = @Shop3Id)
    INSERT INTO [dbo].[Shops] ([Id],[SellerId],[ShopName],[Description],[Logo],[Address],[Phone],[Status],[CommissionRate],[Province],[Region])
    VALUES (@Shop3Id, @Seller3Id, N'K-Beauty Korea', N'Mỹ phẩm Hàn Quốc chính hãng', '/uploads/shop-kbeauty.png', N'78 Trần Phú, Hải Châu', '0902000003', 1, 8.00, N'Da Nang', 'CENTRAL');

PRINT N'OK Shops - 3';

-- =============================================================================
-- 4. Products (24 products spread across shops + categories)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Products])
BEGIN
    INSERT INTO [dbo].[Products]
        ([Name],[Price],[Stock],[ImageUrl],[Description],[CategoryId],[IsActive],[DiscountPrice],[IsNew],[ShopId],[SoldCount],[ViewCount],[WeightGram])
    VALUES
    -- Chăm sóc da mặt (cat 1)
    (N'Serum Vitamin C SkinCeuticals CE Ferulic',  3200000, 50, '/uploads/p01.jpg', N'Serum chống oxy hóa với Vitamin C 15%, Vitamin E và Ferulic Acid.', 1, 1, 2880000, 1, @Shop2Id, 12, 320, 50),
    (N'Estée Lauder Advanced Night Repair 50ml',   2750000, 60, '/uploads/p02.jpg', N'Serum phục hồi ban đêm huyền thoại với công nghệ ChronoluxCB.',           1, 1, 2480000, 0, @Shop2Id, 45, 890, 80),
    (N'SK-II Facial Treatment Essence 230ml',      4200000, 35, '/uploads/p03.jpg', N'Nước thần chứa hơn 90% Pitera giúp da trong suốt.',                       1, 1, 3900000, 0, @Shop2Id, 28, 1240, 250),
    (N'Toner Some By Mi AHA BHA PHA 30 Days',       420000, 120,'/uploads/p04.jpg', N'Toner tẩy tế bào chết với 3 loại acid.',                                  1, 1, NULL,    1, @Shop3Id, 87, 1560, 150),
    (N'Sữa Rửa Mặt CeraVe Hydrating Cleanser',      380000, 150,'/uploads/p05.jpg', N'Sữa rửa mặt dịu nhẹ với 3 Ceramides và Hyaluronic Acid.',                 1, 1, NULL,    0, @Shop1Id, 156, 2100, 240),
    (N'Kem Dưỡng Laneige Water Sleeping Mask',      850000, 80, '/uploads/p06.jpg', N'Mặt nạ ngủ cấp ẩm chuyên sâu.',                                           1, 1, 720000,  1, @Shop3Id, 73, 1840, 70),

    -- Trang điểm (cat 2)
    (N'Son Kem Lì Black Rouge Air Fit Velvet',      285000, 200,'/uploads/p07.jpg', N'Son kem lì mịn màng công thức Air-fit siêu nhẹ.',                         2, 1, NULL,    0, @Shop3Id, 234, 3450, 20),
    (N'Dior Rouge Dior Satin Lipstick',            1050000, 120,'/uploads/p08.jpg', N'Son môi cao cấp với sắc đỏ biểu tượng.',                                  2, 1, 950000,  0, @Shop2Id, 56, 1780, 25),
    (N'Kem Nền Maybelline Fit Me Matte',            220000, 180,'/uploads/p09.jpg', N'Kem nền kiềm dầu che phủ hoàn hảo, kiểm soát bóng nhờn 24 giờ.',          2, 1, NULL,    0, @Shop1Id, 178, 2680, 30),
    (N'MAC Studio Fix Fluid SPF15 30ml',            950000, 100,'/uploads/p10.jpg', N'Kem nền che phủ hoàn hảo, bền màu suốt 24 giờ.',                          2, 1, NULL,    1, @Shop2Id, 67, 1200, 35),
    (N'NARS Radiant Creamy Concealer',              780000, 90, '/uploads/p11.jpg', N'Kem che khuyết điểm thần thánh, mỏng nhẹ tự nhiên.',                      2, 1, NULL,    0, @Shop2Id, 41, 980, 22),
    (N'Lancôme L''Absolu Rouge Cream',              920000, 110,'/uploads/p12.jpg', N'Son dưỡng mịn màng với dầu hoa hồng quý hiếm.',                           2, 1, 820000,  0, @Shop2Id, 38, 1100, 28),

    -- Chăm sóc cơ thể (cat 3)
    (N'Dove Body Love Intense Lotion',              185000, 250,'/uploads/p13.jpg', N'Dưỡng thể phục hồi sâu cho làn da cực khô.',                              3, 1, NULL,    0, @Shop1Id, 312, 1980, 300),
    (N'The Body Shop Shea Body Scrub',              620000, 75, '/uploads/p14.jpg', N'Tẩy tế bào chết toàn thân từ bơ hạt mỡ Ghana.',                           3, 1, 550000,  0, @Shop1Id, 64, 1340, 250),
    (N'L''Occitane Shea Ultra Rich Cream',         1450000, 50, '/uploads/p15.jpg', N'Kem dưỡng thể cao cấp chứa 25% bơ hạt mỡ nguyên chất.',                  3, 1, 1300000, 0, @Shop2Id, 26, 870, 200),

    -- Nước hoa (cat 4)
    (N'Chanel N°5 Eau de Parfum 50ml',             4800000, 30, '/uploads/p16.jpg', N'Biểu tượng của sự sang trọng và nữ tính vượt thời gian.',                4, 1, NULL,    0, @Shop2Id, 15, 1450, 250),
    (N'Dior Miss Dior Blooming Bouquet',           3200000, 40, '/uploads/p17.jpg', N'Hương hoa cỏ thanh khiết như một bó hoa mẫu đơn tươi.',                  4, 1, 2900000, 1, @Shop2Id, 22, 1280, 200),
    (N'YSL Black Opium EDP 50ml',                  3100000, 42, '/uploads/p18.jpg', N'Hương cà phê và vani gợi cảm, lôi cuốn.',                                4, 1, 2800000, 0, @Shop2Id, 29, 1190, 200),

    -- Chăm sóc tóc (cat 5)
    (N'L''Oréal Absolut Repair Shampoo',            680000, 90, '/uploads/p19.jpg', N'Dầu gội phục hồi hư tổn nặng từ protein hạt Diêm mạch vàng.',             5, 1, 580000,  0, @Shop1Id, 89, 1670, 280),
    (N'Pantene Pro-V Repair Conditioner',           175000, 200,'/uploads/p20.jpg', N'Dầu xả phục hồi tóc với công thức dưỡng chất Pro-V.',                     5, 1, NULL,    0, @Shop1Id, 245, 1890, 320),
    (N'Moroccanoil Treatment Oil 100ml',            920000, 70, '/uploads/p21.jpg', N'Dầu dưỡng tóc chứa tinh dầu Argan giúp tóc bóng mượt.',                  5, 1, 820000,  1, @Shop2Id, 52, 1320, 120),

    -- Chống nắng (cat 6)
    (N'Anessa Perfect UV Skincare Milk',            680000, 110,'/uploads/p22.jpg', N'Kem chống nắng bảo vệ tối đa với công nghệ Aqua Booster.',                6, 1, 600000,  0, @Shop3Id, 134, 2340, 60),
    (N'La Roche-Posay Anthelios UVMune 400',        780000, 95, '/uploads/p23.jpg', N'Kem chống nắng phổ rộng bảo vệ da khỏi tia UVA dài.',                     6, 1, NULL,    1, @Shop2Id, 78, 1670, 50),
    (N'Biore UV Aqua Rich Essence',                 320000, 180,'/uploads/p24.jpg', N'Kem chống nắng dạng nước, thấm nhanh không gây bết rít.',                6, 1, 290000,  0, @Shop3Id, 198, 2890, 50);
END
PRINT N'OK Products - 24';

-- =============================================================================
-- 5. ShopProducts (link products to shops)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[ShopProducts])
BEGIN
    INSERT INTO [dbo].[ShopProducts] ([ShopId],[ProductId])
    SELECT p.[ShopId], p.[Id] FROM [dbo].[Products] p WHERE p.[ShopId] IS NOT NULL;
END
PRINT N'OK ShopProducts';

-- =============================================================================
-- 6. UserAddresses
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[UserAddresses])
BEGIN
    INSERT INTO [dbo].[UserAddresses] ([UserId],[ReceiverName],[Phone],[Province],[ProvinceCode],[District],[DistrictCode],[Ward],[WardCode],[Detail],[Region],[IsDefault])
    VALUES
    (@Cust1Id, N'Nguyễn Thị Lan', '0912000001', N'Hồ Chí Minh', '79', N'Quận 1',      '760', N'Phường Bến Nghé',        '26734', N'12 Lê Lợi',                  'SOUTH',   1),
    (@Cust1Id, N'Nguyễn Thị Lan', '0912000001', N'Hà Nội',       '01', N'Hoàn Kiếm',   '002', N'Hàng Bạc',               '00046', N'34 Hàng Bồ',                  'NORTH',   0),
    (@Cust2Id, N'Lê Minh Hà',     '0912000002', N'Hà Nội',       '01', N'Cầu Giấy',    '005', N'Dịch Vọng',              '00076', N'88 Trần Thái Tông',           'NORTH',   1),
    (@Cust3Id, N'Đỗ Hoàng Khoa',  '0912000003', N'Đà Nẵng',      '48', N'Hải Châu',    '491', N'Thạch Thang',            '20266', N'56 Nguyễn Văn Linh',          'CENTRAL', 1),
    (@Cust4Id, N'Vũ Thanh Trúc',  '0912000004', N'Hồ Chí Minh', '79', N'Quận 3',      '770', N'Phường Võ Thị Sáu',      '26803', N'200 Lê Văn Sỹ',              'SOUTH',   1),
    (@Cust5Id, N'Bùi Thu Hằng',   '0912000005', N'Cần Thơ',     '92', N'Ninh Kiều',   '916', N'Tân An',                 '30898', N'15 Trần Hưng Đạo',           'SOUTH',   1);
END
PRINT N'OK UserAddresses';

-- =============================================================================
-- 7. Banners
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Banners])
BEGIN
    INSERT INTO [dbo].[Banners] ([Title],[Subtitle],[ImageUrl],[LinkUrl],[ButtonText],[BgColor],[SortOrder],[IsActive],[CreatedBy])
    VALUES
    (N'New Collection',         N'Spring / Summer 2026 — chạm vào vẻ đẹp mới', '/uploads/banner1.jpg', 'shop.html',          N'Mua Sắm Ngay',  '#f5f0ea', 1, 1, @AdminId),
    (N'Flash Sale Cuối Tuần',   N'Giảm tới 50% các sản phẩm hot',                '/uploads/banner2.jpg', 'shop.html?flash=1',  N'Xem Khuyến Mãi', '#fce4ec', 2, 1, @AdminId),
    (N'Bộ Sưu Tập Nước Hoa',    N'Hương thơm tinh tế cho mọi dịp',               '/uploads/banner3.jpg', 'shop.html?cat=4',    N'Khám Phá',       '#ede7f6', 3, 1, @AdminId);
END
PRINT N'OK Banners - 3';

-- =============================================================================
-- 8. FeaturedProducts
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[FeaturedProducts])
BEGIN
    -- new_arrivals: 4 sản phẩm IsNew=1 mới nhất
    INSERT INTO [dbo].[FeaturedProducts] ([ProductId],[Section],[SortOrder],[IsActive],[CreatedBy])
    SELECT TOP 4 [Id], 'new_arrivals', ROW_NUMBER() OVER (ORDER BY [Id] DESC), 1, @AdminId
    FROM [dbo].[Products] WHERE [IsActive]=1 AND [IsNew]=1 ORDER BY [Id] DESC;

    -- best_sellers: 4 sản phẩm SoldCount cao nhất
    INSERT INTO [dbo].[FeaturedProducts] ([ProductId],[Section],[SortOrder],[IsActive],[CreatedBy])
    SELECT TOP 4 [Id], 'best_sellers', ROW_NUMBER() OVER (ORDER BY [SoldCount] DESC), 1, @AdminId
    FROM [dbo].[Products] WHERE [IsActive]=1 ORDER BY [SoldCount] DESC;
END
PRINT N'OK FeaturedProducts';

-- =============================================================================
-- 9. SiteSettings
-- =============================================================================
MERGE [dbo].[SiteSettings] AS target
USING (VALUES
    -- General
    ('site.name',           N'GlowHub',                                                         'text',  'general',  N'Tên Website'),
    ('site.tagline',        N'Làm Đẹp Mỗi Ngày',                                                'text',  'general',  N'Slogan'),
    ('site.email',          'support@glowhub.vn',                                               'text',  'general',  N'Email Hỗ Trợ'),
    ('site.phone',          '1800 6868',                                                        'text',  'general',  N'Số Điện Thoại'),
    ('site.address',        N'123 Đường Nguyễn Huệ, Q.1, TP.HCM',                              'text',  'general',  N'Địa Chỉ'),
    ('order.default_commission', '10',                                                          'text',  'general',  N'Hoa Hồng Mặc Định (%)'),
    -- Homepage / topbar
    ('topbar_text',         N'🌸 MIỄN PHÍ VẬN CHUYỂN cho đơn hàng từ 500.000₫',                'text',  'homepage', N'Thanh thông báo'),
    ('topbar_visible',      '1',                                                                'bool',  'homepage', N'Hiển thị thanh thông báo'),
    ('topbar_bg_color',     '#111111',                                                          'color', 'homepage', N'Màu nền thanh thông báo'),
    ('hero_label',          N'Bộ Sưu Tập Mới',                                                  'text',  'homepage', N'Nhãn hero'),
    ('hero_title',          N'Phong Cách Của Bạn',                                              'text',  'homepage', N'Tiêu đề hero'),
    ('hero_desc',           N'Khám phá những xu hướng làm đẹp mới nhất.',                       'text',  'homepage', N'Mô tả hero'),
    ('hero_bg_color',       '#f5f0ea',                                                          'color', 'homepage', N'Màu nền hero'),
    ('new_arrivals_title',  N'Hàng Mới Về',                                                     'text',  'homepage', N'Tiêu đề section hàng mới'),
    ('best_sellers_title',  N'Bán Chạy Nhất',                                                   'text',  'homepage', N'Tiêu đề section bán chạy'),
    ('home.free_ship_threshold', '500000',                                                      'text',  'homepage', N'Ngưỡng Miễn Phí Ship (₫)'),
    ('show_new_arrivals',   '1',                                                                'bool',  'homepage', N'Hiển thị section Hàng Mới'),
    ('show_best_sellers',   '1',                                                                'bool',  'homepage', N'Hiển thị section Bán Chạy'),
    ('show_categories',     '1',                                                                'bool',  'homepage', N'Hiển thị section Danh Mục'),
    -- Social
    ('social.facebook',     'https://facebook.com/glowhub',                                     'text',  'social',   N'Facebook URL'),
    ('social.instagram',    'https://instagram.com/glowhub',                                    'text',  'social',   N'Instagram URL'),
    ('social.tiktok',       'https://tiktok.com/@glowhub',                                      'text',  'social',   N'TikTok URL'),
    -- SEO
    ('seo.title',           N'GlowHub - Thời Trang & Làm Đẹp',                                  'text',  'seo',      N'Meta Title'),
    ('seo.description',     N'GlowHub - Cửa hàng thời trang và làm đẹp hàng đầu Việt Nam',     'text',  'seo',      N'Meta Description'),
    ('seo.keywords',        N'thời trang, mỹ phẩm, làm đẹp, glowhub',                          'text',  'seo',      N'Keywords')
) AS source([Key],[Value],[Type],[Group],[Label])
ON target.[Key] = source.[Key]
WHEN NOT MATCHED THEN
    INSERT ([Key],[Value],[Type],[Group],[Label])
    VALUES (source.[Key], source.[Value], source.[Type], source.[Group], source.[Label]);
PRINT N'OK SiteSettings';

-- =============================================================================
-- 10. Vouchers
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Vouchers])
BEGIN
    INSERT INTO [dbo].[Vouchers] ([Code],[Description],[DiscountType],[DiscountValue],[MinOrderAmount],[MaxDiscount],[UsageLimit],[UsedCount],[StartDate],[ExpiryDate],[IsActive],[ShopId])
    VALUES
    -- System vouchers
    ('GLOWHUB10', N'Giảm 10% cho mọi đơn hàng',         'percent', 10,    0,      100000, NULL, 0,  GETUTCDATE(), DATEADD(DAY, 90, GETUTCDATE()), 1, NULL),
    ('NEWUSER',   N'Giảm 20% cho khách hàng mới',        'percent', 20,    0,      200000, 1,    0,  GETUTCDATE(), DATEADD(DAY, 365, GETUTCDATE()), 1, NULL),
    ('SALE50K',   N'Giảm 50.000₫ cho đơn từ 500K',       'fixed',   50000, 500000, NULL,   NULL, 0,  GETUTCDATE(), DATEADD(DAY, 60, GETUTCDATE()), 1, NULL),
    ('FREESHIP',  N'Miễn phí vận chuyển',                'fixed',   30000, 150000, NULL,   200,  3,  GETUTCDATE(), DATEADD(DAY, 30, GETUTCDATE()), 1, NULL),
    ('GLOW20',    N'Giảm 20% tối đa 100.000₫',           'percent', 20,    300000, 100000, 200,  1,  GETUTCDATE(), DATEADD(DAY, 45, GETUTCDATE()), 1, NULL),
    ('NEWBIE15',  N'Khách hàng mới - Giảm 15%',          'percent', 15,    150000, 80000,  100,  0,  GETUTCDATE(), DATEADD(DAY, 60, GETUTCDATE()), 1, NULL),
    -- Shop-specific vouchers
    ('GLOWBOUTIQUE10', N'Giảm 10% - Glow Beauty Boutique', 'percent', 10, 200000, 50000, 50, 0, GETUTCDATE(), DATEADD(DAY, 45, GETUTCDATE()), 1, @Shop1Id),
    ('LUXE15',         N'Giảm 15% - Luxe Beauty House',    'percent', 15, 500000, 150000, 30, 0, GETUTCDATE(), DATEADD(DAY, 45, GETUTCDATE()), 1, @Shop2Id),
    ('KBEAUTY50K',     N'Giảm 50K - K-Beauty Korea',       'fixed',   50000, 300000, NULL, 40, 0, GETUTCDATE(), DATEADD(DAY, 45, GETUTCDATE()), 1, @Shop3Id);
END
PRINT N'OK Vouchers - 9';

-- =============================================================================
-- 11. CustomerVouchers (saved vouchers per user)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[CustomerVouchers])
BEGIN
    DECLARE @vGlowhub10 INT = (SELECT Id FROM [dbo].[Vouchers] WHERE Code='GLOWHUB10');
    DECLARE @vNewuser   INT = (SELECT Id FROM [dbo].[Vouchers] WHERE Code='NEWUSER');
    DECLARE @vFreeship  INT = (SELECT Id FROM [dbo].[Vouchers] WHERE Code='FREESHIP');
    DECLARE @vLuxe15    INT = (SELECT Id FROM [dbo].[Vouchers] WHERE Code='LUXE15');

    INSERT INTO [dbo].[CustomerVouchers] ([UserId],[VoucherId],[SavedAt],[IsUsed]) VALUES
    (@Cust1Id, @vGlowhub10, DATEADD(DAY,-10,GETUTCDATE()), 0),
    (@Cust1Id, @vFreeship,  DATEADD(DAY,-2,GETUTCDATE()),  0),
    (@Cust1Id, @vLuxe15,    DATEADD(DAY,-1,GETUTCDATE()),  0),
    (@Cust2Id, @vGlowhub10, DATEADD(DAY,-5,GETUTCDATE()),  0),
    (@Cust2Id, @vNewuser,   DATEADD(DAY,-15,GETUTCDATE()), 1),
    (@Cust3Id, @vNewuser,   DATEADD(DAY,-3,GETUTCDATE()),  0),
    (@Cust4Id, @vFreeship,  DATEADD(DAY,-1,GETUTCDATE()),  0);
END
PRINT N'OK CustomerVouchers';

-- =============================================================================
-- 12. ShopFollows
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[ShopFollows])
BEGIN
    INSERT INTO [dbo].[ShopFollows] ([UserId],[ShopId]) VALUES
    (@Cust1Id, @Shop1Id), (@Cust1Id, @Shop2Id),
    (@Cust2Id, @Shop2Id),
    (@Cust3Id, @Shop3Id),
    (@Cust4Id, @Shop1Id), (@Cust4Id, @Shop3Id),
    (@Cust5Id, @Shop2Id);
END
PRINT N'OK ShopFollows';

-- =============================================================================
-- 13. Wishlists
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Wishlists])
BEGIN
    INSERT INTO [dbo].[Wishlists] ([CustomerId],[ProductId])
    SELECT @Cust1Id, [Id] FROM [dbo].[Products] WHERE [Id] IN (1,2,16,17);
    INSERT INTO [dbo].[Wishlists] ([CustomerId],[ProductId])
    SELECT @Cust2Id, [Id] FROM [dbo].[Products] WHERE [Id] IN (8, 12, 21);
    INSERT INTO [dbo].[Wishlists] ([CustomerId],[ProductId])
    SELECT @Cust3Id, [Id] FROM [dbo].[Products] WHERE [Id] IN (4, 6, 22);
    INSERT INTO [dbo].[Wishlists] ([CustomerId],[ProductId])
    SELECT @Cust4Id, [Id] FROM [dbo].[Products] WHERE [Id] IN (1, 7, 14);
END
PRINT N'OK Wishlists';

-- =============================================================================
-- 14. CartItems
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[CartItems])
BEGIN
    INSERT INTO [dbo].[CartItems] ([UserId],[ProductId],[Quantity]) VALUES
    (@Cust1Id, 4, 2),
    (@Cust1Id, 7, 1),
    (@Cust2Id, 9, 3),
    (@Cust2Id, 22, 1),
    (@Cust3Id, 24, 2);
END
PRINT N'OK CartItems';

-- =============================================================================
-- 15. RecentlyVieweds
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[RecentlyVieweds])
BEGIN
    INSERT INTO [dbo].[RecentlyVieweds] ([UserId],[ProductId],[ViewedAt]) VALUES
    (@Cust1Id, 1,  DATEADD(MINUTE, -30, GETUTCDATE())),
    (@Cust1Id, 2,  DATEADD(HOUR,   -2,  GETUTCDATE())),
    (@Cust1Id, 16, DATEADD(HOUR,   -5,  GETUTCDATE())),
    (@Cust2Id, 8,  DATEADD(MINUTE, -45, GETUTCDATE())),
    (@Cust2Id, 12, DATEADD(HOUR,   -3,  GETUTCDATE())),
    (@Cust3Id, 4,  DATEADD(MINUTE, -10, GETUTCDATE())),
    (@Cust3Id, 22, DATEADD(HOUR,   -1,  GETUTCDATE()));
END
PRINT N'OK RecentlyVieweds';

-- =============================================================================
-- 16. MediaFiles
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[MediaFiles])
BEGIN
    INSERT INTO [dbo].[MediaFiles] ([FileName],[OriginalName],[Url],[MimeType],[FileSize],[Width],[Height],[Alt],[Folder],[UploadedBy]) VALUES
    ('banner1.jpg',  'spring-collection.jpg', '/uploads/banner1.jpg',  'image/jpeg', 245000, 1920, 800,  N'Banner Spring Collection',     'banners', @AdminId),
    ('banner2.jpg',  'flash-sale.jpg',         '/uploads/banner2.jpg',  'image/jpeg', 198000, 1920, 800,  N'Banner Flash Sale',           'banners', @AdminId),
    ('banner3.jpg',  'perfume.jpg',            '/uploads/banner3.jpg',  'image/jpeg', 220000, 1920, 800,  N'Bộ sưu tập nước hoa',         'banners', @AdminId),
    ('p01.jpg',      'serum-vitc.jpg',         '/uploads/p01.jpg',      'image/jpeg', 88000,  600,  600,  N'Serum Vitamin C',             'products', @Seller2Id),
    ('p07.jpg',      'son-black-rouge.jpg',    '/uploads/p07.jpg',      'image/jpeg', 64000,  600,  600,  N'Son Black Rouge',             'products', @Seller3Id),
    ('shop-glow.png','glow-logo.png',          '/uploads/shop-glow.png','image/png',  18000,  200,  200,  N'Logo Glow Beauty Boutique',  'shops',    @Seller1Id);
END
PRINT N'OK MediaFiles';

-- =============================================================================
-- 17. FlashSales + FlashSaleProducts
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[FlashSales])
BEGIN
    INSERT INTO [dbo].[FlashSales] ([Name],[StartTime],[EndTime],[IsActive],[CreatedBy])
    VALUES
    (N'Flash Sale Cuối Tuần', DATEADD(HOUR, -1, GETUTCDATE()), DATEADD(HOUR, 23, GETUTCDATE()), 1, @AdminId),
    (N'Flash Sale Thứ Hai',   DATEADD(HOUR, 12, GETUTCDATE()), DATEADD(HOUR, 36, GETUTCDATE()), 1, @AdminId);

    DECLARE @fs1 INT = (SELECT Id FROM [dbo].[FlashSales] WHERE Name = N'Flash Sale Cuối Tuần');
    DECLARE @fs2 INT = (SELECT Id FROM [dbo].[FlashSales] WHERE Name = N'Flash Sale Thứ Hai');

    INSERT INTO [dbo].[FlashSaleProducts] ([FlashSaleId],[ProductId],[SalePrice],[OriginalPrice],[Quantity],[SoldCount],[RemainingQuantity],[IsActive])
    SELECT @fs1, p.[Id], ROUND(p.[Price]*0.7,0), p.[Price], 50, 15, 35, 1
    FROM [dbo].[Products] p WHERE p.[Id] IN (1, 8, 17, 22);

    INSERT INTO [dbo].[FlashSaleProducts] ([FlashSaleId],[ProductId],[SalePrice],[OriginalPrice],[Quantity],[SoldCount],[RemainingQuantity],[IsActive])
    SELECT @fs2, p.[Id], ROUND(p.[Price]*0.6,0), p.[Price], 30, 0, 30, 1
    FROM [dbo].[Products] p WHERE p.[Id] IN (3, 10, 16);
END
PRINT N'OK FlashSales + FlashSaleProducts';

-- =============================================================================
-- 18. SellerWallets (create empty wallet per shop)
-- =============================================================================
INSERT INTO [dbo].[SellerWallets] ([ShopId],[Balance],[TotalEarned],[TotalWithdrawn],[TotalRefunded],[UpdatedAt])
SELECT s.[Id], 0, 0, 0, 0, GETUTCDATE()
FROM [dbo].[Shops] s
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[SellerWallets] sw WHERE sw.[ShopId] = s.[Id]);
PRINT N'OK SellerWallets';

-- =============================================================================
-- 19. CustomerWallets
-- =============================================================================
INSERT INTO [dbo].[CustomerWallets] ([UserId],[Balance],[TotalReceived],[TotalSpent],[UpdatedAt])
SELECT u.[Id], 0, 0, 0, GETUTCDATE()
FROM [dbo].[Users] u
WHERE u.[UserType] = 0
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CustomerWallets] cw WHERE cw.[UserId] = u.[Id]);
PRINT N'OK CustomerWallets';

-- =============================================================================
-- 20. Orders + OrderDetails + SubOrders + SubOrderItems + OrderStatusHistories
--     6 orders covering: PENDING, CONFIRMED, SHIPPING, DELIVERED, CANCELLED, COMPLETED
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Orders])
BEGIN
    DECLARE @OrderId INT;

    -- ─── Order #1: Cust1 → Shop1 (DELIVERED, đã thanh toán) ─────────────────
    INSERT INTO [dbo].[Orders] ([UserId],[OrderDate],[UpdatedAt],[TotalAmount],[Status],[ShippingAddress],[Note],
        [ReceiverName],[ReceiverPhone],[PaymentMethod],[PaymentStatus],[OrderCode],[ShippingFee],[FinalAmount],
        [ShopId],[CommissionRate],[ProductRevenue],[CommissionAmount],[SellerPayoutAmount],[PayoutStatus],
        [ToProvince],[TrackingCode])
    VALUES (@Cust1Id, DATEADD(DAY,-12,GETDATE()), DATEADD(DAY,-7,GETDATE()), 760000, 'DELIVERED',
        N'12 Lê Lợi, P. Bến Nghé, Q.1, TP.HCM', N'Giao giờ hành chính',
        N'Nguyễn Thị Lan', '0912000001', 'COD', 'PAID', 'ORD-000001', 30000, 790000,
        @Shop1Id, 10.00, 760000, 76000, 684000, 'PAID',
        N'Hồ Chí Minh', 'TRK-AAA111');
    SET @OrderId = SCOPE_IDENTITY();

    INSERT INTO [dbo].[OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice]) VALUES
    (@OrderId, 5, 2, 380000); -- 2 × CeraVe

    INSERT INTO [dbo].[SubOrders] ([OrderId],[ShopId],[SubOrderCode],[Status],[TotalAmount],[ShippingFee],[FinalAmount],
        [ProductRevenue],[CommissionRate],[CommissionAmount],[SellerPayoutAmount],[PayoutStatus],[TrackingCode],[ToProvince])
    VALUES (@OrderId, @Shop1Id, 'SUB-000001-1', 'DELIVERED', 760000, 30000, 790000,
        760000, 10.00, 76000, 684000, 'PAID', 'TRK-AAA111', N'Hồ Chí Minh');
    DECLARE @sub INT = SCOPE_IDENTITY();
    INSERT INTO [dbo].[SubOrderItems] ([SubOrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@sub, 5, 2, 380000);

    INSERT INTO [dbo].[OrderStatusHistories] ([OrderId],[Status],[Note],[ChangedBy],[ChangedAt]) VALUES
    (@OrderId, 'PENDING',   N'Đơn hàng được tạo',                 @Cust1Id,   DATEADD(DAY,-12,GETUTCDATE())),
    (@OrderId, 'CONFIRMED', N'Shop xác nhận đơn',                 @Seller1Id, DATEADD(DAY,-11,GETUTCDATE())),
    (@OrderId, 'SHIPPING',  N'Đang giao hàng',                     @Seller1Id, DATEADD(DAY,-9, GETUTCDATE())),
    (@OrderId, 'DELIVERED', N'Đã giao thành công',                 @Seller1Id, DATEADD(DAY,-7, GETUTCDATE()));

    -- ─── Order #2: Cust1 → Shop2 (SHIPPING, BANK transfer) ──────────────────
    INSERT INTO [dbo].[Orders] ([UserId],[OrderDate],[TotalAmount],[Status],[ShippingAddress],
        [ReceiverName],[ReceiverPhone],[PaymentMethod],[PaymentStatus],[OrderCode],[ShippingFee],[FinalAmount],
        [ShopId],[CommissionRate],[ProductRevenue],[CommissionAmount],[SellerPayoutAmount],[PayoutStatus],
        [ToProvince],[TrackingCode],[BankTransferConfirmedAt],[BankTransferConfirmedBy])
    VALUES (@Cust1Id, DATEADD(DAY,-3,GETDATE()), 3200000, 'SHIPPING',
        N'12 Lê Lợi, P. Bến Nghé, Q.1, TP.HCM',
        N'Nguyễn Thị Lan', '0912000001', 'BANK', 'PAID', 'ORD-000002', 35000, 3235000,
        @Shop2Id, 12.00, 3200000, 384000, 2816000, 'PENDING',
        N'Hồ Chí Minh', 'TRK-BBB222', DATEADD(DAY,-2,GETUTCDATE()), @AdminId);
    SET @OrderId = SCOPE_IDENTITY();

    INSERT INTO [dbo].[OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@OrderId, 1, 1, 3200000);
    INSERT INTO [dbo].[SubOrders] ([OrderId],[ShopId],[SubOrderCode],[Status],[TotalAmount],[ShippingFee],[FinalAmount],
        [ProductRevenue],[CommissionRate],[CommissionAmount],[SellerPayoutAmount],[PayoutStatus],[TrackingCode],[ToProvince])
    VALUES (@OrderId, @Shop2Id, 'SUB-000002-1', 'SHIPPING', 3200000, 35000, 3235000,
        3200000, 12.00, 384000, 2816000, 'PENDING', 'TRK-BBB222', N'Hồ Chí Minh');
    SET @sub = SCOPE_IDENTITY();
    INSERT INTO [dbo].[SubOrderItems] ([SubOrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@sub, 1, 1, 3200000);

    INSERT INTO [dbo].[OrderStatusHistories] ([OrderId],[Status],[Note],[ChangedBy],[ChangedAt]) VALUES
    (@OrderId, 'PENDING',   N'Đơn hàng được tạo',     @Cust1Id,   DATEADD(DAY,-3,GETUTCDATE())),
    (@OrderId, 'CONFIRMED', N'Đã thanh toán bank',     @AdminId,   DATEADD(DAY,-2,GETUTCDATE())),
    (@OrderId, 'SHIPPING',  N'Đang vận chuyển',         @Seller2Id, DATEADD(DAY,-1,GETUTCDATE()));

    -- ─── Order #3: Cust2 → Shop3 (CONFIRMED) ────────────────────────────────
    INSERT INTO [dbo].[Orders] ([UserId],[OrderDate],[TotalAmount],[Status],[ShippingAddress],
        [ReceiverName],[ReceiverPhone],[PaymentMethod],[PaymentStatus],[OrderCode],[ShippingFee],[FinalAmount],
        [ShopId],[CommissionRate],[ProductRevenue],[CommissionAmount],[SellerPayoutAmount],[ToProvince])
    VALUES (@Cust2Id, DATEADD(DAY,-1,GETDATE()), 990000, 'CONFIRMED',
        N'88 Trần Thái Tông, Dịch Vọng, Cầu Giấy, Hà Nội',
        N'Lê Minh Hà', '0912000002', 'COD', 'UNPAID', 'ORD-000003', 30000, 1020000,
        @Shop3Id, 8.00, 990000, 79200, 910800, N'Hà Nội');
    SET @OrderId = SCOPE_IDENTITY();

    INSERT INTO [dbo].[OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice]) VALUES
    (@OrderId, 4, 1, 420000),
    (@OrderId, 24,2, 285000);

    INSERT INTO [dbo].[SubOrders] ([OrderId],[ShopId],[SubOrderCode],[Status],[TotalAmount],[ShippingFee],[FinalAmount],
        [ProductRevenue],[CommissionRate],[CommissionAmount],[SellerPayoutAmount],[ToProvince])
    VALUES (@OrderId, @Shop3Id, 'SUB-000003-1', 'CONFIRMED', 990000, 30000, 1020000,
        990000, 8.00, 79200, 910800, N'Hà Nội');
    SET @sub = SCOPE_IDENTITY();
    INSERT INTO [dbo].[SubOrderItems] ([SubOrderId],[ProductId],[Quantity],[UnitPrice]) VALUES
    (@sub, 4, 1, 420000), (@sub, 24, 2, 285000);

    INSERT INTO [dbo].[OrderStatusHistories] ([OrderId],[Status],[Note],[ChangedBy],[ChangedAt]) VALUES
    (@OrderId, 'PENDING',   N'Đơn hàng được tạo',  @Cust2Id,   DATEADD(DAY,-1,GETUTCDATE())),
    (@OrderId, 'CONFIRMED', N'Shop xác nhận',       @Seller3Id, GETUTCDATE());

    -- ─── Order #4: Cust3 → Shop2 (PENDING - chờ thanh toán VNPay) ────────────
    INSERT INTO [dbo].[Orders] ([UserId],[OrderDate],[TotalAmount],[Status],[ShippingAddress],
        [ReceiverName],[ReceiverPhone],[PaymentMethod],[PaymentStatus],[OrderCode],[ShippingFee],[FinalAmount],
        [ShopId],[CommissionRate],[ProductRevenue],[CommissionAmount],[SellerPayoutAmount],
        [ToProvince],[PaymentExpireAt])
    VALUES (@Cust3Id, DATEADD(MINUTE,-30,GETDATE()), 2750000, 'PENDING',
        N'56 Nguyễn Văn Linh, Hải Châu, Đà Nẵng',
        N'Đỗ Hoàng Khoa', '0912000003', 'VNPAY', 'UNPAID', 'ORD-000004', 40000, 2790000,
        @Shop2Id, 12.00, 2750000, 330000, 2420000,
        N'Đà Nẵng', DATEADD(MINUTE, 30, GETUTCDATE()));
    SET @OrderId = SCOPE_IDENTITY();
    INSERT INTO [dbo].[OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@OrderId, 2, 1, 2750000);
    INSERT INTO [dbo].[SubOrders] ([OrderId],[ShopId],[SubOrderCode],[Status],[TotalAmount],[ShippingFee],[FinalAmount],
        [ProductRevenue],[CommissionRate],[CommissionAmount],[SellerPayoutAmount],[ToProvince])
    VALUES (@OrderId, @Shop2Id, 'SUB-000004-1', 'PENDING', 2750000, 40000, 2790000,
        2750000, 12.00, 330000, 2420000, N'Đà Nẵng');
    SET @sub = SCOPE_IDENTITY();
    INSERT INTO [dbo].[SubOrderItems] ([SubOrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@sub, 2, 1, 2750000);
    INSERT INTO [dbo].[OrderStatusHistories] ([OrderId],[Status],[Note],[ChangedBy]) VALUES
    (@OrderId, 'PENDING', N'Chờ thanh toán VNPay', @Cust3Id);

    -- ─── Order #5: Cust4 → Shop1 (CANCELLED) ────────────────────────────────
    INSERT INTO [dbo].[Orders] ([UserId],[OrderDate],[UpdatedAt],[TotalAmount],[Status],[ShippingAddress],[Note],
        [ReceiverName],[ReceiverPhone],[PaymentMethod],[PaymentStatus],[OrderCode],[ShippingFee],[FinalAmount],
        [ShopId],[CommissionRate],[CancelReason],[ToProvince])
    VALUES (@Cust4Id, DATEADD(DAY,-5,GETDATE()), DATEADD(DAY,-4,GETDATE()), 440000, 'CANCELLED',
        N'200 Lê Văn Sỹ, Phường Võ Thị Sáu, Q.3, TP.HCM', N'Khách hàng đổi ý',
        N'Vũ Thanh Trúc', '0912000004', 'COD', 'UNPAID', 'ORD-000005', 30000, 470000,
        @Shop1Id, 10.00, N'Khách hàng yêu cầu hủy', N'Hồ Chí Minh');
    SET @OrderId = SCOPE_IDENTITY();
    INSERT INTO [dbo].[OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@OrderId, 9, 2, 220000);
    INSERT INTO [dbo].[SubOrders] ([OrderId],[ShopId],[SubOrderCode],[Status],[TotalAmount],[ShippingFee],[FinalAmount],
        [ProductRevenue],[CommissionRate],[CancelReason],[ToProvince])
    VALUES (@OrderId, @Shop1Id, 'SUB-000005-1', 'CANCELLED', 440000, 30000, 470000,
        440000, 10.00, N'Khách hàng yêu cầu hủy', N'Hồ Chí Minh');
    SET @sub = SCOPE_IDENTITY();
    INSERT INTO [dbo].[SubOrderItems] ([SubOrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@sub, 9, 2, 220000);
    INSERT INTO [dbo].[OrderStatusHistories] ([OrderId],[Status],[Note],[ChangedBy],[ChangedAt]) VALUES
    (@OrderId, 'PENDING',   N'Đơn hàng được tạo',           @Cust4Id, DATEADD(DAY,-5,GETUTCDATE())),
    (@OrderId, 'CANCELLED', N'Khách hàng yêu cầu hủy',      @Cust4Id, DATEADD(DAY,-4,GETUTCDATE()));

    -- ─── Order #6: Cust5 → Shop3 (COMPLETED + đã payout) ───────────────────
    INSERT INTO [dbo].[Orders] ([UserId],[OrderDate],[UpdatedAt],[TotalAmount],[Status],[ShippingAddress],
        [ReceiverName],[ReceiverPhone],[PaymentMethod],[PaymentStatus],[OrderCode],[ShippingFee],[FinalAmount],
        [ShopId],[CommissionRate],[ProductRevenue],[CommissionAmount],[SellerPayoutAmount],[PayoutStatus],
        [ToProvince],[TrackingCode],[WalletReleaseAt])
    VALUES (@Cust5Id, DATEADD(DAY,-20,GETDATE()), DATEADD(DAY,-12,GETDATE()), 855000, 'COMPLETED',
        N'15 Trần Hưng Đạo, Tân An, Ninh Kiều, Cần Thơ',
        N'Bùi Thu Hằng', '0912000005', 'COD', 'PAID', 'ORD-000006', 35000, 890000,
        @Shop3Id, 8.00, 855000, 68400, 786600, 'PAID',
        N'Cần Thơ', 'TRK-CCC333', DATEADD(DAY,-5,GETUTCDATE()));
    SET @OrderId = SCOPE_IDENTITY();
    INSERT INTO [dbo].[OrderDetails] ([OrderId],[ProductId],[Quantity],[UnitPrice]) VALUES
    (@OrderId, 7, 3, 285000); -- 3 × Black Rouge

    INSERT INTO [dbo].[SubOrders] ([OrderId],[ShopId],[SubOrderCode],[Status],[TotalAmount],[ShippingFee],[FinalAmount],
        [ProductRevenue],[CommissionRate],[CommissionAmount],[SellerPayoutAmount],[PayoutStatus],
        [TrackingCode],[ToProvince],[WalletReleaseAt])
    VALUES (@OrderId, @Shop3Id, 'SUB-000006-1', 'COMPLETED', 855000, 35000, 890000,
        855000, 8.00, 68400, 786600, 'PAID',
        'TRK-CCC333', N'Cần Thơ', DATEADD(DAY,-5,GETUTCDATE()));
    SET @sub = SCOPE_IDENTITY();
    INSERT INTO [dbo].[SubOrderItems] ([SubOrderId],[ProductId],[Quantity],[UnitPrice]) VALUES (@sub, 7, 3, 285000);

    INSERT INTO [dbo].[OrderStatusHistories] ([OrderId],[Status],[Note],[ChangedBy],[ChangedAt]) VALUES
    (@OrderId, 'PENDING',   N'Đơn hàng được tạo',          @Cust5Id,   DATEADD(DAY,-20,GETUTCDATE())),
    (@OrderId, 'CONFIRMED', N'Shop xác nhận',               @Seller3Id, DATEADD(DAY,-19,GETUTCDATE())),
    (@OrderId, 'SHIPPING',  N'Đang giao hàng',               @Seller3Id, DATEADD(DAY,-17,GETUTCDATE())),
    (@OrderId, 'DELIVERED', N'Khách đã nhận hàng',          @Seller3Id, DATEADD(DAY,-12,GETUTCDATE())),
    (@OrderId, 'COMPLETED', N'Đơn hoàn tất, đã thanh toán', @AdminId,   DATEADD(DAY,-5, GETUTCDATE()));
END
PRINT N'OK Orders + OrderDetails + SubOrders + OrderStatusHistories';

-- =============================================================================
-- 21. OrderStatusLogs (admin status-change audit)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[OrderStatusLogs])
BEGIN
    DECLARE @ord1 INT = (SELECT Id FROM [dbo].[Orders] WHERE OrderCode='ORD-000001');
    DECLARE @ord6 INT = (SELECT Id FROM [dbo].[Orders] WHERE OrderCode='ORD-000006');

    INSERT INTO [dbo].[OrderStatusLogs] ([OrderId],[OldStatus],[NewStatus],[ChangedBy],[Note],[CreatedAt]) VALUES
    (@ord1, 'PENDING',   'CONFIRMED', @Seller1Id, N'Shop xác nhận',          DATEADD(DAY,-11,GETDATE())),
    (@ord1, 'CONFIRMED', 'SHIPPING',  @Seller1Id, N'Đã giao đơn vị VC',     DATEADD(DAY,-9, GETDATE())),
    (@ord1, 'SHIPPING',  'DELIVERED', @Seller1Id, N'Giao thành công',         DATEADD(DAY,-7, GETDATE())),
    (@ord6, 'DELIVERED', 'COMPLETED', @AdminId,   N'Đơn hoàn tất, đã payout', DATEADD(DAY,-5, GETDATE()));
END
PRINT N'OK OrderStatusLogs';

-- =============================================================================
-- 22. Reviews (cho các đơn DELIVERED/COMPLETED)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Reviews])
BEGIN
    INSERT INTO [dbo].[Reviews] ([ProductId],[UserId],[Rating],[Comment],[SellerReply],[ReplyAt],[Images],[IsVerifiedPurchase],[CreatedAt]) VALUES
    (5, @Cust1Id, 5, N'Sữa rửa mặt rất dịu nhẹ, dùng cho da nhạy cảm rất ok. Sẽ mua lại!',     N'Cảm ơn bạn đã ủng hộ shop ❤️', DATEADD(DAY,-5,GETDATE()), NULL, 1, DATEADD(DAY,-6,GETDATE())),
    (7, @Cust5Id, 5, N'Son lì siêu đẹp, lâu trôi, màu chuẩn như ảnh.',                          N'Shop cảm ơn bạn 🥰',            DATEADD(DAY,-4,GETDATE()), NULL, 1, DATEADD(DAY,-5,GETDATE())),
    (7, @Cust2Id, 4, N'Son đẹp nhưng hơi khô môi sau vài tiếng.',                                NULL, NULL, NULL, 0, DATEADD(DAY,-2,GETDATE())),
    (4, @Cust3Id, 5, N'Toner siêu đỉnh, da sáng hẳn sau 2 tuần dùng.',                          NULL, NULL, NULL, 0, DATEADD(DAY,-1,GETDATE())),
    (1, @Cust4Id, 5, N'Serum đắt nhưng xứng đáng từng đồng. Da căng bóng rõ rệt.',              NULL, NULL, NULL, 0, DATEADD(DAY,-3,GETDATE()));
END
PRINT N'OK Reviews';

-- =============================================================================
-- 23. QnA
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[QnA])
BEGIN
    INSERT INTO [dbo].[QnA] ([ProductId],[CustomerId],[Question],[AskedAt],[Answer],[AnsweredAt],[IsActive]) VALUES
    (1, @Cust2Id, N'Serum này dùng được cho da nhạy cảm không ạ?', DATEADD(DAY,-4,GETUTCDATE()),
                 N'Dạ phù hợp ạ, nhưng nên patch test ở vùng nhỏ trước khi dùng toàn mặt.', DATEADD(DAY,-3,GETUTCDATE()), 1),
    (7, @Cust3Id, N'Shop còn màu MLBB không ạ?',                    DATEADD(DAY,-2,GETUTCDATE()),
                 N'Dạ shop còn đủ màu, bạn inbox shop để được tư vấn nhé.', DATEADD(DAY,-1,GETUTCDATE()), 1),
    (16,@Cust1Id, N'Chanel N°5 mua tặng mẹ có hợp không ạ?',         DATEADD(DAY,-1,GETUTCDATE()),
                  NULL, NULL, 1);
END
PRINT N'OK QnA';

-- =============================================================================
-- 24. Notifications
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Notifications])
BEGIN
    INSERT INTO [dbo].[Notifications] ([UserId],[Type],[Title],[Message],[IsRead],[Link],[CreatedAt]) VALUES
    (@Cust1Id, 1, N'Đơn ORD-000001 đã giao',          N'Đơn hàng của bạn đã được giao thành công.',    1, '/orders.html?id=1', DATEADD(DAY,-7,GETUTCDATE())),
    (@Cust1Id, 2, N'Voucher GLOWHUB10 đã sẵn sàng',   N'Voucher mới đã được thêm vào ví của bạn.',     0, '/vouchers.html',     DATEADD(DAY,-2,GETUTCDATE())),
    (@Cust2Id, 0, N'Chào mừng đến với GlowHub',        N'Cảm ơn bạn đã đăng ký. Nhận ngay voucher NEWUSER giảm 20%.', 0, '/vouchers.html', DATEADD(DAY,-15,GETUTCDATE())),
    (@Cust3Id, 3, N'Đơn ORD-000004 đang chờ thanh toán', N'Vui lòng hoàn tất thanh toán trong 30 phút.', 0, '/orders.html?id=4', DATEADD(MINUTE,-30,GETUTCDATE())),
    (@Seller1Id, 4, N'Đơn ORD-000001 đã được thanh toán', N'Số dư ví của bạn đã được cộng thêm 684.000₫.', 1, '/seller.html#wallet', DATEADD(DAY,-7,GETUTCDATE())),
    (@Seller3Id, 4, N'Đã payout đơn ORD-000006',       N'Đã chuyển 786.600₫ vào ví của shop.',            0, '/seller.html#wallet', DATEADD(DAY,-5,GETUTCDATE())),
    (@AdminId,   5, N'Có 1 tranh chấp mới',            N'Khách hàng đã mở khiếu nại đơn ORD-000005.',    0, '/admin.html#disputes', DATEADD(DAY,-3,GETUTCDATE()));
END
PRINT N'OK Notifications';

-- =============================================================================
-- 25. WalletTransactions + cập nhật SellerWallets từ order DELIVERED/COMPLETED
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[WalletTransactions])
BEGIN
    DECLARE @ord1Id INT = (SELECT Id FROM [dbo].[Orders] WHERE OrderCode='ORD-000001');
    DECLARE @ord6Id INT = (SELECT Id FROM [dbo].[Orders] WHERE OrderCode='ORD-000006');

    -- Shop1 earning từ Order #1
    INSERT INTO [dbo].[WalletTransactions] ([ShopId],[OrderId],[Type],[Amount],[BalanceBefore],[BalanceAfter],[Note],[CreatedAt])
    VALUES (@Shop1Id, @ord1Id, 'EARNING', 684000, 0, 684000, N'Earning từ ORD-000001', DATEADD(DAY,-7,GETUTCDATE()));

    UPDATE [dbo].[SellerWallets] SET [Balance]=684000, [TotalEarned]=684000, [UpdatedAt]=GETUTCDATE() WHERE [ShopId]=@Shop1Id;

    -- Shop3 earning + withdrawal từ Order #6
    INSERT INTO [dbo].[WalletTransactions] ([ShopId],[OrderId],[Type],[Amount],[BalanceBefore],[BalanceAfter],[Note],[CreatedAt])
    VALUES
    (@Shop3Id, @ord6Id, 'EARNING',    786600, 0,      786600, N'Earning từ ORD-000006',         DATEADD(DAY,-12,GETUTCDATE())),
    (@Shop3Id, NULL,    'WITHDRAWAL', 500000, 786600, 286600, N'Admin payout shop K-Beauty',   DATEADD(DAY,-5, GETUTCDATE()));

    UPDATE [dbo].[SellerWallets] SET [Balance]=286600, [TotalEarned]=786600, [TotalWithdrawn]=500000, [UpdatedAt]=GETUTCDATE() WHERE [ShopId]=@Shop3Id;
END
PRINT N'OK WalletTransactions';

-- =============================================================================
-- 26. PayoutHistories
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[PayoutHistories])
BEGIN
    INSERT INTO [dbo].[PayoutHistories] ([ShopId],[Amount],[Note],[PayoutDate],[ProcessedBy],[CreatedAt])
    VALUES
    (@Shop3Id, 500000, N'Payout đợt 1 cho K-Beauty Korea', DATEADD(DAY,-5,GETUTCDATE()), @AdminId, DATEADD(DAY,-5,GETUTCDATE()));
END
PRINT N'OK PayoutHistories';

-- =============================================================================
-- 27. CustomerWalletTransactions (giả lập cashback)
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[CustomerWalletTransactions])
BEGIN
    DECLARE @ord1B INT = (SELECT Id FROM [dbo].[Orders] WHERE OrderCode='ORD-000001');

    INSERT INTO [dbo].[CustomerWalletTransactions] ([UserId],[Type],[Amount],[BalanceBefore],[BalanceAfter],[Note],[OrderId],[CreatedAt])
    VALUES
    (@Cust1Id, 'CASHBACK', 10000, 0,     10000, N'Hoàn tiền 1% từ ORD-000001', @ord1B, DATEADD(DAY,-7,GETUTCDATE())),
    (@Cust1Id, 'TOPUP',    50000, 10000, 60000, N'Nạp ví khuyến mãi',          NULL,   DATEADD(DAY,-3,GETUTCDATE()));

    UPDATE [dbo].[CustomerWallets] SET [Balance]=60000, [TotalReceived]=60000, [UpdatedAt]=GETUTCDATE() WHERE [UserId]=@Cust1Id;
END
PRINT N'OK CustomerWalletTransactions';

-- =============================================================================
-- 28. Disputes
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[Disputes])
BEGIN
    DECLARE @ord5Id INT = (SELECT Id FROM [dbo].[Orders] WHERE OrderCode='ORD-000005');

    INSERT INTO [dbo].[Disputes] ([OrderId],[CustomerId],[Reason],[Description],[Status],[RefundAmount],[FavorCustomer],[CreatedAt])
    VALUES (@ord5Id, @Cust4Id, N'Yêu cầu hoàn tiền',
            N'Khách hàng đã hủy đơn nhưng chưa nhận lại tiền cọc.', 'OPEN', 0, 0, DATEADD(DAY,-3,GETUTCDATE()));
END
PRINT N'OK Disputes';

-- =============================================================================
-- 29. AuditLogs
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[AuditLogs])
BEGIN
    INSERT INTO [dbo].[AuditLogs] ([UserId],[UserName],[Action],[Entity],[EntityId],[OldValue],[NewValue],[IpAddress],[CreatedAt]) VALUES
    (@AdminId,   'admin',       'LOGIN',  'Auth',   NULL, NULL, NULL,                                              '127.0.0.1', DATEADD(DAY,-2,GETUTCDATE())),
    (@AdminId,   'admin',       'UPDATE', 'Product','7',  '{"price":290000}', '{"price":285000}',                  '127.0.0.1', DATEADD(DAY,-2,GETUTCDATE())),
    (@AdminId,   'admin',       'CREATE', 'Banner', '1',  NULL, N'{"title":"New Collection"}',                     '127.0.0.1', DATEADD(DAY,-5,GETUTCDATE())),
    (@Seller1Id, 'seller_mai',  'UPDATE', 'Order',  '1',  '{"status":"SHIPPING"}', '{"status":"DELIVERED"}',       '127.0.0.1', DATEADD(DAY,-7,GETUTCDATE())),
    (@AdminId,   'admin',       'CREATE', 'Payout', '1',  NULL, N'{"shopId":"shop3","amount":500000}',             '127.0.0.1', DATEADD(DAY,-5,GETUTCDATE()));
END
PRINT N'OK AuditLogs';

COMMIT TRANSACTION;

-- =============================================================================
-- Summary report
-- =============================================================================
PRINT N'';
PRINT N'====================================================';
PRINT N'  GlowHub seed data installed successfully!';
PRINT N'====================================================';
PRINT N'  Login any account with password = 123456';
PRINT N'    admin       (Admin)';
PRINT N'    seller_mai / seller_long / seller_ngoc (Sellers)';
PRINT N'    lan.nguyen / ha.le / khoa.do / truc.vu / hang.bui (Customers)';
PRINT N'====================================================';
PRINT N'';

SELECT
    t.name                                              AS [Table],
    SUM(CASE WHEN i.index_id IN (0,1) THEN p.rows END)  AS [Rows]
FROM sys.tables t
LEFT JOIN sys.partitions p ON t.object_id = p.object_id
LEFT JOIN sys.indexes    i ON p.object_id = i.object_id AND p.index_id = i.index_id
WHERE t.name <> '__EFMigrationsHistory'
GROUP BY t.name
ORDER BY t.name;

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT N'LỖI: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO
