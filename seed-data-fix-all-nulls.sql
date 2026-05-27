/* ============================================================
   GLOWHUB — FIX TOÀN BỘ NULL string gây lỗi EF SqlNullValueException
   ------------------------------------------------------------
   Các Entity (User/Product/Order/Category/Review) khai báo các
   property string KHÔNG nullable, nhưng DB có thể vẫn còn NULL
   từ data cũ. EF Core sẽ throw khi GetString gặp NULL.

   Script này:
     1) DIAGNOSTIC: in số lượng NULL trên từng cột
     2) UPDATE: NULL → '' (hoặc giá trị mặc định)
     3) Re-run diagnostic để verify đã sạch
   Idempotent. Chạy nhiều lần OK.
   ============================================================ */

USE [BaseCoreDB];
GO
SET NOCOUNT ON;

PRINT N'╔══════════════════════════════════════════════════════════╗';
PRINT N'║  DIAGNOSTIC — ĐẾM NULL TRƯỚC KHI FIX                     ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

SELECT 'Users' AS [Bảng],
    SUM(CASE WHEN Name     IS NULL THEN 1 ELSE 0 END) AS [Name],
    SUM(CASE WHEN UserName IS NULL THEN 1 ELSE 0 END) AS [UserName],
    SUM(CASE WHEN Password IS NULL THEN 1 ELSE 0 END) AS [Password],
    SUM(CASE WHEN Contact  IS NULL THEN 1 ELSE 0 END) AS [Contact],
    SUM(CASE WHEN Email    IS NULL THEN 1 ELSE 0 END) AS [Email],
    SUM(CASE WHEN Phone    IS NULL THEN 1 ELSE 0 END) AS [Phone],
    SUM(CASE WHEN Position IS NULL THEN 1 ELSE 0 END) AS [Position],
    SUM(CASE WHEN Image    IS NULL THEN 1 ELSE 0 END) AS [Image]
FROM Users;

SELECT 'Products' AS [Bảng],
    SUM(CASE WHEN Name        IS NULL THEN 1 ELSE 0 END) AS [Name],
    SUM(CASE WHEN ImageUrl    IS NULL THEN 1 ELSE 0 END) AS [ImageUrl],
    SUM(CASE WHEN Description IS NULL THEN 1 ELSE 0 END) AS [Description]
FROM Products;

SELECT 'Orders' AS [Bảng],
    SUM(CASE WHEN UserId          IS NULL THEN 1 ELSE 0 END) AS [UserId],
    SUM(CASE WHEN Status          IS NULL THEN 1 ELSE 0 END) AS [Status],
    SUM(CASE WHEN ShippingAddress IS NULL THEN 1 ELSE 0 END) AS [ShippingAddress],
    SUM(CASE WHEN Note            IS NULL THEN 1 ELSE 0 END) AS [Note]
FROM Orders;

SELECT 'Categories' AS [Bảng],
    SUM(CASE WHEN Name        IS NULL THEN 1 ELSE 0 END) AS [Name],
    SUM(CASE WHEN Description IS NULL THEN 1 ELSE 0 END) AS [Description]
FROM Categories;

IF OBJECT_ID('Reviews', 'U') IS NOT NULL
    SELECT 'Reviews' AS [Bảng],
        SUM(CASE WHEN Comment IS NULL THEN 1 ELSE 0 END) AS [Comment]
    FROM Reviews;

PRINT N'';
PRINT N'╔══════════════════════════════════════════════════════════╗';
PRINT N'║  FIX — UPDATE TẤT CẢ NULL → ''''                         ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

/* ======================== USERS ======================== */
DECLARE @t INT;

UPDATE Users SET Name     = N'(Tên trống)'                            WHERE Name     IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Name      : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET UserName = N'user_' + RIGHT(CAST(NEWID() AS NVARCHAR(40)),8) WHERE UserName IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.UserName  : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET Password = N''                                       WHERE Password IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Password  : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET Contact  = N''                                       WHERE Contact  IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Contact   : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET Email    = N''                                       WHERE Email    IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Email     : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET Phone    = N''                                       WHERE Phone    IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Phone     : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET Position = N''                                       WHERE Position IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Position  : ' + CAST(@t AS NVARCHAR);
UPDATE Users SET Image    = N''                                       WHERE Image    IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Users.Image     : ' + CAST(@t AS NVARCHAR);

/* ======================== PRODUCTS ======================== */
UPDATE Products SET Name        = N'(Sản phẩm chưa đặt tên)' WHERE Name        IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Products.Name        : ' + CAST(@t AS NVARCHAR);
UPDATE Products SET ImageUrl    = N''                        WHERE ImageUrl    IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Products.ImageUrl    : ' + CAST(@t AS NVARCHAR);
UPDATE Products SET Description = N''                        WHERE Description IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Products.Description : ' + CAST(@t AS NVARCHAR);

/* ======================== ORDERS ======================== */
UPDATE Orders SET Status          = N'PENDING' WHERE Status          IS NULL OR Status = '';  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Orders.Status          : ' + CAST(@t AS NVARCHAR);
UPDATE Orders SET ShippingAddress = N''        WHERE ShippingAddress IS NULL;                  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Orders.ShippingAddress : ' + CAST(@t AS NVARCHAR);
UPDATE Orders SET Note            = N''        WHERE Note            IS NULL;                  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Orders.Note            : ' + CAST(@t AS NVARCHAR);

/* ======================== CATEGORIES ======================== */
UPDATE Categories SET Name        = N'(Danh mục)' WHERE Name        IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Categories.Name        : ' + CAST(@t AS NVARCHAR);
UPDATE Categories SET Description = N''           WHERE Description IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Categories.Description : ' + CAST(@t AS NVARCHAR);

/* ======================== REVIEWS ======================== */
IF OBJECT_ID('Reviews', 'U') IS NOT NULL
BEGIN
    UPDATE Reviews SET Comment = N'' WHERE Comment IS NULL;
    SET @t = @@ROWCOUNT;
    IF @t > 0 PRINT N'  Reviews.Comment : ' + CAST(@t AS NVARCHAR);
END

/* ======================== BANNERS ======================== */
IF OBJECT_ID('Banners', 'U') IS NOT NULL
BEGIN
    UPDATE Banners SET Title    = N'(Banner)' WHERE Title    IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Banners.Title    : ' + CAST(@t AS NVARCHAR);
    UPDATE Banners SET ImageUrl = N''         WHERE ImageUrl IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Banners.ImageUrl : ' + CAST(@t AS NVARCHAR);
END

/* ======================== VOUCHERS ======================== */
IF OBJECT_ID('Vouchers', 'U') IS NOT NULL
BEGIN
    UPDATE Vouchers SET Code         = N'CODE_' + CAST(NEWID() AS NVARCHAR(36)) WHERE Code         IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Vouchers.Code         : ' + CAST(@t AS NVARCHAR);
    UPDATE Vouchers SET DiscountType = N'percent'                               WHERE DiscountType IS NULL;  SET @t = @@ROWCOUNT;  IF @t > 0 PRINT N'  Vouchers.DiscountType : ' + CAST(@t AS NVARCHAR);
END

/* ======================== VERIFY ======================== */
PRINT N'';
PRINT N'╔══════════════════════════════════════════════════════════╗';
PRINT N'║  VERIFY — ĐẾM NULL SAU KHI FIX (phải toàn 0)             ║';
PRINT N'╚══════════════════════════════════════════════════════════╝';

SELECT 'Users' AS [Bảng],
    SUM(CASE WHEN Name     IS NULL THEN 1 ELSE 0 END) AS [Name],
    SUM(CASE WHEN UserName IS NULL THEN 1 ELSE 0 END) AS [UserName],
    SUM(CASE WHEN Password IS NULL THEN 1 ELSE 0 END) AS [Password],
    SUM(CASE WHEN Contact  IS NULL THEN 1 ELSE 0 END) AS [Contact],
    SUM(CASE WHEN Email    IS NULL THEN 1 ELSE 0 END) AS [Email],
    SUM(CASE WHEN Phone    IS NULL THEN 1 ELSE 0 END) AS [Phone],
    SUM(CASE WHEN Position IS NULL THEN 1 ELSE 0 END) AS [Position],
    SUM(CASE WHEN Image    IS NULL THEN 1 ELSE 0 END) AS [Image]
FROM Users;

SELECT 'Products' AS [Bảng],
    SUM(CASE WHEN Name        IS NULL THEN 1 ELSE 0 END) AS [Name],
    SUM(CASE WHEN ImageUrl    IS NULL THEN 1 ELSE 0 END) AS [ImageUrl],
    SUM(CASE WHEN Description IS NULL THEN 1 ELSE 0 END) AS [Description]
FROM Products;

SELECT 'Orders' AS [Bảng],
    SUM(CASE WHEN Status          IS NULL THEN 1 ELSE 0 END) AS [Status],
    SUM(CASE WHEN ShippingAddress IS NULL THEN 1 ELSE 0 END) AS [ShippingAddress],
    SUM(CASE WHEN Note            IS NULL THEN 1 ELSE 0 END) AS [Note]
FROM Orders;

PRINT N'';
PRINT N'✓ Đã clean. Reload admin.html → tab Đơn hàng / Chi tiết phải load được.';
PRINT N'  Nếu vẫn 500: copy nội dung response từ Network tab → gửi cho dev.';
GO
