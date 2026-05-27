/* ============================================================
   GLOWHUB — SEED DATA  (idempotent, chỉ thêm mới, không xóa)
   ------------------------------------------------------------
   Cách dùng:
     1) Mở SQL Server Management Studio (SSMS)
     2) Connect tới server SQL đang chạy backend
     3) Mở file này, bấm Execute (F5)
     4) Xem cửa sổ Messages — script sẽ in tóm tắt số dòng

   AN TOÀN:
     - Tất cả block đều check NOT EXISTS hoặc count threshold
       → chạy lại nhiều lần không tạo trùng / không xóa data cũ
     - Mật khẩu user mới = "user123" (plain text, dev only)
                       admin    = "admin123"
     - Date phân bố trong 6 tháng gần nhất → biểu đồ doanh thu đẹp
   ============================================================ */

USE [BaseCoreDB];
GO
SET NOCOUNT ON;

PRINT N'╔══════════════════════════════════════════════════════════╗';
PRINT N'║         GLOWHUB — SEED DATA — bắt đầu...                ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

/* ============================================================
   STEP 1 — CATEGORIES (10 danh mục)
   ============================================================ */
PRINT N'';
PRINT N'[1/8] Categories...';

;WITH CategoryData(Name, Description) AS (
    SELECT * FROM (VALUES
        (N'Chăm sóc da mặt',         N'Serum, kem dưỡng, toner, sữa rửa mặt cho mọi loại da'),
        (N'Trang điểm',              N'Son môi, kem nền, phấn má, mascara và các sản phẩm makeup'),
        (N'Chăm sóc cơ thể',         N'Kem dưỡng thể, tẩy tế bào chết, dầu dưỡng da toàn thân'),
        (N'Nước hoa',                N'Nước hoa nữ, nữ tính, tinh tế cho mỗi dịp'),
        (N'Chăm sóc tóc',            N'Dầu gội, dầu xả, serum tóc, mặt nạ ủ tóc'),
        (N'Chống nắng',              N'Kem chống nắng vật lý và hóa học SPF 30-50+'),
        (N'Tẩy trang',               N'Nước tẩy trang, dầu tẩy trang, sáp tẩy trang chuyên sâu'),
        (N'Mặt nạ',                  N'Mặt nạ giấy, mặt nạ đất sét, mặt nạ ngủ tái tạo da'),
        (N'Sản phẩm nam',            N'Skincare và grooming dành riêng cho phái mạnh'),
        (N'Phụ kiện trang điểm',     N'Cọ, mút trang điểm, bông tẩy trang, dụng cụ làm đẹp')
    ) AS v(Name, Description)
)
INSERT INTO Categories (Name, Description)
SELECT cd.Name, cd.Description
FROM CategoryData cd
WHERE NOT EXISTS (SELECT 1 FROM Categories c WHERE c.Name = cd.Name);

DECLARE @cnt INT = @@ROWCOUNT;
PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' danh mục mới';

/* Lấy CategoryId thường dùng vào biến để các bước sau dùng */
DECLARE @CatSkincare   INT = (SELECT Id FROM Categories WHERE Name = N'Chăm sóc da mặt');
DECLARE @CatMakeup     INT = (SELECT Id FROM Categories WHERE Name = N'Trang điểm');
DECLARE @CatBody       INT = (SELECT Id FROM Categories WHERE Name = N'Chăm sóc cơ thể');
DECLARE @CatFragrance  INT = (SELECT Id FROM Categories WHERE Name = N'Nước hoa');
DECLARE @CatHair       INT = (SELECT Id FROM Categories WHERE Name = N'Chăm sóc tóc');
DECLARE @CatSunscreen  INT = (SELECT Id FROM Categories WHERE Name = N'Chống nắng');
DECLARE @CatCleanser   INT = (SELECT Id FROM Categories WHERE Name = N'Tẩy trang');
DECLARE @CatMask       INT = (SELECT Id FROM Categories WHERE Name = N'Mặt nạ');
DECLARE @CatMen        INT = (SELECT Id FROM Categories WHERE Name = N'Sản phẩm nam');
DECLARE @CatTools      INT = (SELECT Id FROM Categories WHERE Name = N'Phụ kiện trang điểm');

/* ============================================================
   STEP 2 — USERS (30 user: 2 admin + 28 customer)
   Lưu ý: Salt = NULL (mật khẩu plain text cho dev) — đúng pattern
          backend đang xử lý.
   ============================================================ */
PRINT N'';
PRINT N'[2/8] Users...';

DECLARE @UserSeed TABLE (Name NVARCHAR(100), UserName NVARCHAR(50), Password NVARCHAR(255),
                        Email NVARCHAR(100), Phone NVARCHAR(20), Position NVARCHAR(100),
                        UserType INT);

INSERT INTO @UserSeed VALUES
-- Admin
(N'GlowHub Admin',         'admin',          'admin123', 'admin@glowhub.vn',     '0901234567', N'Administrator', 1),
(N'Trần Mai Linh',         'mai.tran',       'admin123', 'mai.tran@glowhub.vn',  '0901112233', N'Manager',       1),
-- Customers (28)
(N'Nguyễn Thị Lan',        'lan.nguyen',     'user123',  'lan@gmail.com',        '0912345678', N'Customer',      0),
(N'Phạm Minh Anh',         'anh.pham',       'user123',  'anh.pham@gmail.com',   '0912345679', N'Customer',      0),
(N'Lê Hoàng Nam',          'nam.le',         'user123',  'nam.le@gmail.com',     '0912345680', N'Customer',      0),
(N'Trần Thanh Hà',         'ha.tran',        'user123',  'ha.tran@gmail.com',    '0912345681', N'Customer',      0),
(N'Vũ Quỳnh Như',          'nhu.vu',         'user123',  'nhu.vu@gmail.com',     '0912345682', N'Customer',      0),
(N'Đặng Bảo Châu',         'chau.dang',      'user123',  'chau@gmail.com',       '0912345683', N'Customer',      0),
(N'Hoàng Mỹ Linh',         'linh.hoang',     'user123',  'linh.hoang@gmail.com', '0912345684', N'Customer',      0),
(N'Bùi Thu Trang',         'trang.bui',      'user123',  'trang.bui@gmail.com',  '0912345685', N'Customer',      0),
(N'Đỗ Hồng Ngọc',          'ngoc.do',        'user123',  'ngoc.do@gmail.com',    '0912345686', N'Customer',      0),
(N'Phan Diệu Linh',        'dieulinh.phan',  'user123',  'dieulinh@gmail.com',   '0912345687', N'Customer',      0),
(N'Lý Anh Thư',            'thu.ly',         'user123',  'thu.ly@gmail.com',     '0912345688', N'Customer',      0),
(N'Mai Khánh Vy',          'vy.mai',         'user123',  'vy.mai@gmail.com',     '0912345689', N'Customer',      0),
(N'Trương Bích Phương',    'phuong.truong',  'user123',  'phuong@gmail.com',     '0912345690', N'Customer',      0),
(N'Cao Ngọc Hân',          'han.cao',        'user123',  'han.cao@gmail.com',    '0912345691', N'Customer',      0),
(N'Nguyễn Văn Hùng',       'hung.nguyen',    'user123',  'hung.nguyen@gmail.com','0912345692', N'Customer',      0),
(N'Lê Thị Hoa',            'hoa.le',         'user123',  'hoa.le@gmail.com',     '0912345693', N'Customer',      0),
(N'Phạm Quốc Bảo',         'bao.pham',       'user123',  'bao.pham@gmail.com',   '0912345694', N'Customer',      0),
(N'Trần Khánh My',         'my.tran',        'user123',  'my.tran@gmail.com',    '0912345695', N'Customer',      0),
(N'Đinh Hoàng Phúc',       'phuc.dinh',      'user123',  'phuc.dinh@gmail.com',  '0912345696', N'Customer',      0),
(N'Võ Thanh Tú',           'tu.vo',          'user123',  'tu.vo@gmail.com',      '0912345697', N'Customer',      0),
(N'Hồ Ngọc Châu',          'chau.ho',        'user123',  'chau.ho@gmail.com',    '0912345698', N'Customer',      0),
(N'Đào Minh Quân',         'quan.dao',       'user123',  'quan.dao@gmail.com',   '0912345699', N'Customer',      0),
(N'Lưu Hà Vy',             'vy.luu',         'user123',  'vy.luu@gmail.com',     '0912345700', N'Customer',      0),
(N'Tô Bảo Trâm',           'tram.to',        'user123',  'tram.to@gmail.com',    '0912345701', N'Customer',      0),
(N'Ngô Hải Yến',           'yen.ngo',        'user123',  'yen.ngo@gmail.com',    '0912345702', N'Customer',      0),
(N'Phùng Anh Đào',         'dao.phung',      'user123',  'dao.phung@gmail.com',  '0912345703', N'Customer',      0),
(N'Tạ Khánh Linh',         'linh.ta',        'user123',  'linh.ta@gmail.com',    '0912345704', N'Customer',      0),
(N'Dương Hoàng Long',      'long.duong',     'user123',  'long.duong@gmail.com', '0912345705', N'Customer',      0),
(N'Trịnh Thùy Dương',      'duong.trinh',    'user123',  'duong.trinh@gmail.com','0912345706', N'Customer',      0);

INSERT INTO Users (Id, Name, UserName, Password, Salt, Contact, Email, Phone, Position, Image, IsActive, UserType, Created)
SELECT
    LOWER(CONVERT(NVARCHAR(450), NEWID())),
    us.Name, us.UserName, us.Password, NULL,
    N'Liên hệ qua email',
    us.Email, us.Phone, us.Position, N'',
    1, us.UserType,
    DATEADD(DAY, -ABS(CHECKSUM(NEWID())) % 180, GETDATE())  -- tạo trong 6 tháng qua
FROM @UserSeed us
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.UserName = us.UserName);

SET @cnt = @@ROWCOUNT;
PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' user mới';

/* ============================================================
   STEP 3 — PRODUCTS (~120 sản phẩm thực tế)
   ============================================================ */
PRINT N'';
PRINT N'[3/8] Products...';

DECLARE @ProdSeed TABLE (
    Name NVARCHAR(200), Price DECIMAL(18,2), Stock INT,
    ImageUrl NVARCHAR(500), Description NVARCHAR(1000),
    CategoryId INT
);

-- ── Chăm sóc da mặt (Skincare) — 20 SP ──
INSERT INTO @ProdSeed VALUES
(N'Serum Vitamin C SkinCeuticals CE Ferulic',          3200000, 50,  'https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=400', N'Serum chống oxy hóa với Vitamin C 15%, Vitamin E và Ferulic Acid. Sáng da, mờ thâm nám, chống lão hóa.', @CatSkincare),
(N'Toner Some By Mi AHA BHA PHA 30 Days',              420000, 120, 'https://images.unsplash.com/photo-1556228578-8c89e6adf883?w=400',  N'Toner tẩy tế bào chết với 3 loại acid. Cải thiện kết cấu da, thu nhỏ lỗ chân lông sau 30 ngày.', @CatSkincare),
(N'Sữa Rửa Mặt CeraVe Hydrating Cleanser',             380000, 150, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400',  N'Sữa rửa mặt dịu nhẹ với 3 Ceramides và Hyaluronic Acid. Phù hợp da nhạy cảm và da khô.', @CatSkincare),
(N'Kem Dưỡng Ẩm Laneige Water Bank',                   850000, 80,  'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Cấp ẩm chuyên sâu với HYDRO IONIZED MINERAL WATER. Da mềm mại, cân bằng tức thì.', @CatSkincare),
(N'Serum The Ordinary Niacinamide 10% + Zinc 1%',      280000, 200, 'https://images.unsplash.com/photo-1556228720-da4e85f4d6ce?w=400',  N'Niacinamide nồng độ cao kiểm soát dầu, se khít lỗ chân lông và làm đều màu da.', @CatSkincare),
(N'Toner Klairs Supple Preparation Unscented',         420000, 110, 'https://images.unsplash.com/photo-1612817288484-6f916006741a?w=400', N'Toner cân bằng pH không hương liệu cho da nhạy cảm. Bổ sung độ ẩm tức thì.', @CatSkincare),
(N'Sữa Rửa Mặt Cetaphil Gentle Skin Cleanser',         310000, 180, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Làm sạch dịu nhẹ không xà phòng. Phù hợp mọi loại da kể cả em bé.', @CatSkincare),
(N'Serum Cosrx Advanced Snail 96 Mucin',               360000, 130, 'https://images.unsplash.com/photo-1614859127489-c39d1ea15a98?w=400', N'96% dịch nhầy ốc sên phục hồi và làm dịu da hư tổn, mờ vết thâm.', @CatSkincare),
(N'Kem Dưỡng La Roche-Posay Effaclar Duo+',            720000, 90,  'https://images.unsplash.com/photo-1631730486572-226d1f595b68?w=400', N'Trị mụn và ngăn ngừa vết thâm cho da dầu mụn. Hiệu quả sau 4 tuần.', @CatSkincare),
(N'Toner Mamonde Rose Water',                          340000, 140, 'https://images.unsplash.com/photo-1576091160550-2173dba999ef?w=400', N'Toner chiết xuất hoa hồng Damask làm dịu và cấp ẩm tức thì cho mọi loại da.', @CatSkincare),
(N'Sữa Rửa Mặt Hada Labo Gokujyun Foaming',            220000, 200, 'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400',  N'Bọt mịn với Hyaluronic Acid siêu thấm. Làm sạch không khô căng.', @CatSkincare),
(N'Serum Olay Regenerist Mini',                        890000, 70,  'https://images.unsplash.com/photo-1620916297893-2f0e0867ad96?w=400', N'Niacinamide + Peptide chống lão hóa, làm săn chắc và đều màu da rõ rệt.', @CatSkincare),
(N'Kem Dưỡng Mắt Kiehl''s Avocado Eye Cream',         790000, 60,  'https://images.unsplash.com/photo-1612817159949-195b6eb9e31a?w=400', N'Chiết xuất bơ và protein đậu nành dưỡng vùng da quanh mắt mịn màng.', @CatSkincare),
(N'Toner Pixi Glow Tonic',                             520000, 100, 'https://images.unsplash.com/photo-1631730486572-226d1f595b68?w=400', N'5% Glycolic Acid tẩy tế bào chết và làm sáng tức thì. Cult favorite.', @CatSkincare),
(N'Serum Bioderma Sebium Serum',                       650000, 80,  'https://images.unsplash.com/photo-1620916297893-2f0e0867ad96?w=400', N'Kiểm soát dầu nhờn và se khít lỗ chân lông cho da dầu mụn.', @CatSkincare),
(N'Kem Dưỡng Innisfree Green Tea Seed',                490000, 95,  'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Hạt trà xanh Jeju cấp ẩm 24 giờ, da căng bóng tự nhiên.', @CatSkincare),
(N'Sữa Rửa Mặt Innisfree Green Tea Cleansing Foam',    250000, 150, 'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400', N'Bọt nhẹ từ trà xanh, làm sạch dịu nhẹ và cấp ẩm.', @CatSkincare),
(N'Essence SK-II Facial Treatment',                    3500000,30,  'https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=400', N'Pitera™ huyền thoại tái tạo da, giúp da trong suốt như pha lê.', @CatSkincare),
(N'Serum The Ordinary Hyaluronic Acid 2% + B5',        320000, 160, 'https://images.unsplash.com/photo-1556228720-da4e85f4d6ce?w=400', N'3 trọng lượng phân tử HA cấp ẩm đa tầng. B5 phục hồi và làm dịu.', @CatSkincare),
(N'Kem Dưỡng Vichy Mineral 89',                        790000, 75,  'https://images.unsplash.com/photo-1572116469696-31de0f17cc34?w=400', N'89% nước khoáng núi lửa Vichy + HA, củng cố hàng rào bảo vệ da.', @CatSkincare);

-- ── Trang điểm (Makeup) — 20 SP ──
INSERT INTO @ProdSeed VALUES
(N'Son Kem Lì Black Rouge Air Fit Velvet',             285000, 200, 'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=400', N'Son kem lì mỏng mịn công thức Air-fit siêu nhẹ. Bám màu suốt ngày không khô môi.', @CatMakeup),
(N'Kem Nền Maybelline Fit Me Matte',                   220000, 180, 'https://images.unsplash.com/photo-1631214524020-3c69b8b0c7b9?w=400', N'Kem nền kiềm dầu che phủ hoàn hảo, kiểm soát bóng nhờn suốt 24 giờ.', @CatMakeup),
(N'Phấn Má Hồng NARS Blush Orgasm',                    1150000,60,  'https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=400', N'Tông hồng đào ánh vàng biểu tượng. Hiệu ứng da căng bóng tự nhiên.', @CatMakeup),
(N'Mascara Maybelline Lash Sensational Sky High',      290000, 220, 'https://images.unsplash.com/photo-1591360236480-9c6e1e2c0ec5?w=400', N'Mascara cọ cong làm dài và uốn mi lên đến 36mm. Không vón cục.', @CatMakeup),
(N'Son Lì 3CE Velvet Lip Tint',                        350000, 180, 'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=400', N'Tint môi velvet matte mịn như nhung. Bám màu cả ngày, không phai.', @CatMakeup),
(N'Bảng Mắt Urban Decay Naked Heat',                   1450000,40,  'https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=400', N'12 tông màu nóng từ cam đào đến đỏ rượu. Lên màu chuẩn, dễ tán.', @CatMakeup),
(N'Kem Nền MAC Studio Fix Fluid SPF15',                890000, 90,  'https://images.unsplash.com/photo-1631214524020-3c69b8b0c7b9?w=400', N'Kem nền lì matte che phủ trung bình-cao. Bền màu 8 giờ.', @CatMakeup),
(N'Son Dior Rouge Couture Velvet',                     1290000,55,  'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=400', N'Son lì velvet sang trọng của Dior. 16 giờ bám màu không khô.', @CatMakeup),
(N'Mascara L''Oreal Voluminous Lash Paradise',         310000, 200, 'https://images.unsplash.com/photo-1591360236480-9c6e1e2c0ec5?w=400', N'Cọ Millionizer làm dày và làm cong mi hiệu quả. Không lem.', @CatMakeup),
(N'Phấn Phủ Innisfree No Sebum Mineral',               220000, 250, 'https://images.unsplash.com/photo-1631214524020-3c69b8b0c7b9?w=400', N'Phấn bột kiểm soát dầu cho da hỗn hợp. Da mịn lì cả ngày.', @CatMakeup),
(N'Phấn Má NARS Multiple Stick',                       870000, 70,  'https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=400', N'Thỏi phấn má cream-stick dạng kem. Tán dễ, kết quả tự nhiên.', @CatMakeup),
(N'Son Romand Juicy Lasting Tint',                     310000, 200, 'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=400', N'Tint môi mọng nước trong veo. Cảm giác mềm mịn không khô.', @CatMakeup),
(N'Eyeliner Stila Stay All Day Liquid',                490000, 110, 'https://images.unsplash.com/photo-1591360236480-9c6e1e2c0ec5?w=400', N'Bút kẻ mắt nước siêu mảnh, bền màu 16 giờ. Không lem không trôi.', @CatMakeup),
(N'Bảng Mắt MAC 9 màu Burgundy Times Nine',            1290000,45,  'https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=400', N'9 ô màu burgundy sang trọng. Lên màu chuẩn studio.', @CatMakeup),
(N'Son Tom Ford Lip Color Rouge',                      2150000,30,  'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=400', N'Son nhung Tom Ford huyền thoại với 16 sắc thái rouge đẳng cấp.', @CatMakeup),
(N'Kem Nền Estee Lauder Double Wear Stay-In-Place',    1290000,65,  'https://images.unsplash.com/photo-1631214524020-3c69b8b0c7b9?w=400', N'Kem nền 24 giờ không trôi, không bóng, che khuyết điểm hoàn hảo.', @CatMakeup),
(N'Bút Chì Mày Etude House Drawing Eye Brow',          120000, 250, 'https://images.unsplash.com/photo-1591360236480-9c6e1e2c0ec5?w=400', N'Bút chì kẻ mày 2 đầu, đầu nhỏ vẽ chi tiết, đầu chải làm gọn.', @CatMakeup),
(N'Highlighter Becca Shimmering Skin Perfector',       1290000,50,  'https://images.unsplash.com/photo-1596462502278-27bfdc403348?w=400', N'Bột bắt sáng Champagne Pop biểu tượng. Da phát sáng glowy.', @CatMakeup),
(N'Mascara Diorshow Iconic Overcurl',                  990000, 60,  'https://images.unsplash.com/photo-1591360236480-9c6e1e2c0ec5?w=400', N'Cong vuốt mi từ gốc đến ngọn cả ngày. Không vón, không lem.', @CatMakeup),
(N'Concealer Tarte Shape Tape',                        890000, 100, 'https://images.unsplash.com/photo-1631214524020-3c69b8b0c7b9?w=400', N'Che khuyết điểm full coverage huyền thoại. Bền 16 giờ.', @CatMakeup);

-- ── Chăm sóc cơ thể (Body) — 12 SP ──
INSERT INTO @ProdSeed VALUES
(N'Kem Dưỡng Thể Vaseline Healthy White',              145000, 300, 'https://images.unsplash.com/photo-1556228720-da4e85f4d6ce?w=400', N'Dưỡng ẩm 24 giờ với Vitamin B3. Da mềm mịn và sáng đều.', @CatBody),
(N'Tẩy Tế Bào Chết The Body Shop Shea Scrub',          650000, 70,  'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=400', N'Tẩy tế bào chết dạng kem với hạt walnut và bơ Shea. Da mềm mại.', @CatBody),
(N'Sữa Tắm Dove Deeply Nourishing',                    195000, 280, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Công thức 1/4 kem dưỡng độc quyền. Da mềm mịn ngay sau khi tắm.', @CatBody),
(N'Kem Dưỡng Body Bath & Body Works',                  430000, 120, 'https://images.unsplash.com/photo-1556228720-da4e85f4d6ce?w=400', N'24 giờ dưỡng ẩm với mùi hương dễ chịu. Nhiều mùi để chọn.', @CatBody),
(N'Tẩy Tế Bào Chết Cocoon Cà Phê Đắk Lắk',             185000, 180, 'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=400', N'Body scrub cà phê thiên nhiên thuần Việt. Da săn chắc và mịn màng.', @CatBody),
(N'Sữa Tắm Coco Vera Sensual',                         245000, 200, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Sữa tắm hương hoa nhài và sữa dừa. Tạo bọt êm và lưu hương lâu.', @CatBody),
(N'Dầu Dưỡng Body Bio-Oil Skincare Oil',               350000, 150, 'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=400', N'Dầu dưỡng đa năng giúp mờ sẹo, rạn da và làm đều màu da.', @CatBody),
(N'Kem Dưỡng Tay L''Occitane Shea Hand Cream',         590000, 90,  'https://images.unsplash.com/photo-1556228720-da4e85f4d6ce?w=400', N'Bơ Shea 20% từ Burkina Faso. Phục hồi tay mềm mại nhanh chóng.', @CatBody),
(N'Kem Tẩy Lông Veet Silky Fresh',                     185000, 200, 'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=400', N'Tẩy lông nhanh 5 phút với Aloe Vera dịu nhẹ cho da.', @CatBody),
(N'Sữa Tắm Olay Foaming Body Wash',                    275000, 180, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Bọt mịn với Niacinamide làm sáng đều màu da toàn thân.', @CatBody),
(N'Dầu Massage Johnson''s Baby Oil',                    95000,  280, 'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=400', N'Dầu khoáng tinh khiết massage cho cả người lớn và em bé.', @CatBody),
(N'Lăn Khử Mùi Rexona Antibacterial',                  110000, 320, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Bảo vệ chống mồ hôi và mùi hôi 48 giờ. Khô thoáng cả ngày.', @CatBody);

-- ── Nước hoa (Fragrance) — 10 SP ──
INSERT INTO @ProdSeed VALUES
(N'Nước Hoa Chloe Eau de Parfum 50ml',                 2800000, 35, 'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=400', N'Hương hoa hồng thanh tao pha lẫn mộc lan và musk trắng. Lưu hương lâu, tinh tế.', @CatFragrance),
(N'Nước Hoa Viktor & Rolf Flowerbomb 30ml',            2100000, 28, 'https://images.unsplash.com/photo-1592945403407-9caf930a5b57?w=400', N'Hương jasmine, rose, freesia và musk. Ấm áp và gợi cảm.', @CatFragrance),
(N'Nước Hoa Dior Miss Dior 50ml',                      3200000, 30, 'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=400', N'Hương hoa hồng Centifolia Grasse trong tinh hoa Pháp lãng mạn.', @CatFragrance),
(N'Nước Hoa YSL Black Opium 50ml',                     2500000, 32, 'https://images.unsplash.com/photo-1592945403407-9caf930a5b57?w=400', N'Cà phê đen + vanilla + hoa cam. Quyến rũ và bí ẩn về đêm.', @CatFragrance),
(N'Nước Hoa Marc Jacobs Daisy EDT 50ml',               2200000, 40, 'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=400', N'Tươi tắn với hoa cúc, dâu rừng và violet. Trẻ trung yêu đời.', @CatFragrance),
(N'Nước Hoa Versace Bright Crystal 30ml',              1450000, 50, 'https://images.unsplash.com/photo-1592945403407-9caf930a5b57?w=400', N'Lựu, peony và magnolia trong veo, mát lành. Phù hợp ngày năng động.', @CatFragrance),
(N'Nước Hoa Lancome La Vie Est Belle 50ml',            2900000, 28, 'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=400', N'Iris, jasmine và patchouli ấm áp. Hương đại diện cho hạnh phúc.', @CatFragrance),
(N'Nước Hoa Calvin Klein CK One 100ml',                1290000, 60, 'https://images.unsplash.com/photo-1592945403407-9caf930a5b57?w=400', N'Unisex citrus và trà xanh. Tươi mát, hợp mọi giới tính.', @CatFragrance),
(N'Nước Hoa Gucci Bloom 50ml',                         2700000, 30, 'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=400', N'Tuberose, jasmine và Rangoon Creeper. Bó hoa rực rỡ trong vườn.', @CatFragrance),
(N'Nước Hoa Hermes Twilly d''Hermes 50ml',             3100000, 22, 'https://images.unsplash.com/photo-1592945403407-9caf930a5b57?w=400', N'Gừng + tuberose + sandalwood. Tinh thần Hermes mạnh mẽ và nữ tính.', @CatFragrance);

-- ── Chăm sóc tóc (Hair) — 10 SP ──
INSERT INTO @ProdSeed VALUES
(N'Dầu Gội Kérastase Nutritive Bain Satin',            780000, 90,  'https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=400', N'Dầu gội cao cấp phục hồi tóc khô hư tổn với Irisome Complex.', @CatHair),
(N'Serum Dưỡng Tóc Moroccanoil Treatment',             920000, 65,  'https://images.unsplash.com/photo-1560472354-b33ff0c44a43?w=400', N'Dầu argan ấm nóng: bóng mượt, chống xơ rối, bảo vệ khỏi nhiệt.', @CatHair),
(N'Dầu Xả Tresemmé Keratin Smooth',                    195000, 200, 'https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=400', N'Keratin giảm xơ rối, làm mềm và bóng mượt tóc tới 72 giờ.', @CatHair),
(N'Mặt Nạ Tóc L''Oreal Elseve Total Repair 5',         195000, 180, 'https://images.unsplash.com/photo-1560472354-b33ff0c44a43?w=400', N'Mặt nạ ủ tóc 5 trong 1: chống rụng, bóng, mềm, gãy và khô.', @CatHair),
(N'Serum Tóc TIGI Bed Head After Party',               420000, 110, 'https://images.unsplash.com/photo-1560472354-b33ff0c44a43?w=400', N'Sữa dưỡng tóc giảm xơ rối, không nhờn, có mùi hương dễ chịu.', @CatHair),
(N'Dầu Gội Pantene Pro-V Daily Moisture',              165000, 250, 'https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=400', N'Pro-V dưỡng ẩm hàng ngày. Tóc bóng khỏe sau lần gội đầu tiên.', @CatHair),
(N'Mặt Nạ Tóc Olaplex No. 8',                          990000, 50,  'https://images.unsplash.com/photo-1560472354-b33ff0c44a43?w=400', N'Bond Intense Moisture Mask phục hồi tóc tẩy/nhuộm nặng.', @CatHair),
(N'Xịt Dưỡng Tóc Bumble and Bumble Hairdresser''s Oil',1290000,45,  'https://images.unsplash.com/photo-1560472354-b33ff0c44a43?w=400', N'6 loại dầu thiên nhiên tổng hợp. Da đầu nhẹ, tóc mượt mà.', @CatHair),
(N'Dầu Gội Davines Love Smoothing',                    690000, 80,  'https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=400', N'Dầu olive Italia làm mượt và giảm xơ. Bao bì thân thiện môi trường.', @CatHair),
(N'Gôm Xịt Tóc Schwarzkopf got2b Glued',               220000, 220, 'https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=400', N'Giữ nếp tóc cứng cáp nhưng dễ rửa sạch. Phù hợp tạo kiểu cá tính.', @CatHair);

-- ── Chống nắng (Sunscreen) — 10 SP ──
INSERT INTO @ProdSeed VALUES
(N'Kem Chống Nắng Anessa Perfect UV SPF50+',           680000, 110, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Công nghệ Aqua Booster Nhật. SPF50+ PA++++, chống nước và mồ hôi.', @CatSunscreen),
(N'Kem Chống Nắng La Roche-Posay Anthelios SPF50',     750000, 95,  'https://images.unsplash.com/photo-1608248543803-ba4f8c70ae0b?w=400', N'Công thức Cell-Ox Shield cho da nhạy cảm. Bảo vệ toàn diện UVA/UVB.', @CatSunscreen),
(N'Sunplay Skin Aqua UV Sunscreen Milk SPF50+',        185000, 280, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Mỏng nhẹ thẩm thấu nhanh không bết dính. Giá tốt cho học sinh sinh viên.', @CatSunscreen),
(N'Innisfree Daily UV Defense Sunscreen SPF36',        320000, 180, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Trà xanh Jeju + Hyaluronic Acid. Cấp ẩm + chống nắng cùng lúc.', @CatSunscreen),
(N'Bioré UV Aqua Rich Watery Essence SPF50+',          250000, 200, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Essence chống nắng dạng nước. Da căng mướt như không có gì trên mặt.', @CatSunscreen),
(N'Skin Aqua Tone Up UV Essence Mint Green',           165000, 250, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Tone-up mint green giúp da hồng hào tự nhiên + chống nắng.', @CatSunscreen),
(N'Eucerin Sun Pigment Control SPF50+',                890000, 65,  'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Thiamidol giảm nám và đốm nâu hiệu quả. Chống nắng cao cấp.', @CatSunscreen),
(N'Nivea Sun UV Face Shine Control SPF50',             295000, 170, 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Kiềm dầu kép, không bóng nhờn. Lý tưởng cho da dầu Việt Nam.', @CatSunscreen),
(N'Vichy Capital Soleil UV Age Daily SPF60',           890000, 70,  'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Chống lão hóa do tia UV với Niacinamide + Peptide. Mỏng mịn.', @CatSunscreen),
(N'Heliocare 360 Color Gel Oil-Free SPF50+',           1290000,50,  'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=400', N'Bảo vệ siêu cao + có tone tay che khuyết điểm nhẹ. Da đẹp tức thì.', @CatSunscreen);

-- ── Tẩy trang (Cleansing) — 10 SP ──
INSERT INTO @ProdSeed VALUES
(N'Nước Tẩy Trang Bioderma Sensibio H2O 500ml',        490000, 150, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Micellar water gốc Pháp. Làm sạch dịu nhẹ cho da nhạy cảm.', @CatCleanser),
(N'Dầu Tẩy Trang DHC Deep Cleansing Oil 200ml',        595000, 100, 'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400', N'Dầu olive nguyên chất, tan trôi kem chống nắng và makeup lì hiệu quả.', @CatCleanser),
(N'Nước Tẩy Trang Garnier Micellar 400ml',             175000, 280, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Tẩy trang toàn diện không cần rửa lại. Giá rẻ chất lượng tốt.', @CatCleanser),
(N'Sáp Tẩy Trang Banila Co Clean It Zero',             430000, 140, 'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400', N'Sáp tan thành dầu khi tiếp xúc da. Tẩy sạch makeup bám lâu.', @CatCleanser),
(N'Dầu Tẩy Trang Innisfree Apple Seed Cleansing',      365000, 120, 'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400', N'Dầu hạt táo organic làm sạch dịu nhẹ và cấp ẩm.', @CatCleanser),
(N'Nước Tẩy Trang L''Oreal Sublime Soft 400ml',        220000, 200, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Hồng trà + hoa anh đào. Làm sạch nhẹ nhàng và dưỡng da.', @CatCleanser),
(N'Tẩy Trang Mắt Maybelline 125ml',                    140000, 230, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'2 lớp dầu-nước tẩy mascara lì hiệu quả không kích ứng mắt.', @CatCleanser),
(N'Dầu Tẩy Trang Shu Uemura Anti/Oxi+',                890000, 80,  'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400', N'Dầu cleansing huyền thoại Nhật. Da sáng và mềm mại sau mỗi lần dùng.', @CatCleanser),
(N'Sáp Tẩy Trang Heimish All Clean Balm',              350000, 150, 'https://images.unsplash.com/photo-1556228841-a3b6f4a4ade6?w=400', N'Sáp tẩy trang dịu nhẹ giá hợp lý. Best seller Olive Young.', @CatCleanser),
(N'Nước Tẩy Trang Avene Micellar Lotion 400ml',        420000, 100, 'https://images.unsplash.com/photo-1570194065650-d99fb4b8ccb9?w=400', N'Nước khoáng Avene + 0% paraben. Lý tưởng cho da nhạy cảm.', @CatCleanser);

-- ── Mặt nạ (Mask) — 10 SP ──
INSERT INTO @ProdSeed VALUES
(N'Mặt Nạ Mediheal NMF Aquaring Ampoule',              25000,  500, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Mặt nạ NMF + Aqua Ampoule cấp ẩm tức thì. Da căng bóng sau 15 phút.', @CatMask),
(N'Mặt Nạ SK-II Facial Treatment',                     280000, 60,  'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Pitera concentrate cao cấp. Mỗi miếng có hiệu quả như 5 chai essence.', @CatMask),
(N'Mặt Nạ Đất Sét Innisfree Super Volcanic Pore Clay', 295000, 150, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Đất sét núi lửa Jeju hút sạch dầu thừa và tạp chất trong lỗ chân lông.', @CatMask),
(N'Mặt Nạ Ngủ Laneige Water Sleeping Mask',            560000, 100, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Mặt nạ ngủ cấp ẩm chuyên sâu qua đêm. Da căng bóng buổi sáng.', @CatMask),
(N'Mặt Nạ Cosrx Acne Pimple Master Patch',             95000,  300, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Miếng dán hút mủ mụn qua đêm. 24 miếng/pack tiện dụng.', @CatMask),
(N'Mặt Nạ Klairs Rich Moist Soothing',                 65000,  280, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Mặt nạ giấy dịu nhẹ với 5% Beta-glucan. Phục hồi da kích ứng.', @CatMask),
(N'Mặt Nạ Bioaqua Cucumber Hydrating',                 35000,  400, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Chiết xuất dưa chuột làm mát và cấp ẩm da. Giá rẻ hợp túi tiền.', @CatMask),
(N'Mặt Nạ Đất Sét Aztec Healing Clay',                 280000, 130, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Đất sét Bentonite từ Indian Healing. Thải độc lỗ chân lông sâu.', @CatMask),
(N'Mặt Nạ Lá Mềm Hada Labo Premium Whitening',         55000,  350, 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Hyaluronic Acid + Tranexamic Acid làm sáng da. Giá tốt Nhật Bản.', @CatMask),
(N'Mặt Nạ Ngủ Belif The True Cream Aqua Bomb',         790000, 70,  'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=400', N'Bom cấp ẩm với 5 loại thảo dược. Da căng mọng mỗi sáng.', @CatMask);

-- ── Sản phẩm nam (Men) — 8 SP ──
INSERT INTO @ProdSeed VALUES
(N'Sữa Rửa Mặt Nivea Men All-in-One',                  140000, 250, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Làm sạch + tẩy tế bào chết + dưỡng ẩm 3 in 1. Dành riêng cho nam.', @CatMen),
(N'Gel Cạo Râu Gillette Sensitive Skin',               115000, 280, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Gel cạo râu da nhạy cảm. Không kích ứng, dao lướt mịn.', @CatMen),
(N'Kem Dưỡng Nam L''Oreal Men Expert Hydra Energetic', 195000, 180, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Cấp ẩm + chống mỏi mệt cho da nam. Taurine + Vitamin C.', @CatMen),
(N'Sáp Vuốt Tóc American Crew Fiber',                  390000, 100, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Sáp giữ nếp matte tự nhiên cho mọi kiểu tóc nam.', @CatMen),
(N'Nước Hoa Nam Bleu de Chanel EDP 100ml',             4200000, 25, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Nước hoa nam huyền thoại Chanel. Tinh tế, lịch lãm, đẳng cấp.', @CatMen),
(N'Lăn Khử Mùi Dove Men+Care Cool Fresh',              85000,  330, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'48 giờ khử mùi cho nam. Mát lạnh tinh thần suốt ngày dài.', @CatMen),
(N'Sữa Tắm Nam Romano Force',                          135000, 250, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Sữa tắm hương nam tính mạnh mẽ. Bọt mịn sảng khoái.', @CatMen),
(N'Kem Dưỡng Sau Cạo Râu Nivea Men Sensitive',         135000, 220, 'https://images.unsplash.com/photo-1626251914-7c1f0902d8c4?w=400', N'Làm dịu da và phục hồi vùng da bị tổn thương sau cạo râu.', @CatMen);

-- ── Phụ kiện trang điểm (Tools) — 10 SP ──
INSERT INTO @ProdSeed VALUES
(N'Bộ Cọ Trang Điểm 12 Cây BS-MALL',                   590000, 80,  'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'12 cây cọ chuyên nghiệp đầy đủ chức năng. Lông mềm bền bỉ.', @CatTools),
(N'Mút Trang Điểm Beauty Blender Original',            520000, 120, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Mút trứng huyền thoại. Tán nền hoàn hảo, hiệu ứng airbrush.', @CatTools),
(N'Bông Tẩy Trang Cotton Pads 222 Miếng',              45000,  450, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'100% cotton tự nhiên, 3 lớp dày, không xơ. Tiện lợi hằng ngày.', @CatTools),
(N'Bấm Mi Shu Uemura Eyelash Curler',                  590000, 90,  'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Bấm mi best seller Nhật. Tạo độ cong tự nhiên không gãy mi.', @CatTools),
(N'Gương Trang Điểm LED Cảm Ứng',                      450000, 110, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Gương 3 ánh sáng LED, sạc USB. Make-up dễ dàng mọi nơi.', @CatTools),
(N'Khăn Mặt Khô Cotton Tencel 80 Tờ',                  125000, 230, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Khăn dùng một lần, mềm mại, không kích ứng. An toàn cho da.', @CatTools),
(N'Que Gỗ Đẩy Lớp Biểu Bì Tay',                        35000,  500, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Que gỗ làm móng chuyên dụng. Set 100 que tiện dùng.', @CatTools),
(N'Túi Đựng Mỹ Phẩm Bagsmart Du Lịch',                 390000, 120, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Túi mỹ phẩm 3 tầng có móc treo. Nhỏ gọn, đẹp, tiện du lịch.', @CatTools),
(N'Tăm Bông Trang Điểm Premium 200 Cây',               45000,  500, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Tăm bông 2 đầu nhỏ, dùng sửa lỗi trang điểm chính xác.', @CatTools),
(N'Băng Đô Rửa Mặt Spa Headband Hồng',                 65000,  380, 'https://images.unsplash.com/photo-1631214503826-4dc1a8b2c91d?w=400', N'Vải Coral mềm. Giữ tóc khi rửa mặt, đắp mặt nạ tiện lợi.', @CatTools);

-- INSERT vào Products, skip nếu Name đã tồn tại
INSERT INTO Products (Name, Price, Stock, ImageUrl, Description, CategoryId, IsActive)
SELECT ps.Name, ps.Price, ps.Stock, ps.ImageUrl, ps.Description, ps.CategoryId, 1
FROM @ProdSeed ps
WHERE NOT EXISTS (SELECT 1 FROM Products p WHERE p.Name = ps.Name);

SET @cnt = @@ROWCOUNT;
PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' sản phẩm mới';

/* ============================================================
   STEP 4 — BANNERS (10 banner, 6 active)
   ============================================================ */
PRINT N'';
PRINT N'[4/8] Banners...';

IF OBJECT_ID('Banners', 'U') IS NOT NULL
BEGIN
    ;WITH BannerData(Title, Subtitle, ImageUrl, LinkUrl, ButtonText, BgColor, SortOrder, IsActive) AS (
        SELECT * FROM (VALUES
            (N'Bộ Sưu Tập Mùa Hè 2026',  N'Tươi mát, rạng rỡ mỗi ngày', 'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '/shop.html', N'Khám phá ngay', '#fff0f6', 1, 1),
            (N'Giảm 30% Skincare',        N'Áp dụng cho mọi sản phẩm dưỡng da', 'https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=1200', '/shop.html?cat=1', N'Mua sắm', '#ffd6e7', 2, 1),
            (N'Son Lì Black Rouge Mới',  N'Bộ sưu tập 12 màu giới hạn',  'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=1200', '/shop.html?cat=2', N'Xem ngay', '#fce7f3', 3, 1),
            (N'Nước Hoa Cao Cấp',         N'Hương thơm tinh tế, lưu hương lâu', 'https://images.unsplash.com/photo-1541643600914-78b08468370c?w=1200', '/shop.html?cat=4', N'Khám phá', '#fdf2f8', 4, 1),
            (N'Mặt Nạ Mediheal',          N'Mua 5 tặng 1, áp dụng đến hết tháng', 'https://images.unsplash.com/photo-1571781926291-c477ebfd024b?w=1200', '/shop.html?cat=8', N'Săn ưu đãi', '#fdf2f8', 5, 1),
            (N'Free Ship Toàn Quốc',      N'Đơn từ 500.000₫', 'https://images.unsplash.com/photo-1556228453-efd6c1ff04f6?w=1200', '/shop.html', N'Đặt hàng', '#fff0f6', 6, 1),
            (N'Banner Tết 2025 (đã ẩn)', N'Chương trình cũ', 'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#', N'', '#fff', 7, 0),
            (N'Banner Black Friday (đã ẩn)', N'Hết hạn', 'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#', N'', '#fff', 8, 0),
            (N'Banner 8/3 (đã ẩn)',       N'Hết hạn', 'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#', N'', '#fff', 9, 0),
            (N'Banner Noel (đã ẩn)',      N'Hết hạn', 'https://images.unsplash.com/photo-1607602132700-068258431c6c?w=1200', '#', N'', '#fff', 10, 0)
        ) AS v(Title, Subtitle, ImageUrl, LinkUrl, ButtonText, BgColor, SortOrder, IsActive)
    )
    INSERT INTO Banners (Title, Subtitle, ImageUrl, LinkUrl, ButtonText, BgColor, SortOrder, IsActive, CreatedAt)
    SELECT bd.Title, bd.Subtitle, bd.ImageUrl, bd.LinkUrl, bd.ButtonText, bd.BgColor, bd.SortOrder, bd.IsActive, GETDATE()
    FROM BannerData bd
    WHERE NOT EXISTS (SELECT 1 FROM Banners b WHERE b.Title = bd.Title);

    SET @cnt = @@ROWCOUNT;
    PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' banner mới';
END
ELSE
BEGIN
    PRINT N'    - (skip) Bảng Banners chưa tồn tại — chạy backend để EF tạo trước';
END

/* ============================================================
   STEP 5 — VOUCHERS (10 mã)
   ============================================================ */
PRINT N'';
PRINT N'[5/8] Vouchers...';

IF OBJECT_ID('Vouchers', 'U') IS NOT NULL
BEGIN
    ;WITH VoucherData(Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, StartDate, ExpiryDate, IsActive) AS (
        SELECT * FROM (VALUES
            ('WELCOME10',    CAST(N'Giảm 10% cho khách hàng mới — tối đa 50K' AS NVARCHAR(200)),    'percent',  CAST(10  AS DECIMAL(18,2)), CAST(50000   AS DECIMAL(18,2)), CAST(200000 AS DECIMAL(18,2)), 1000, DATEADD(MONTH, -2, GETDATE()), DATEADD(MONTH, 6, GETDATE()),  CAST(1 AS BIT)),
            ('SUMMER20',     CAST(N'Giảm 20% cho đơn hàng từ 500K — tối đa 100K' AS NVARCHAR(200)), 'percent',  CAST(20  AS DECIMAL(18,2)), CAST(100000  AS DECIMAL(18,2)), CAST(500000 AS DECIMAL(18,2)), 500,  DATEADD(MONTH, -1, GETDATE()), DATEADD(MONTH, 3, GETDATE()),  CAST(1 AS BIT)),
            ('FREESHIP',     CAST(N'Miễn phí vận chuyển toàn quốc đơn từ 300K' AS NVARCHAR(200)),    'fixed',    CAST(30000 AS DECIMAL(18,2)),CAST(30000  AS DECIMAL(18,2)), CAST(300000 AS DECIMAL(18,2)), 9999, DATEADD(MONTH, -3, GETDATE()), DATEADD(YEAR, 1, GETDATE()),   CAST(1 AS BIT)),
            ('SKINCARE15',   CAST(N'Giảm 15% sản phẩm chăm sóc da' AS NVARCHAR(200)),                'percent',  CAST(15  AS DECIMAL(18,2)), CAST(150000  AS DECIMAL(18,2)), CAST(0      AS DECIMAL(18,2)), 200,  DATEADD(MONTH, -1, GETDATE()), DATEADD(MONTH, 2, GETDATE()),  CAST(1 AS BIT)),
            ('VIP50K',       CAST(N'Giảm thẳng 50K cho khách VIP' AS NVARCHAR(200)),                 'fixed',    CAST(50000 AS DECIMAL(18,2)),CAST(50000  AS DECIMAL(18,2)), CAST(800000 AS DECIMAL(18,2)), 100,  DATEADD(MONTH, -2, GETDATE()), DATEADD(MONTH, 4, GETDATE()),  CAST(1 AS BIT)),
            ('NEWUSER100K',  CAST(N'Tặng 100K cho đơn đầu tiên từ 1 triệu' AS NVARCHAR(200)),         'fixed',    CAST(100000 AS DECIMAL(18,2)),CAST(100000 AS DECIMAL(18,2)),CAST(1000000 AS DECIMAL(18,2)),300,  DATEADD(MONTH, -1, GETDATE()), DATEADD(MONTH, 6, GETDATE()),  CAST(1 AS BIT)),
            ('LIPSTICK25',   CAST(N'Giảm 25% son môi — chỉ áp dụng tháng này' AS NVARCHAR(200)),     'percent',  CAST(25  AS DECIMAL(18,2)), CAST(200000  AS DECIMAL(18,2)), CAST(0      AS DECIMAL(18,2)), 150,  GETDATE(),                     DATEADD(MONTH, 1, GETDATE()),  CAST(1 AS BIT)),
            ('BLACKFRIDAY',  CAST(N'BLACKFRIDAY — đã hết hạn' AS NVARCHAR(200)),                     'percent',  CAST(30  AS DECIMAL(18,2)), CAST(500000  AS DECIMAL(18,2)), CAST(1000000 AS DECIMAL(18,2)),9999, DATEADD(YEAR, -1, GETDATE()), DATEADD(MONTH, -10, GETDATE()), CAST(0 AS BIT)),
            ('TET2025',      CAST(N'Tết 2025 — đã kết thúc' AS NVARCHAR(200)),                       'percent',  CAST(20  AS DECIMAL(18,2)), CAST(300000  AS DECIMAL(18,2)), CAST(500000 AS DECIMAL(18,2)), 999,  DATEADD(MONTH, -8, GETDATE()), DATEADD(MONTH, -6, GETDATE()),  CAST(0 AS BIT)),
            ('FLASH5',       CAST(N'Flash sale 5% cho mọi đơn — không min' AS NVARCHAR(200)),         'percent',  CAST(5   AS DECIMAL(18,2)), CAST(50000   AS DECIMAL(18,2)), CAST(0      AS DECIMAL(18,2)), 9999, DATEADD(MONTH, -1, GETDATE()), DATEADD(MONTH, 12, GETDATE()), CAST(1 AS BIT))
        ) AS v(Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, StartDate, ExpiryDate, IsActive)
    )
    INSERT INTO Vouchers (Code, Description, DiscountType, DiscountValue, MaxDiscount, MinOrderAmount, UsageLimit, UsedCount, StartDate, ExpiryDate, IsActive, CreatedAt)
    SELECT vd.Code, vd.Description, vd.DiscountType, vd.DiscountValue, vd.MaxDiscount, vd.MinOrderAmount, vd.UsageLimit, 0, vd.StartDate, vd.ExpiryDate, vd.IsActive, GETDATE()
    FROM VoucherData vd
    WHERE NOT EXISTS (SELECT 1 FROM Vouchers v WHERE v.Code = vd.Code);

    SET @cnt = @@ROWCOUNT;
    PRINT N'    + Đã thêm ' + CAST(@cnt AS NVARCHAR) + N' voucher mới';
END
ELSE
BEGIN
    PRINT N'    - (skip) Bảng Vouchers chưa tồn tại';
END

/* ============================================================
   STEP 6 — ORDERS + ORDERDETAILS (~80 đơn, status đa dạng)
   ============================================================ */
PRINT N'';
PRINT N'[6/8] Orders + OrderDetails...';

DECLARE @existingOrders INT = (SELECT COUNT(*) FROM Orders);
IF @existingOrders < 60
BEGIN
    DECLARE @targetOrders INT = 80;
    DECLARE @i INT = 0;

    /* Danh sách khách hàng + sản phẩm để random pick */
    DECLARE @CustomerIds TABLE (RowId INT IDENTITY(1,1), UserId NVARCHAR(450));
    INSERT INTO @CustomerIds (UserId)
    SELECT Id FROM Users WHERE UserType = 0 AND IsActive = 1;

    DECLARE @ProductIds TABLE (RowId INT IDENTITY(1,1), ProductId INT, Price DECIMAL(18,2));
    INSERT INTO @ProductIds (ProductId, Price)
    SELECT Id, Price FROM Products WHERE IsActive = 1;

    DECLARE @customerCount INT = (SELECT COUNT(*) FROM @CustomerIds);
    DECLARE @productCount  INT = (SELECT COUNT(*) FROM @ProductIds);

    IF @customerCount > 0 AND @productCount > 0
    BEGIN
        DECLARE @newOrders INT = @targetOrders - @existingOrders;
        WHILE @i < @newOrders
        BEGIN
            DECLARE @userRow INT = (ABS(CHECKSUM(NEWID())) % @customerCount) + 1;
            DECLARE @userId NVARCHAR(450) = (SELECT UserId FROM @CustomerIds WHERE RowId = @userRow);

            /* Status weighted: 40% COMPLETED, 20% CONFIRMED, 15% SHIPPING, 15% PENDING, 10% CANCELLED */
            DECLARE @r INT = ABS(CHECKSUM(NEWID())) % 100;
            DECLARE @status NVARCHAR(20) =
                CASE
                    WHEN @r < 40 THEN 'COMPLETED'
                    WHEN @r < 60 THEN 'CONFIRMED'
                    WHEN @r < 75 THEN 'SHIPPING'
                    WHEN @r < 90 THEN 'PENDING'
                    ELSE 'CANCELLED'
                END;

            /* Ngày đặt: phân bố 180 ngày qua, càng gần càng nhiều */
            DECLARE @daysAgo INT = ABS(CHECKSUM(NEWID())) % 180;
            DECLARE @orderDate DATETIME2(7) = DATEADD(MINUTE, -ABS(CHECKSUM(NEWID())) % 1440,
                                                      DATEADD(DAY, -@daysAgo, GETDATE()));

            /* Địa chỉ và ghi chú */
            DECLARE @addr NVARCHAR(500) =
                (SELECT TOP 1 v FROM (VALUES
                    (N'Số 123 Lê Lợi, Quận 1, TP.HCM'),
                    (N'45 Nguyễn Huệ, Quận 1, TP.HCM'),
                    (N'Tòa nhà Vincom, Hai Bà Trưng, Hà Nội'),
                    (N'78 Cầu Giấy, Hà Nội'),
                    (N'200 Trần Phú, Quận 5, TP.HCM'),
                    (N'Block C1, Vinhomes Smart City, Hà Nội'),
                    (N'52 Bạch Đằng, Đà Nẵng'),
                    (N'Phú Mỹ Hưng, Quận 7, TP.HCM'),
                    (N'15 Lê Thái Tổ, Hoàn Kiếm, Hà Nội'),
                    (N'168 Hùng Vương, Hải Phòng')
                ) AS x(v) ORDER BY NEWID());

            -- LƯU Ý: entity Order.Note KHÔNG nullable → phải dùng '' thay NULL
            DECLARE @note NVARCHAR(500) =
                CASE WHEN ABS(CHECKSUM(NEWID())) % 3 = 0
                     THEN (SELECT TOP 1 v FROM (VALUES
                            (N'Gọi trước khi giao'),
                            (N'Để hàng ở bảo vệ'),
                            (N'Giao trong giờ hành chính'),
                            (N'Gói cẩn thận, dễ vỡ'),
                            (N'Khách hàng quen'),
                            (N'Đổi quà nếu hết hàng'))
                            AS x(v) ORDER BY NEWID())
                     ELSE N''
                END;

            INSERT INTO Orders (UserId, OrderDate, UpdatedAt, TotalAmount, Status, ShippingAddress, Note)
            VALUES (@userId, @orderDate,
                    CASE WHEN @status IN ('COMPLETED','SHIPPING','CONFIRMED','CANCELLED')
                         THEN DATEADD(HOUR, ABS(CHECKSUM(NEWID())) % 72, @orderDate)
                         ELSE NULL
                    END,
                    0, /* TotalAmount sẽ update sau */
                    @status, @addr, @note);

            DECLARE @orderId INT = SCOPE_IDENTITY();

            /* Insert 1-5 OrderDetails */
            DECLARE @itemCount INT = (ABS(CHECKSUM(NEWID())) % 5) + 1;
            DECLARE @j INT = 0;
            DECLARE @addedProducts TABLE (ProductId INT);
            DELETE FROM @addedProducts;

            WHILE @j < @itemCount
            BEGIN
                DECLARE @pRow INT = (ABS(CHECKSUM(NEWID())) % @productCount) + 1;
                DECLARE @pid INT, @price DECIMAL(18,2);
                SELECT @pid = ProductId, @price = Price FROM @ProductIds WHERE RowId = @pRow;

                /* Skip nếu đã có trong order này */
                IF NOT EXISTS (SELECT 1 FROM @addedProducts WHERE ProductId = @pid)
                BEGIN
                    INSERT INTO @addedProducts VALUES (@pid);
                    DECLARE @qty INT = (ABS(CHECKSUM(NEWID())) % 3) + 1;
                    INSERT INTO OrderDetails (OrderId, ProductId, Quantity, UnitPrice)
                    VALUES (@orderId, @pid, @qty, @price);
                END
                SET @j = @j + 1;
            END

            /* Update TotalAmount */
            UPDATE Orders
            SET TotalAmount = (SELECT SUM(Quantity * UnitPrice) FROM OrderDetails WHERE OrderId = @orderId)
            WHERE Id = @orderId;

            SET @i = @i + 1;
        END

        PRINT N'    + Đã tạo ' + CAST(@newOrders AS NVARCHAR) + N' đơn hàng với chi tiết';
    END
    ELSE
    BEGIN
        PRINT N'    - (skip) Chưa có user customer hoặc sản phẩm để tạo đơn';
    END
END
ELSE
BEGIN
    PRINT N'    - (skip) Đã có ' + CAST(@existingOrders AS NVARCHAR) + N' đơn, không cần seed thêm';
END

/* ============================================================
   STEP 7 — REVIEWS (~60 review từ COMPLETED orders)
   ============================================================ */
PRINT N'';
PRINT N'[7/8] Reviews...';

DECLARE @existingReviews INT = (SELECT COUNT(*) FROM Reviews);
IF @existingReviews < 40
BEGIN
    DECLARE @reviewComments TABLE (RowId INT IDENTITY(1,1), Comment NVARCHAR(1000), Rating INT);
    INSERT INTO @reviewComments (Comment, Rating) VALUES
    (N'Sản phẩm rất tốt, da mịn màng hẳn sau 1 tuần dùng. Sẽ mua lại!', 5),
    (N'Giao hàng nhanh, đóng gói cẩn thận. Mùi hương dễ chịu.', 5),
    (N'Chất lượng đúng như mô tả, giá hợp lý. Recommend!', 5),
    (N'Mình đã dùng nhiều lần rồi, vẫn ưng nhất sản phẩm này.', 5),
    (N'Da khô của mình thấy cải thiện rõ rệt sau 2 tuần. Cảm ơn shop!', 5),
    (N'Sản phẩm tốt nhưng giá hơi cao so với kỳ vọng. 4 sao.', 4),
    (N'Hương thơm dịu, không gây kích ứng. Đáng tiền.', 4),
    (N'Ổn, không có gì xuất sắc nhưng đủ dùng.', 4),
    (N'Da mình thấy mềm hơn, hơi bóng nhẹ. OK.', 4),
    (N'Đóng gói đẹp, sản phẩm chất lượng. Sẽ ủng hộ shop tiếp.', 4),
    (N'Mới dùng được vài lần, chưa thấy hiệu quả rõ. 3 sao tạm.', 3),
    (N'Bình thường, không có gì đặc biệt. Mong shop cải thiện mùi hương.', 3),
    (N'Sản phẩm tạm được, không hợp da mình lắm.', 3),
    (N'Không hợp da nhạy cảm như mình. Bị kích ứng nhẹ.', 2),
    (N'Mùi hắc quá, mình không hợp. Phí tiền.', 2),
    (N'Sản phẩm bị bóp méo, có vẻ hàng cũ. Thất vọng.', 1);

    DECLARE @completedOrders TABLE (RowId INT IDENTITY(1,1), UserId NVARCHAR(450), ProductId INT, OrderDate DATETIME2(7));
    INSERT INTO @completedOrders (UserId, ProductId, OrderDate)
    SELECT DISTINCT o.UserId, od.ProductId, o.OrderDate
    FROM Orders o
    JOIN OrderDetails od ON o.Id = od.OrderId
    WHERE o.Status = 'COMPLETED'
      AND NOT EXISTS (
          SELECT 1 FROM Reviews r
          WHERE r.UserId = o.UserId AND r.ProductId = od.ProductId
      );

    DECLARE @cmtCount INT = (SELECT COUNT(*) FROM @reviewComments);
    DECLARE @poolCount INT = (SELECT COUNT(*) FROM @completedOrders);
    DECLARE @reviewTarget INT = 60;
    DECLARE @ri INT = 0;
    DECLARE @actual INT = 0;

    WHILE @ri < @reviewTarget AND @poolCount > 0
    BEGIN
        DECLARE @poolRow INT = (ABS(CHECKSUM(NEWID())) % @poolCount) + 1;
        DECLARE @cmtRow INT = (ABS(CHECKSUM(NEWID())) % @cmtCount) + 1;

        DECLARE @rvUserId NVARCHAR(450), @rvProductId INT, @rvOrderDate DATETIME2(7);
        SELECT @rvUserId = UserId, @rvProductId = ProductId, @rvOrderDate = OrderDate
        FROM @completedOrders WHERE RowId = @poolRow;

        DECLARE @rvComment NVARCHAR(1000), @rvRating INT;
        SELECT @rvComment = Comment, @rvRating = Rating
        FROM @reviewComments WHERE RowId = @cmtRow;

        /* Review chỉ sau khi đơn được giao 1-7 ngày */
        DECLARE @rvDate DATETIME2(7) = DATEADD(DAY, (ABS(CHECKSUM(NEWID())) % 7) + 1, @rvOrderDate);

        IF NOT EXISTS (SELECT 1 FROM Reviews WHERE UserId = @rvUserId AND ProductId = @rvProductId)
        BEGIN
            INSERT INTO Reviews (ProductId, UserId, Rating, Comment, CreatedAt)
            VALUES (@rvProductId, @rvUserId, @rvRating, @rvComment, @rvDate);
            SET @actual = @actual + 1;
        END
        SET @ri = @ri + 1;
    END

    PRINT N'    + Đã thêm ' + CAST(@actual AS NVARCHAR) + N' review mới';
END
ELSE
BEGIN
    PRINT N'    - (skip) Đã có ' + CAST(@existingReviews AS NVARCHAR) + N' review';
END

/* ============================================================
   STEP 8 — CART ITEMS (~30 item rải cho 8-10 user)
   ============================================================ */
PRINT N'';
PRINT N'[8/8] CartItems...';

DECLARE @existingCart INT = (SELECT COUNT(*) FROM CartItems);
IF @existingCart < 20
BEGIN
    DECLARE @activeCustomers TABLE (RowId INT IDENTITY(1,1), UserId NVARCHAR(450));
    INSERT INTO @activeCustomers (UserId)
    SELECT TOP 10 Id FROM Users WHERE UserType = 0 AND IsActive = 1 ORDER BY NEWID();

    DECLARE @prodList TABLE (RowId INT IDENTITY(1,1), ProductId INT);
    INSERT INTO @prodList (ProductId)
    SELECT Id FROM Products WHERE IsActive = 1;

    DECLARE @uCount INT = (SELECT COUNT(*) FROM @activeCustomers);
    DECLARE @pCount INT = (SELECT COUNT(*) FROM @prodList);
    DECLARE @ci INT = 0;
    DECLARE @cartAdded INT = 0;

    DECLARE @attempts INT = 0;
    WHILE @ci < 40 AND @attempts < 200
    BEGIN
        -- Tính @uRow, @pRow vào biến scalar TRƯỚC — SQL Server sẽ re-evaluate
        -- NEWID() cho từng row nếu để inline trong WHERE → null/multi-row error
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

    PRINT N'    + Đã thêm ' + CAST(@cartAdded AS NVARCHAR) + N' item vào ' + CAST(@uCount AS NVARCHAR) + N' giỏ hàng';
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
PRINT N'║                  SEED HOÀN TẤT — TỔNG KẾT                ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

SELECT
    N'Categories' AS [Bảng], (SELECT COUNT(*) FROM Categories)   AS [Số dòng]
UNION ALL SELECT N'Users',         (SELECT COUNT(*) FROM Users)
UNION ALL SELECT N'  └─ Admin',    (SELECT COUNT(*) FROM Users WHERE UserType = 1)
UNION ALL SELECT N'  └─ Customer', (SELECT COUNT(*) FROM Users WHERE UserType = 0)
UNION ALL SELECT N'Products',      (SELECT COUNT(*) FROM Products)
UNION ALL SELECT N'Orders',        (SELECT COUNT(*) FROM Orders)
UNION ALL SELECT N'  └─ COMPLETED',(SELECT COUNT(*) FROM Orders WHERE Status = 'COMPLETED')
UNION ALL SELECT N'  └─ CONFIRMED',(SELECT COUNT(*) FROM Orders WHERE Status = 'CONFIRMED')
UNION ALL SELECT N'  └─ SHIPPING', (SELECT COUNT(*) FROM Orders WHERE Status = 'SHIPPING')
UNION ALL SELECT N'  └─ PENDING',  (SELECT COUNT(*) FROM Orders WHERE Status = 'PENDING')
UNION ALL SELECT N'  └─ CANCELLED',(SELECT COUNT(*) FROM Orders WHERE Status = 'CANCELLED')
UNION ALL SELECT N'OrderDetails',  (SELECT COUNT(*) FROM OrderDetails)
UNION ALL SELECT N'Reviews',       (SELECT COUNT(*) FROM Reviews)
UNION ALL SELECT N'CartItems',     (SELECT COUNT(*) FROM CartItems);

PRINT N'';
PRINT N'✓ Đăng nhập admin: username=admin, password=admin123';
PRINT N'✓ Đăng nhập user : username=lan.nguyen, password=user123';
PRINT N'✓ Mở admin.html / index.html để xem dữ liệu mới';
PRINT N'';
GO
