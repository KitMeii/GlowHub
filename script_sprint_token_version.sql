-- ============================================================
-- Sprint: Role Change Force-Logout
-- ============================================================
-- Thêm cột TokenVersion vào bảng Users để cơ chế OnTokenValidated
-- ở APIService / AuthService có thể đối chiếu "tv" claim của JWT
-- với phiên bản hiện tại của user. Khi admin đổi role hoặc ban user,
-- backend tăng TokenVersion ⇒ mọi JWT cũ bị reject ngay request kế tiếp.
--
-- SQL Server (UseSqlServer trong Program.cs)
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE Name = N'TokenVersion'
      AND Object_ID = Object_ID(N'Users')
)
BEGIN
    ALTER TABLE [Users] ADD [TokenVersion] INT NOT NULL CONSTRAINT DF_Users_TokenVersion DEFAULT 0;
    PRINT 'Added column Users.TokenVersion';
END
ELSE
BEGIN
    PRINT 'Column Users.TokenVersion already exists — skipped';
END
GO
