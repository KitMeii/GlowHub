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
    -- 'HCM' | 'HN' | 'OTHER'
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
-- DONE
-- ─────────────────────────────────────────────────────────
PRINT '=== Sprint 11 migration complete ===';
