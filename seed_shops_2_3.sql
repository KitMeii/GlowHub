-- ============================================================
--  SEED: Shop 2 — Luxe Beauty House  +  Shop 3 — Nature & Bloom
--  Password mặc định tất cả seller: 123456 (MD5)
--  Chạy 1 lần duy nhất — dùng IF NOT EXISTS để idempotent
-- ============================================================

-- ── IDs cố định để dễ tham chiếu ───────────────────────────
DECLARE @SellerLuxeId  NVARCHAR(450) = 'A1B2C3D4-0001-0001-0001-000000000001';
DECLARE @SellerNatureId NVARCHAR(450) = 'A1B2C3D4-0002-0002-0002-000000000002';
DECLARE @ShopLuxeId    NVARCHAR(450) = 'B2C3D4E5-0001-0001-0001-000000000001';
DECLARE @ShopNatureId  NVARCHAR(450) = 'B2C3D4E5-0002-0002-0002-000000000002';
DECLARE @Now           DATETIME2     = GETUTCDATE();

-- ============================================================
--  1. TẠO SELLER USERS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @SellerLuxeId)
BEGIN
    INSERT INTO Users (Id, Name, UserName, Password, Salt, Contact, Email, Phone, Position, Image, IsActive, UserType, Created)
    VALUES (
        @SellerLuxeId,
        N'Luxe Beauty House',
        'luxe_seller',
        'e10adc3949ba59abbe56e057f20f883e',  -- MD5("123456")
        0x,
        '',
        'luxe@glowhub.vn',
        '0912345001',
        '',
        '',
        1,
        2,   -- Seller
        @Now
    );
    PRINT 'Created seller: luxe_seller';
END

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @SellerNatureId)
BEGIN
    INSERT INTO Users (Id, Name, UserName, Password, Salt, Contact, Email, Phone, Position, Image, IsActive, UserType, Created)
    VALUES (
        @SellerNatureId,
        N'Nature & Bloom',
        'nature_seller',
        'e10adc3949ba59abbe56e057f20f883e',  -- MD5("123456")
        0x,
        '',
        'nature@glowhub.vn',
        '0912345002',
        '',
        '',
        1,
        2,   -- Seller
        @Now
    );
    PRINT 'Created seller: nature_seller';
END

-- ============================================================
--  2. TẠO SHOPS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Shops WHERE Id = @ShopLuxeId)
BEGIN
    INSERT INTO Shops (Id, SellerId, ShopName, Description, Logo, Address, Phone, Status, CommissionRate, Province, Region, CreatedAt)
    VALUES (
        @ShopLuxeId,
        @SellerLuxeId,
        N'Luxe Beauty House',
        N'Chuyên cung cấp mỹ phẩm cao cấp chính hãng: skincare, makeup và nước hoa từ các thương hiệu quốc tế hàng đầu.',
        'https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?w=200',
        N'45 Hàng Bài, Hoàn Kiếm, Hà Nội',
        '0912345001',
        1,   -- Active
        10.0,
        N'Hà Nội',
        'NORTH',
        @Now
    );
    PRINT 'Created shop: Luxe Beauty House';
END

IF NOT EXISTS (SELECT 1 FROM Shops WHERE Id = @ShopNatureId)
BEGIN
    INSERT INTO Shops (Id, SellerId, ShopName, Description, Logo, Address, Phone, Status, CommissionRate, Province, Region, CreatedAt)
    VALUES (
        @ShopNatureId,
        @SellerNatureId,
        N'Nature & Bloom',
        N'Mỹ phẩm thiên nhiên thuần chay — dưỡng thể, chăm sóc tóc và kem chống nắng từ các thương hiệu thân thiện môi trường.',
        'https://images.unsplash.com/photo-1607613009820-a29f7bb81c04?w=200',
        N'78 Lê Lợi, Quận 1, TP.HCM',
        '0912345002',
        1,   -- Active
        10.0,
        N'TP.HCM',
        'SOUTH',
        @Now
    );
    PRINT 'Created shop: Nature & Bloom';
END

-- ============================================================
--  3. SELLER WALLETS
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM SellerWallets WHERE ShopId = @ShopLuxeId)
BEGIN
    INSERT INTO SellerWallets (ShopId, Balance, TotalEarned, TotalWithdrawn, TotalRefunded, UpdatedAt)
    VALUES (@ShopLuxeId, 0, 0, 0, 0, @Now);
END

IF NOT EXISTS (SELECT 1 FROM SellerWallets WHERE ShopId = @ShopNatureId)
BEGIN
    INSERT INTO SellerWallets (ShopId, Balance, TotalEarned, TotalWithdrawn, TotalRefunded, UpdatedAt)
    VALUES (@ShopNatureId, 0, 0, 0, 0, @Now);
END

-- ============================================================
--  4. SẢN PHẨM SHOP 2 — Luxe Beauty House (15 sản phẩm)
--     Categories: 1=Chăm sóc da mặt, 2=Trang điểm, 4=Nước hoa
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Products WHERE ShopId = @ShopLuxeId)
BEGIN
    -- Chăm sóc da mặt (CategoryId = 1)
    INSERT INTO Products (Name, Price, DiscountPrice, Stock, ImageUrl, Description, CategoryId, IsActive, IsNew, WeightGram, ShopId, CreatedAt)
    VALUES
    (N'Sulwhasoo First Care Activating Serum 60ml', 1980000, 1780000, 45,
     'https://images.pexels.com/photos/3762879/pexels-photo-3762879.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Serum dưỡng tinh chất Nhân Sâm Quý phái của Sulwhasoo — tăng cường hàng rào bảo vệ da và cải thiện kết cấu da mịn màng.', 1, 1, 1, 120, @ShopLuxeId, @Now),

    (N'Laneige Water Bank Blue Hyaluronic Cream 50ml', 890000, 790000, 60,
     'https://images.pexels.com/photos/4041392/pexels-photo-4041392.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem dưỡng ẩm chuyên sâu với Hyaluronic Acid đa tầng — cấp nước 72 giờ, da căng mướt suốt cả ngày.', 1, 1, 1, 200, @ShopLuxeId, @Now),

    (N'Tatcha The Water Cream Oil-Free Moisturizer 50ml', 1650000, NULL, 30,
     'https://images.pexels.com/photos/6621462/pexels-photo-6621462.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem dưỡng ẩm không dầu với chiết xuất Hadasei-3™ độc quyền — kiểm soát bóng nhờn, thu nhỏ lỗ chân lông.', 1, 1, 0, 180, @ShopLuxeId, @Now),

    (N'Paula''s Choice CLEAR Regular Strength Serum BHA 30ml', 780000, 700000, 80,
     'https://images.pexels.com/photos/7755235/pexels-photo-7755235.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Serum BHA 2% tẩy da chết hóa học — thông tắc lỗ chân lông, giảm mụn đầu đen và mụn viêm.', 1, 1, 0, 100, @ShopLuxeId, @Now),

    (N'Drunk Elephant C-Firma Day Serum 30ml', 2100000, 1890000, 25,
     'https://images.pexels.com/photos/3735641/pexels-photo-3735641.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Serum Vitamin C 15% ổn định với Ferulic Acid — chống oxy hóa, làm sáng và chống lão hóa sâu.', 1, 1, 1, 90, @ShopLuxeId, @Now),

    (N'Kiehl''s Ultra Facial Cream SPF 30 50ml', 1200000, NULL, 55,
     'https://images.pexels.com/photos/5473182/pexels-photo-5473182.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem dưỡng ẩm toàn diện SPF 30 — bảo vệ da khỏi tia UV và giữ ẩm tối ưu trong 24 giờ.', 1, 1, 0, 170, @ShopLuxeId, @Now),

    -- Trang điểm (CategoryId = 2)
    (N'Yves Saint Laurent All Hours Foundation 25ml', 1480000, 1300000, 40,
     'https://images.pexels.com/photos/2533266/pexels-photo-2533266.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem nền full coverage 24H, chống thấm nước — che phủ hoàn hảo, không oxy hóa suốt cả ngày.', 2, 1, 0, 150, @ShopLuxeId, @Now),

    (N'Armani Luminous Silk Foundation 30ml', 1650000, NULL, 35,
     'https://images.pexels.com/photos/2697787/pexels-photo-2697787.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem nền lụa mịn huyền thoại — độ che phủ vừa phải, tạo hiệu ứng skin tint tự nhiên rạng rỡ.', 2, 1, 1, 140, @ShopLuxeId, @Now),

    (N'Too Faced Better Than Sex Mascara 8ml', 680000, 600000, 90,
     'https://images.pexels.com/photos/3373736/pexels-photo-3373736.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Mascara làm dày và dài mi huyền thoại — công thức collagen dưỡng mi, không vón cục, bền màu 24H.', 2, 1, 0, 60, @ShopLuxeId, @Now),

    (N'Urban Decay All Nighter Setting Spray 118ml', 820000, 740000, 70,
     'https://images.pexels.com/photos/6621467/pexels-photo-6621467.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Xịt khóa makeup 24H — Temperature Control Technology giúp lớp trang điểm bền vẹn qua mọi thời tiết.', 2, 1, 0, 200, @ShopLuxeId, @Now),

    (N'Benefit Hoola Bronzer 8g', 950000, NULL, 45,
     'https://images.pexels.com/photos/4959620/pexels-photo-4959620.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Phấn bronzer huyền thoại — tạo hiệu ứng da rám nắng tự nhiên, phù hợp mọi tông da.', 2, 1, 0, 80, @ShopLuxeId, @Now),

    -- Nước hoa (CategoryId = 4)
    (N'Tom Ford Black Orchid EDP 50ml', 4500000, 4100000, 20,
     'https://images.pexels.com/photos/1190829/pexels-photo-1190829.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Hương thơm huyền bí với Hắc Lan và gia vị phương Đông — biểu tượng của sự sang trọng tuyệt đối.', 4, 1, 0, 250, @ShopLuxeId, @Now),

    (N'Maison Margiela Replica Beach Walk EDT 100ml', 3800000, 3500000, 15,
     'https://images.pexels.com/photos/965989/pexels-photo-965989.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Hương biển hạ nhiệt — dừa, tiêu trắng và gỗ tếch tái hiện cảm giác bước trên bãi biển Hawaii.', 4, 1, 1, 300, @ShopLuxeId, @Now),

    (N'Byredo Bal d''Afrique EDP 50ml', 4200000, NULL, 12,
     'https://images.pexels.com/photos/3825527/pexels-photo-3825527.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Hương Phi Châu ấm áp — cúc vàng Châu Phi, gỗ tuyết tùng và xạ hương trắng tạo nên nốt hương độc đáo.', 4, 1, 1, 270, @ShopLuxeId, @Now),

    (N'Parfums de Marly Delina EDP 75ml', 5200000, 4800000, 18,
     'https://images.pexels.com/photos/755992/pexels-photo-755992.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Hương hoa mẫu đơn Thổ Nhĩ Kỳ kết hợp lychee và hương vải tinh tế — nữ tính và quyến rũ không thể cưỡng.', 4, 1, 1, 280, @ShopLuxeId, @Now);

    PRINT 'Inserted 15 products for Luxe Beauty House';
END

-- ============================================================
--  5. SẢN PHẨM SHOP 3 — Nature & Bloom (15 sản phẩm)
--     Categories: 3=Chăm sóc cơ thể, 5=Chăm sóc tóc, 6=Chống nắng
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Products WHERE ShopId = @ShopNatureId)
BEGIN
    -- Chăm sóc cơ thể (CategoryId = 3)
    INSERT INTO Products (Name, Price, DiscountPrice, Stock, ImageUrl, Description, CategoryId, IsActive, IsNew, WeightGram, ShopId, CreatedAt)
    VALUES
    (N'Nécessaire The Body Serum 100ml', 980000, 880000, 50,
     'https://images.pexels.com/photos/6621471/pexels-photo-6621471.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Serum dưỡng thể với Niacinamide 5% và Hyaluronic Acid — dưỡng trắng, làm đều màu da và cấp ẩm chuyên sâu.', 3, 1, 1, 300, @ShopNatureId, @Now),

    (N'Sol de Janeiro Brazilian Bum Bum Cream 240ml', 1250000, 1100000, 40,
     'https://images.pexels.com/photos/4202325/pexels-photo-4202325.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem dưỡng thể huyền thoại Brazil — Guaraná và Açaí nuôi dưỡng da săn chắc, thơm ngọt ngào suốt 24H.', 3, 1, 1, 450, @ShopNatureId, @Now),

    (N'Frank Body Original Coffee Scrub 200g', 480000, 420000, 75,
     'https://images.pexels.com/photos/3685530/pexels-photo-3685530.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Tẩy da chết cơ thể với bã cà phê hữu cơ — giảm cellulite, làm mịn da và kích thích tuần hoàn máu.', 3, 1, 0, 350, @ShopNatureId, @Now),

    (N'Aesop Resurrection Aromatique Hand Balm 75ml', 890000, NULL, 55,
     'https://images.pexels.com/photos/5240411/pexels-photo-5240411.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem dưỡng tay sang trọng từ Aesop — chiết xuất thảo mộc hữu cơ làm mềm và phục hồi đôi bàn tay khô ráp.', 3, 1, 0, 120, @ShopNatureId, @Now),

    (N'Herbivore Botanicals Coco Rose Exfoliating Scrub 236ml', 680000, 600000, 35,
     'https://images.pexels.com/photos/3851076/pexels-photo-3851076.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Tẩy da chết toàn thân với dừa và cánh hoa hồng — loại bỏ tế bào chết, dưỡng da mềm mại và thơm phức.', 3, 1, 1, 400, @ShopNatureId, @Now),

    (N'Elemis Pro-Collagen Body Oil 100ml', 1580000, 1400000, 25,
     'https://images.pexels.com/photos/3735217/pexels-photo-3735217.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Dầu dưỡng thể Elemis với hoa nhài và hoa trà — thẩm thấu nhanh, cải thiện độ đàn hồi và làm mờ rạn da.', 3, 1, 0, 200, @ShopNatureId, @Now),

    -- Chăm sóc tóc (CategoryId = 5)
    (N'Olaplex No.3 Hair Perfector 100ml', 860000, 780000, 65,
     'https://images.pexels.com/photos/3993449/pexels-photo-3993449.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Treatment phục hồi tóc hư tổn tại nhà — tái tạo liên kết disulfide bị phá vỡ bởi hóa chất và nhiệt.', 5, 1, 1, 220, @ShopNatureId, @Now),

    (N'Redken All Soft Shampoo 300ml', 520000, 460000, 80,
     'https://images.pexels.com/photos/6621373/pexels-photo-6621373.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Dầu gội dưỡng mềm với Argan Oil — phục hồi tóc khô xơ, tăng độ bóng và dễ chải tức thì.', 5, 1, 0, 400, @ShopNatureId, @Now),

    (N'Davines OI Conditioner 250ml', 780000, NULL, 45,
     'https://images.pexels.com/photos/4465124/pexels-photo-4465124.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Dầu xả All-In-One với Roucou Oil — dưỡng mềm, giảm xơ rối và bảo vệ tóc trước tác nhân môi trường.', 5, 1, 1, 350, @ShopNatureId, @Now),

    (N'Bumble and bumble Hairdresser''s Invisible Oil 100ml', 980000, 880000, 40,
     'https://images.pexels.com/photos/6621374/pexels-photo-6621374.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Dầu dưỡng tóc vô hình với 6 loại dầu — bảo vệ nhiệt đến 230°C, tăng bóng và giảm frizz hiệu quả.', 5, 1, 0, 180, @ShopNatureId, @Now),

    -- Chống nắng (CategoryId = 6)
    (N'Skin Aqua UV Super Moisture Gel SPF50+ 110g', 320000, 285000, 120,
     'https://images.pexels.com/photos/4202328/pexels-photo-4202328.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem chống nắng gel dưỡng ẩm Nhật Bản — kết cấu trong suốt, không bết dính, SPF50+/PA++++.', 6, 1, 1, 200, @ShopNatureId, @Now),

    (N'Isntree Hyaluronic Acid Watery Sun Gel SPF50+ 50ml', 380000, 340000, 90,
     'https://images.pexels.com/photos/7755231/pexels-photo-7755231.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem chống nắng dạng gel với 8 loại Hyaluronic Acid — cấp ẩm tối đa, an toàn cho da nhạy cảm.', 6, 1, 1, 150, @ShopNatureId, @Now),

    (N'Altruist Dermatologist Sunscreen SPF50 200ml', 180000, NULL, 150,
     'https://images.pexels.com/photos/6621462/pexels-photo-6621462.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem chống nắng giá tốt SPF50 do bác sĩ da liễu phát triển — bảo vệ quang phổ rộng, lành tính và hiệu quả.', 6, 1, 0, 300, @ShopNatureId, @Now),

    (N'Bondi Sands SPF 50 Fragrance Free Sunscreen Lotion 150ml', 480000, 430000, 70,
     'https://images.pexels.com/photos/5240400/pexels-photo-5240400.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem chống nắng Úc không mùi hương — không gây kích ứng, chống thấm nước, phù hợp cho cả gia đình.', 6, 1, 0, 250, @ShopNatureId, @Now),

    (N'Thank You Farmer Sun Project Water Sun Cream SPF50+ 50ml', 420000, 375000, 85,
     'https://images.pexels.com/photos/4465126/pexels-photo-4465126.jpeg?auto=compress&cs=tinysrgb&w=500',
     N'Kem chống nắng dạng nước dưỡng ẩm Hàn Quốc — kiểm soát bã nhờn, finish tự nhiên, thích hợp làm primer.', 6, 1, 1, 160, @ShopNatureId, @Now);

    PRINT 'Inserted 15 products for Nature & Bloom';
END

-- ============================================================
--  6. VOUCHER RIÊNG CHO TỪNG SHOP
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = 'LUXE15')
BEGIN
    INSERT INTO Vouchers (Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, UsedCount, IsActive, ShopId, CreatedAt)
    VALUES (
        'LUXE15',
        N'Giảm 15% cho đơn từ 500,000₫ — Luxe Beauty House',
        'percent', 15, 500000, 500000,
        100, 0, 1,
        @ShopLuxeId,
        @Now
    );
    PRINT 'Created voucher LUXE15 for Luxe Beauty House';
END

IF NOT EXISTS (SELECT 1 FROM Vouchers WHERE Code = 'NATURE50K')
BEGIN
    INSERT INTO Vouchers (Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, UsedCount, IsActive, ShopId, CreatedAt)
    VALUES (
        'NATURE50K',
        N'Giảm 50,000₫ cho đơn từ 300,000₫ — Nature & Bloom',
        'fixed', 50000, NULL, 300000,
        100, 0, 1,
        @ShopNatureId,
        @Now
    );
    PRINT 'Created voucher NATURE50K for Nature & Bloom';
END

-- ============================================================
--  7. KIỂM TRA KẾT QUẢ
-- ============================================================
SELECT
    s.ShopName,
    s.Province,
    COUNT(p.Id) AS SoSanPham,
    (SELECT Code FROM Vouchers WHERE ShopId = s.Id) AS VoucherShop
FROM Shops s
LEFT JOIN Products p ON p.ShopId = s.Id AND p.IsActive = 1
GROUP BY s.Id, s.ShopName, s.Province;
