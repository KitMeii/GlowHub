/* ============================================================
   GLOWHUB — FIX Orders.Note NULL gây lỗi 500 ở /api/orders/all
   ------------------------------------------------------------
   Entity Order.cs khai báo:
       public string Note { get; set; }   // không nullable
   Nhưng DB schema cho phép Note NULL → EF SqlNullValueException.
   Fix: chuyển toàn bộ NULL → '' (empty string).
   Idempotent: chạy nhiều lần OK.
   ============================================================ */

USE [BaseCoreDB];
GO
SET NOCOUNT ON;

PRINT N'═══ Fix Orders.Note NULL ═══';

/* Cập nhật Note NULL → '' */
DECLARE @noteCnt INT = (SELECT COUNT(*) FROM Orders WHERE Note IS NULL);
PRINT N'  Số đơn có Note NULL: ' + CAST(@noteCnt AS NVARCHAR);

UPDATE Orders SET Note = N'' WHERE Note IS NULL;
PRINT N'  + Đã cập nhật ' + CAST(@@ROWCOUNT AS NVARCHAR) + N' dòng Orders.Note';

/* Phòng ngừa các cột non-nullable string khác trong Orders */
UPDATE Orders SET ShippingAddress = N'' WHERE ShippingAddress IS NULL;
IF @@ROWCOUNT > 0 PRINT N'  + Đã sửa ShippingAddress NULL';

UPDATE Orders SET Status = N'PENDING' WHERE Status IS NULL OR Status = '';
IF @@ROWCOUNT > 0 PRINT N'  + Đã sửa Status NULL/rỗng';

/* Phòng ngừa Reviews.Comment (entity có thể non-nullable) */
IF EXISTS (SELECT 1 FROM Reviews WHERE Comment IS NULL)
BEGIN
    UPDATE Reviews SET Comment = N'' WHERE Comment IS NULL;
    PRINT N'  + Đã sửa Reviews.Comment NULL';
END

/* Phòng ngừa Users — entity Name/Email/Phone/Position/Image/Contact đều non-null */
UPDATE Users SET Contact  = N'' WHERE Contact  IS NULL;
UPDATE Users SET Position = N'' WHERE Position IS NULL;
UPDATE Users SET Image    = N'' WHERE Image    IS NULL;
UPDATE Users SET Email    = N'' WHERE Email    IS NULL;
UPDATE Users SET Phone    = N'' WHERE Phone    IS NULL;
UPDATE Users SET Name     = N'(Chưa có tên)' WHERE Name IS NULL OR Name = '';

PRINT N'';
PRINT N'✓ Đã fix. Reload admin.html → tab Đơn hàng & Chi tiết đơn sẽ load được.';
GO
