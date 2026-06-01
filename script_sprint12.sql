-- ============================================================
-- Sprint 12: OAuth Google/Facebook + VNPay + Bank Transfer
--            + WAITING_PAYMENT Flow
-- Run after: script_sprint11.sql
-- ============================================================

USE BaseCoreDB
GO


PRINT 'Sprint 12: OAuth + VNPay + Bank Transfer';

-- ─────────────────────────────────────────────────────────────
-- Section 1: OAuth fields → Users table
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Users') AND name = N'OAuthProvider')
    ALTER TABLE Users ADD OAuthProvider NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Users') AND name = N'OAuthId')
    ALTER TABLE Users ADD OAuthId NVARCHAR(200) NULL;

PRINT 'Section 1 done: Users.OAuthProvider + OAuthId';

-- ─────────────────────────────────────────────────────────────
-- Section 2: Payment fields → Orders table
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Orders') AND name = N'ToProvince')
    ALTER TABLE Orders ADD ToProvince NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Orders') AND name = N'VNPayTransactionId')
    ALTER TABLE Orders ADD VNPayTransactionId NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Orders') AND name = N'PaymentExpireAt')
    ALTER TABLE Orders ADD PaymentExpireAt DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Orders') AND name = N'BankTransferConfirmedAt')
    ALTER TABLE Orders ADD BankTransferConfirmedAt DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Orders') AND name = N'BankTransferConfirmedBy')
    ALTER TABLE Orders ADD BankTransferConfirmedBy NVARCHAR(450) NULL;

PRINT 'Section 2 done: Orders payment fields';

-- ─────────────────────────────────────────────────────────────
-- Section 3: ToProvince → SubOrders table
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'SubOrders') AND name = N'ToProvince')
    ALTER TABLE SubOrders ADD ToProvince NVARCHAR(100) NULL;

PRINT 'Section 3 done: SubOrders.ToProvince';

-- ─────────────────────────────────────────────────────────────
-- Section 4: Index hỗ trợ tra cứu OAuth
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Users') AND name = N'IX_Users_OAuth')
    CREATE INDEX IX_Users_OAuth ON Users (OAuthProvider, OAuthId);

PRINT 'Section 4 done: Users OAuth index';

-- ─────────────────────────────────────────────────────────────
-- Section 5: Index PaymentExpireAt để auto-expire job nhanh
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'Orders') AND name = N'IX_Orders_PaymentExpireAt')
    CREATE INDEX IX_Orders_PaymentExpireAt ON Orders (PaymentExpireAt)
    WHERE PaymentExpireAt IS NOT NULL;

PRINT 'Section 5 done: Orders PaymentExpireAt index';

-- ─────────────────────────────────────────────────────────────
-- Section 6: Xác nhận các cột Sprint 11 đã tồn tại (guard)
-- Chạy sau script_sprint11.sql; nếu chưa chạy thì tạo ở đây
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Shops') AND name = N'Province')
    ALTER TABLE Shops ADD Province NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Shops') AND name = N'Region')
    ALTER TABLE Shops ADD Region NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'UserAddresses') AND name = N'Province')
    ALTER TABLE UserAddresses ADD Province NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'UserAddresses') AND name = N'Region')
    ALTER TABLE UserAddresses ADD Region NVARCHAR(20) NULL;

-- PaymentMethod column width: đảm bảo chứa được 'WAITING_PAYMENT' (15 chars)
-- PaymentStatus column cần >= 20 chars (đã là NVARCHAR(20) trong migrations)

PRINT 'Section 6 done: Sprint 11 guard columns';

PRINT '✅ Sprint 12 SQL hoàn tất!';
GO


--bsung để fix lỗi 
USE BaseCoreDB
GO

-- Fix PaymentExpireAt (script_sprint12 đang dùng nhưng chưa có)
IF COL_LENGTH('Orders','PaymentExpireAt') IS NULL
  ALTER TABLE Orders ADD 
    PaymentExpireAt DATETIME2 NULL;
GO

-- Các cột còn lại của Sprint 12
IF COL_LENGTH('Orders','ToProvince') IS NULL
  ALTER TABLE Orders ADD 
    ToProvince NVARCHAR(100) NULL;
GO

IF COL_LENGTH('Orders','VNPayTransactionId') IS NULL
  ALTER TABLE Orders ADD 
    VNPayTransactionId NVARCHAR(100) NULL;
GO

IF COL_LENGTH('Orders','BankTransferConfirmedAt') IS NULL
  ALTER TABLE Orders ADD 
    BankTransferConfirmedAt DATETIME2 NULL;
GO

IF COL_LENGTH('Orders','BankTransferConfirmedBy') IS NULL
  ALTER TABLE Orders ADD 
    BankTransferConfirmedBy NVARCHAR(450) NULL;
GO

IF COL_LENGTH('Users','OAuthProvider') IS NULL
  ALTER TABLE Users ADD 
    OAuthProvider NVARCHAR(20) NULL;
GO

IF COL_LENGTH('Users','OAuthId') IS NULL
  ALTER TABLE Users ADD 
    OAuthId NVARCHAR(200) NULL;
GO

IF COL_LENGTH('SubOrders','ToProvince') IS NULL
  ALTER TABLE SubOrders ADD 
    ToProvince NVARCHAR(100) NULL;
GO

-- Verify
SELECT 
  COL_LENGTH('Orders','PaymentExpireAt') as PaymentExpireAt,
  COL_LENGTH('Orders','VNPayTransactionId') as VNPayTxId,
  COL_LENGTH('Orders','BankTransferConfirmedAt') as BankConfirmedAt,
  COL_LENGTH('Users','OAuthProvider') as OAuthProvider;

PRINT 'Sprint 12 columns done!';
GO