USE BaseCoreDB
GO

-- 1. Thêm cột WeightGram
IF NOT EXISTS (SELECT 1 FROM sys.columns 
  WHERE object_id = OBJECT_ID('Products') 
  AND name = 'WeightGram')
  ALTER TABLE Products ADD 
    WeightGram INT NOT NULL DEFAULT 500;
GO

-- 2. Thêm cột Province + Region vào UserAddresses
IF NOT EXISTS (SELECT 1 FROM sys.columns 
  WHERE object_id = OBJECT_ID('UserAddresses') 
  AND name = 'Province')
  ALTER TABLE UserAddresses ADD 
    Province NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns 
  WHERE object_id = OBJECT_ID('UserAddresses') 
  AND name = 'Region')
  ALTER TABLE UserAddresses ADD 
    Region NVARCHAR(20) NULL;
GO

-- 3. Thêm cột Province + Region vào Shops
IF NOT EXISTS (SELECT 1 FROM sys.columns 
  WHERE object_id = OBJECT_ID('Shops') 
  AND name = 'Province')
  ALTER TABLE Shops ADD 
    Province NVARCHAR(100) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns 
  WHERE object_id = OBJECT_ID('Shops') 
  AND name = 'Region')
  ALTER TABLE Shops ADD 
    Region NVARCHAR(20) NULL;
GO

-- 4. Thêm cột RemainingQuantity vào FlashSaleProducts
IF NOT EXISTS (SELECT 1 FROM sys.columns 
  WHERE object_id = OBJECT_ID('FlashSaleProducts') 
  AND name = 'RemainingQuantity')
  ALTER TABLE FlashSaleProducts ADD 
    RemainingQuantity INT NOT NULL DEFAULT 0;
GO

-- 5. Backfill RemainingQuantity
UPDATE FlashSaleProducts 
SET RemainingQuantity = CASE 
  WHEN Quantity - SoldCount > 0 
  THEN Quantity - SoldCount 
  ELSE 0 END;
GO

-- 6. Tạo bảng SubOrders
IF NOT EXISTS (SELECT 1 FROM sys.tables 
  WHERE name = 'SubOrders')
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
    CONSTRAINT FK_SubOrders_Orders 
      FOREIGN KEY (OrderId) REFERENCES Orders(Id) 
      ON DELETE CASCADE,
    CONSTRAINT FK_SubOrders_Shops  
      FOREIGN KEY (ShopId) REFERENCES Shops(Id)
  );
  CREATE INDEX IX_SubOrders_OrderId ON SubOrders(OrderId);
  CREATE INDEX IX_SubOrders_ShopId  ON SubOrders(ShopId);
  PRINT 'SubOrders created';
END
ELSE PRINT 'SubOrders already exists';
GO

-- 7. Tạo bảng SubOrderItems
IF NOT EXISTS (SELECT 1 FROM sys.tables 
  WHERE name = 'SubOrderItems')
BEGIN
  CREATE TABLE SubOrderItems (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    SubOrderId INT NOT NULL,
    ProductId  INT NOT NULL,
    Quantity   INT           NOT NULL DEFAULT 1,
    UnitPrice  DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_SubOrderItems_SubOrders 
      FOREIGN KEY (SubOrderId) REFERENCES SubOrders(Id) 
      ON DELETE CASCADE,
    CONSTRAINT FK_SubOrderItems_Products  
      FOREIGN KEY (ProductId) REFERENCES Products(Id)
  );
  CREATE INDEX IX_SubOrderItems_SubOrderId 
    ON SubOrderItems(SubOrderId);
  PRINT 'SubOrderItems created';
END
ELSE PRINT 'SubOrderItems already exists';
GO

-- 8. Tạo bảng CustomerWallets
IF NOT EXISTS (SELECT 1 FROM sys.tables 
  WHERE name = 'CustomerWallets')
BEGIN
  CREATE TABLE CustomerWallets (
    UserId        NVARCHAR(450) PRIMARY KEY,
    Balance       DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalReceived DECIMAL(18,2) NOT NULL DEFAULT 0,
    TotalSpent    DECIMAL(18,2) NOT NULL DEFAULT 0,
    UpdatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CustomerWallets_Users 
      FOREIGN KEY (UserId) REFERENCES Users(Id) 
      ON DELETE CASCADE
  );
  PRINT 'CustomerWallets created';
END
ELSE PRINT 'CustomerWallets already exists';
GO

-- 9. Tạo bảng CustomerWalletTransactions
IF NOT EXISTS (SELECT 1 FROM sys.tables 
  WHERE name = 'CustomerWalletTransactions')
BEGIN
  CREATE TABLE CustomerWalletTransactions (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    UserId        NVARCHAR(450) NOT NULL,
    Type          NVARCHAR(20)  NOT NULL,
    Amount        DECIMAL(18,2) NOT NULL DEFAULT 0,
    BalanceBefore DECIMAL(18,2) NOT NULL DEFAULT 0,
    BalanceAfter  DECIMAL(18,2) NOT NULL DEFAULT 0,
    Note          NVARCHAR(500) NULL,
    OrderId       INT NULL,
    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CustWalletTx_Users  
      FOREIGN KEY (UserId) REFERENCES Users(Id) 
      ON DELETE CASCADE,
    CONSTRAINT FK_CustWalletTx_Orders 
      FOREIGN KEY (OrderId) REFERENCES Orders(Id) 
      ON DELETE NO ACTION
  );
  CREATE INDEX IX_CustWalletTx_UserId 
    ON CustomerWalletTransactions(UserId);
  PRINT 'CustomerWalletTransactions created';
END
ELSE PRINT 'CustomerWalletTransactions already exists';
GO

-- 10. Seed Region Shops
UPDATE Shops SET Province='Ho Chi Minh', Region='SOUTH'
WHERE Id='cd9cdd36-30eb-42bd-866c-0cae6ff0fe3e';

UPDATE Shops SET Region='SOUTH' 
WHERE Region IS NULL AND (
  Province LIKE '%Hồ Chí Minh%' OR Province LIKE '%Ho Chi Minh%'
  OR Province LIKE '%Bình Dương%' OR Province LIKE '%Đồng Nai%'
  OR Province LIKE '%Cần Thơ%' OR Province LIKE '%Long An%'
  OR Province LIKE '%Vũng Tàu%' OR Province LIKE '%Tiền Giang%'
);
UPDATE Shops SET Region='NORTH' 
WHERE Region IS NULL AND (
  Province LIKE '%Hà Nội%' OR Province LIKE '%Ha Noi%'
  OR Province LIKE '%Hải Phòng%' OR Province LIKE '%Quảng Ninh%'
  OR Province LIKE '%Bắc Ninh%' OR Province LIKE '%Hải Dương%'
  OR Province LIKE '%Thái Nguyên%' OR Province LIKE '%Lào Cai%'
);
UPDATE Shops SET Region='CENTRAL' 
WHERE Region IS NULL AND (
  Province LIKE '%Đà Nẵng%' OR Province LIKE '%Huế%'
  OR Province LIKE '%Khánh Hòa%' OR Province LIKE '%Nghệ An%'
  OR Province LIKE '%Quảng Nam%' OR Province LIKE '%Đắk Lắk%'
  OR Province LIKE '%Lâm Đồng%' OR Province LIKE '%Bình Định%'
);
UPDATE Shops SET Region='ISLAND' 
WHERE Region IS NULL AND (
  Province LIKE '%Phú Quốc%' OR Province LIKE '%Côn Đảo%'
);
UPDATE Shops SET Region='OTHER' WHERE Region IS NULL;
PRINT 'Shops Region seeded';
GO

-- 11. Seed Region UserAddresses
UPDATE UserAddresses SET Region='SOUTH' 
WHERE Region IS NULL AND Province IS NOT NULL AND (
  Province LIKE '%Hồ Chí Minh%' OR Province LIKE '%Ho Chi Minh%'
  OR Province LIKE '%Bình Dương%' OR Province LIKE '%Đồng Nai%'
  OR Province LIKE '%Cần Thơ%' OR Province LIKE '%Long An%'
  OR Province LIKE '%Vũng Tàu%' OR Province LIKE '%Tiền Giang%'
);
UPDATE UserAddresses SET Region='NORTH' 
WHERE Region IS NULL AND Province IS NOT NULL AND (
  Province LIKE '%Hà Nội%' OR Province LIKE '%Ha Noi%'
  OR Province LIKE '%Hải Phòng%' OR Province LIKE '%Quảng Ninh%'
  OR Province LIKE '%Bắc Ninh%' OR Province LIKE '%Hải Dương%'
  OR Province LIKE '%Thái Nguyên%' OR Province LIKE '%Lào Cai%'
);
UPDATE UserAddresses SET Region='CENTRAL' 
WHERE Region IS NULL AND Province IS NOT NULL AND (
  Province LIKE '%Đà Nẵng%' OR Province LIKE '%Huế%'
  OR Province LIKE '%Khánh Hòa%' OR Province LIKE '%Nghệ An%'
  OR Province LIKE '%Quảng Nam%' OR Province LIKE '%Đắk Lắk%'
  OR Province LIKE '%Lâm Đồng%' OR Province LIKE '%Bình Định%'
);
UPDATE UserAddresses SET Region='ISLAND' 
WHERE Region IS NULL AND Province IS NOT NULL AND (
  Province LIKE '%Phú Quốc%' OR Province LIKE '%Côn Đảo%'
);
UPDATE UserAddresses SET Region='OTHER' 
WHERE Region IS NULL AND Province IS NOT NULL;
PRINT 'UserAddresses Region seeded';
GO

-- 12. Verify
SELECT 'SubOrders' as TableName, COUNT(*) as Rows 
FROM SubOrders
UNION ALL
SELECT 'SubOrderItems', COUNT(*) FROM SubOrderItems
UNION ALL
SELECT 'CustomerWallets', COUNT(*) FROM CustomerWallets
UNION ALL
SELECT 'CustomerWalletTransactions', COUNT(*) 
FROM CustomerWalletTransactions
UNION ALL
SELECT 'Shops with Region', COUNT(*) 
FROM Shops WHERE Region IS NOT NULL
UNION ALL
SELECT 'FlashSaleProducts backfilled', COUNT(*) 
FROM FlashSaleProducts WHERE RemainingQuantity > 0;

PRINT '=== Sprint 11 migration complete ===';
GO