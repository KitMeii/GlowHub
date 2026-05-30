-- ============================================================
--  Sprint 11: Multi-shop Cart + Race Condition Flash Sale
--             + CustomerWallet + Dynamic Shipping Fee
--  Database: BaseCoreDB (SQL Server)
--  Run order: từ trên xuống dưới, từng block
-- ============================================================

-- ─────────────────────────────────────────────────────────
-- 1. ALTER Products — thêm WeightGram (dùng tính phí ship)
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Products') AND name = 'WeightGram'
)
BEGIN
    ALTER TABLE Products ADD WeightGram INT NOT NULL DEFAULT 500;
    PRINT 'Products.WeightGram added';
END
ELSE PRINT 'Products.WeightGram already exists';

-- ─────────────────────────────────────────────────────────
-- 2. ALTER UserAddresses — thêm Province + Region
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserAddresses') AND name = 'Province'
)
BEGIN
    ALTER TABLE UserAddresses ADD Province NVARCHAR(100) NULL;
    PRINT 'UserAddresses.Province added';
END
ELSE PRINT 'UserAddresses.Province already exists';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('UserAddresses') AND name = 'Region'
)
BEGIN
    -- 'NORTH' | 'CENTRAL' | 'SOUTH' | 'ISLAND' | 'OTHER'
    ALTER TABLE UserAddresses ADD Region NVARCHAR(20) NULL;
    PRINT 'UserAddresses.Region added';
END
ELSE PRINT 'UserAddresses.Region already exists';

-- ─────────────────────────────────────────────────────────
-- 3. ALTER Shops — thêm Province + Region
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Shops') AND name = 'Province'
)
BEGIN
    ALTER TABLE Shops ADD Province NVARCHAR(100) NULL;
    PRINT 'Shops.Province added';
END
ELSE PRINT 'Shops.Province already exists';

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Shops') AND name = 'Region'
)
BEGIN
    -- 'NORTH' | 'CENTRAL' | 'SOUTH' | 'ISLAND' | 'OTHER'
    ALTER TABLE Shops ADD Region NVARCHAR(20) NULL;
    PRINT 'Shops.Region added';
END
ELSE PRINT 'Shops.Region already exists';

-- ─────────────────────────────────────────────────────────
-- 4. ALTER FlashSaleProducts — thêm RemainingQuantity (fix race condition)
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('FlashSaleProducts') AND name = 'RemainingQuantity'
)
BEGIN
    ALTER TABLE FlashSaleProducts ADD RemainingQuantity INT NOT NULL DEFAULT 0;
    -- Backfill từ Quantity - SoldCount
    UPDATE FlashSaleProducts SET RemainingQuantity = CASE WHEN Quantity - SoldCount > 0 THEN Quantity - SoldCount ELSE 0 END;
    PRINT 'FlashSaleProducts.RemainingQuantity added + backfilled';
END
ELSE PRINT 'FlashSaleProducts.RemainingQuantity already exists';

-- ─────────────────────────────────────────────────────────
-- 5. CREATE SubOrders — đơn hàng theo shop
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubOrders')
BEGIN
    CREATE TABLE SubOrders (
        Id                  INT IDENTITY(1,1) PRIMARY KEY,
        OrderId             INT NOT NULL,
        ShopId              NVARCHAR(450) NOT NULL,
        SubOrderCode        NVARCHAR(20)  NULL,
        Status              NVARCHAR(20)  NOT NULL DEFAULT 'PENDING',
        TotalAmount         DECIMAL(18,2) NOT NULL DEFAULT 0,
        ShippingFee         DECIMAL(18,2) NOT NULL DEFAULT 0,
        FinalAmount         DECIMAL(18,2) NOT NULL DEFAULT 0,
        ProductRevenue      DECIMAL(18,2) NOT NULL DEFAULT 0,
        CommissionRate      DECIMAL(5,2)  NOT NULL DEFAULT 0,
        CommissionAmount    DECIMAL(18,2) NOT NULL DEFAULT 0,
        SellerPayoutAmount  DECIMAL(18,2) NOT NULL DEFAULT 0,
        ShopVoucherDiscount DECIMAL(18,2) NOT NULL DEFAULT 0,
        PayoutStatus        NVARCHAR(20)  NOT NULL DEFAULT 'PENDING',
        WalletReleaseAt     DATETIME2     NULL,
        TrackingCode        NVARCHAR(100) NULL,
        CancelReason        NVARCHAR(500) NULL,
        Note                NVARCHAR(500) NULL,
        CreatedAt           DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt           DATETIME2     NULL,
        CONSTRAINT FK_SubOrders_Orders FOREIGN KEY (OrderId)
            REFERENCES Orders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_SubOrders_Shops  FOREIGN KEY (ShopId)
            REFERENCES Shops(Id)
    );
    CREATE INDEX IX_SubOrders_OrderId ON SubOrders(OrderId);
    CREATE INDEX IX_SubOrders_ShopId  ON SubOrders(ShopId);
    PRINT 'SubOrders table created';
END
ELSE PRINT 'SubOrders already exists';

-- ─────────────────────────────────────────────────────────
-- 6. CREATE SubOrderItems
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SubOrderItems')
BEGIN
    CREATE TABLE SubOrderItems (
        Id           INT IDENTITY(1,1) PRIMARY KEY,
        SubOrderId   INT NOT NULL,
        ProductId    INT NOT NULL,
        Quantity     INT           NOT NULL DEFAULT 1,
        UnitPrice    DECIMAL(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT FK_SubOrderItems_SubOrders FOREIGN KEY (SubOrderId)
            REFERENCES SubOrders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_SubOrderItems_Products  FOREIGN KEY (ProductId)
            REFERENCES Products(Id)
    );
    CREATE INDEX IX_SubOrderItems_SubOrderId ON SubOrderItems(SubOrderId);
    PRINT 'SubOrderItems table created';
END
ELSE PRINT 'SubOrderItems already exists';

-- ─────────────────────────────────────────────────────────
-- 7. CREATE CustomerWallets
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerWallets')
BEGIN
    CREATE TABLE CustomerWallets (
        UserId        NVARCHAR(450) PRIMARY KEY,
        Balance       DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalReceived DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalSpent    DECIMAL(18,2) NOT NULL DEFAULT 0,
        UpdatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CustomerWallets_Users FOREIGN KEY (UserId)
            REFERENCES Users(Id) ON DELETE CASCADE
    );
    PRINT 'CustomerWallets table created';
END
ELSE PRINT 'CustomerWallets already exists';

-- ─────────────────────────────────────────────────────────
-- 8. CREATE CustomerWalletTransactions
-- ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CustomerWalletTransactions')
BEGIN
    CREATE TABLE CustomerWalletTransactions (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        UserId        NVARCHAR(450) NOT NULL,
        Type          NVARCHAR(20)  NOT NULL,   -- TOPUP | REFUND | SPEND | ADJUSTMENT
        Amount        DECIMAL(18,2) NOT NULL DEFAULT 0,
        BalanceBefore DECIMAL(18,2) NOT NULL DEFAULT 0,
        BalanceAfter  DECIMAL(18,2) NOT NULL DEFAULT 0,
        Note          NVARCHAR(500) NULL,
        OrderId       INT NULL,
        CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_CustWalletTx_Users  FOREIGN KEY (UserId)
            REFERENCES Users(Id) ON DELETE CASCADE,
        CONSTRAINT FK_CustWalletTx_Orders FOREIGN KEY (OrderId)
            REFERENCES Orders(Id) ON DELETE NO ACTION
    );
    CREATE INDEX IX_CustWalletTx_UserId ON CustomerWalletTransactions(UserId);
    PRINT 'CustomerWalletTransactions table created';
END
ELSE PRINT 'CustomerWalletTransactions already exists';

-- ─────────────────────────────────────────────────────────
-- 9. SEED: Shop Province + Region
--    Mapping: NORTH | CENTRAL | SOUTH | ISLAND | OTHER
-- ─────────────────────────────────────────────────────────

-- Beauty Store (demo shop) → SOUTH (TP.HCM)
UPDATE Shops
SET Province = 'Ho Chi Minh', Region = 'SOUTH'
WHERE Id = 'cd9cdd36-30eb-42bd-866c-0cae6ff0fe3e';
PRINT 'Beauty Store region set to SOUTH';

-- Backfill tất cả shop còn NULL Region:
--   Nếu Province chứa từ khoá → tự động map vùng
UPDATE Shops SET Region = 'SOUTH' WHERE Region IS NULL AND (
    Province LIKE '%Hồ Chí Minh%' OR Province LIKE '%Ho Chi Minh%' OR Province LIKE '%HCM%'
    OR Province LIKE '%Bình Dương%' OR Province LIKE '%Đồng Nai%' OR Province LIKE '%Long An%'
    OR Province LIKE '%Cần Thơ%' OR Province LIKE '%An Giang%' OR Province LIKE '%Kiên Giang%'
    OR Province LIKE '%Tiền Giang%' OR Province LIKE '%Bến Tre%' OR Province LIKE '%Vĩnh Long%'
    OR Province LIKE '%Trà Vinh%' OR Province LIKE '%Đồng Tháp%' OR Province LIKE '%Hậu Giang%'
    OR Province LIKE '%Sóc Trăng%' OR Province LIKE '%Bạc Liêu%' OR Province LIKE '%Cà Mau%'
    OR Province LIKE '%Tây Ninh%' OR Province LIKE '%Bình Phước%' OR Province LIKE '%Vũng Tàu%'
);
UPDATE Shops SET Region = 'NORTH' WHERE Region IS NULL AND (
    Province LIKE '%Hà Nội%' OR Province LIKE '%Ha Noi%' OR Province LIKE '%Hải Phòng%'
    OR Province LIKE '%Quảng Ninh%' OR Province LIKE '%Hải Dương%' OR Province LIKE '%Hưng Yên%'
    OR Province LIKE '%Thái Bình%' OR Province LIKE '%Nam Định%' OR Province LIKE '%Ninh Bình%'
    OR Province LIKE '%Vĩnh Phúc%' OR Province LIKE '%Bắc Ninh%' OR Province LIKE '%Bắc Giang%'
    OR Province LIKE '%Thái Nguyên%' OR Province LIKE '%Lạng Sơn%' OR Province LIKE '%Cao Bằng%'
    OR Province LIKE '%Hà Giang%' OR Province LIKE '%Lào Cai%' OR Province LIKE '%Yên Bái%'
    OR Province LIKE '%Phú Thọ%' OR Province LIKE '%Sơn La%' OR Province LIKE '%Điện Biên%'
    OR Province LIKE '%Lai Châu%' OR Province LIKE '%Hòa Bình%' OR Province LIKE '%Tuyên Quang%'
    OR Province LIKE '%Bắc Kạn%'
);
UPDATE Shops SET Region = 'CENTRAL' WHERE Region IS NULL AND (
    Province LIKE '%Thanh Hóa%' OR Province LIKE '%Nghệ An%' OR Province LIKE '%Hà Tĩnh%'
    OR Province LIKE '%Quảng Bình%' OR Province LIKE '%Quảng Trị%' OR Province LIKE '%Huế%'
    OR Province LIKE '%Đà Nẵng%' OR Province LIKE '%Quảng Nam%' OR Province LIKE '%Quảng Ngãi%'
    OR Province LIKE '%Bình Định%' OR Province LIKE '%Phú Yên%' OR Province LIKE '%Khánh Hòa%'
    OR Province LIKE '%Ninh Thuận%' OR Province LIKE '%Bình Thuận%' OR Province LIKE '%Kon Tum%'
    OR Province LIKE '%Gia Lai%' OR Province LIKE '%Đắk Lắk%' OR Province LIKE '%Đắk Nông%'
    OR Province LIKE '%Lâm Đồng%' OR Province LIKE '%Đà Lạt%' OR Province LIKE '%Nha Trang%'
);
UPDATE Shops SET Region = 'ISLAND' WHERE Region IS NULL AND (
    Province LIKE '%Phú Quốc%' OR Province LIKE '%Côn Đảo%' OR Province LIKE '%Hoàng Sa%'
    OR Province LIKE '%Trường Sa%' OR Province LIKE '%Lý Sơn%'
);
-- Fallback
UPDATE Shops SET Region = 'OTHER' WHERE Region IS NULL;

PRINT 'Shops.Region backfill complete';

-- ─────────────────────────────────────────────────────────
-- 10. SEED: UserAddresses Region backfill (tương tự)
-- ─────────────────────────────────────────────────────────
UPDATE UserAddresses SET Region = 'SOUTH' WHERE Region IS NULL AND Province IS NOT NULL AND (
    Province LIKE '%Hồ Chí Minh%' OR Province LIKE '%Ho Chi Minh%' OR Province LIKE '%HCM%'
    OR Province LIKE '%Bình Dương%' OR Province LIKE '%Đồng Nai%' OR Province LIKE '%Long An%'
    OR Province LIKE '%Cần Thơ%' OR Province LIKE '%An Giang%' OR Province LIKE '%Kiên Giang%'
    OR Province LIKE '%Tiền Giang%' OR Province LIKE '%Bến Tre%' OR Province LIKE '%Vĩnh Long%'
    OR Province LIKE '%Trà Vinh%' OR Province LIKE '%Đồng Tháp%' OR Province LIKE '%Hậu Giang%'
    OR Province LIKE '%Sóc Trăng%' OR Province LIKE '%Bạc Liêu%' OR Province LIKE '%Cà Mau%'
    OR Province LIKE '%Tây Ninh%' OR Province LIKE '%Bình Phước%' OR Province LIKE '%Vũng Tàu%'
);
UPDATE UserAddresses SET Region = 'NORTH' WHERE Region IS NULL AND Province IS NOT NULL AND (
    Province LIKE '%Hà Nội%' OR Province LIKE '%Ha Noi%' OR Province LIKE '%Hải Phòng%'
    OR Province LIKE '%Quảng Ninh%' OR Province LIKE '%Hải Dương%' OR Province LIKE '%Hưng Yên%'
    OR Province LIKE '%Thái Bình%' OR Province LIKE '%Nam Định%' OR Province LIKE '%Ninh Bình%'
    OR Province LIKE '%Vĩnh Phúc%' OR Province LIKE '%Bắc Ninh%' OR Province LIKE '%Bắc Giang%'
    OR Province LIKE '%Thái Nguyên%' OR Province LIKE '%Lạng Sơn%' OR Province LIKE '%Cao Bằng%'
    OR Province LIKE '%Hà Giang%' OR Province LIKE '%Lào Cai%' OR Province LIKE '%Yên Bái%'
    OR Province LIKE '%Phú Thọ%' OR Province LIKE '%Sơn La%' OR Province LIKE '%Điện Biên%'
    OR Province LIKE '%Lai Châu%' OR Province LIKE '%Hòa Bình%' OR Province LIKE '%Tuyên Quang%'
    OR Province LIKE '%Bắc Kạn%'
);
UPDATE UserAddresses SET Region = 'CENTRAL' WHERE Region IS NULL AND Province IS NOT NULL AND (
    Province LIKE '%Thanh Hóa%' OR Province LIKE '%Nghệ An%' OR Province LIKE '%Hà Tĩnh%'
    OR Province LIKE '%Quảng Bình%' OR Province LIKE '%Quảng Trị%' OR Province LIKE '%Huế%'
    OR Province LIKE '%Đà Nẵng%' OR Province LIKE '%Quảng Nam%' OR Province LIKE '%Quảng Ngãi%'
    OR Province LIKE '%Bình Định%' OR Province LIKE '%Phú Yên%' OR Province LIKE '%Khánh Hòa%'
    OR Province LIKE '%Ninh Thuận%' OR Province LIKE '%Bình Thuận%' OR Province LIKE '%Kon Tum%'
    OR Province LIKE '%Gia Lai%' OR Province LIKE '%Đắk Lắk%' OR Province LIKE '%Đắk Nông%'
    OR Province LIKE '%Lâm Đồng%'
);
UPDATE UserAddresses SET Region = 'ISLAND' WHERE Region IS NULL AND Province IS NOT NULL AND (
    Province LIKE '%Phú Quốc%' OR Province LIKE '%Côn Đảo%'
);
UPDATE UserAddresses SET Region = 'OTHER' WHERE Region IS NULL AND Province IS NOT NULL;

PRINT 'UserAddresses.Region backfill complete';

-- ─────────────────────────────────────────────────────────
-- DONE
-- ─────────────────────────────────────────────────────────
PRINT '=== Sprint 11 migration complete ===';
