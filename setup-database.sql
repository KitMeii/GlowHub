-- ============================================================================
-- GlowHub — Consolidated database setup script (no seed data)
-- Target  : Microsoft SQL Server
-- DB Name : BaseCoreDB
-- Usage   : Run in SSMS / sqlcmd as a user with sysadmin/dbcreator rights.
--           DROPS the existing BaseCoreDB if present, then recreates it.
-- ============================================================================

USE [master];
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'BaseCoreDB')
BEGIN
    ALTER DATABASE [BaseCoreDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [BaseCoreDB];
END
GO

CREATE DATABASE [BaseCoreDB];
GO

USE [BaseCoreDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================================
-- EF Core migration history (so EF skips already-applied migrations)
-- ============================================================================
CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId]    NVARCHAR(150) NOT NULL,
    [ProductVersion] NVARCHAR(32)  NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId] ASC)
);
GO

-- ============================================================================
-- 1. Users
-- ============================================================================
CREATE TABLE [dbo].[Users] (
    [Id]            NVARCHAR(450)    NOT NULL,
    [Name]          NVARCHAR(100)    NOT NULL,
    [UserName]      NVARCHAR(50)     NOT NULL,
    [Password]      NVARCHAR(255)    NOT NULL,
    [Salt]          VARBINARY(MAX)   NULL,
    [Contact]       NVARCHAR(MAX)    NOT NULL DEFAULT (''),
    [Email]         NVARCHAR(100)    NOT NULL DEFAULT (''),
    [Phone]         NVARCHAR(20)     NOT NULL DEFAULT (''),
    [Position]      NVARCHAR(MAX)    NOT NULL DEFAULT (''),
    [Image]         NVARCHAR(MAX)    NOT NULL DEFAULT (''),
    [IsActive]      BIT              NOT NULL DEFAULT 1,
    [UserType]      INT              NOT NULL DEFAULT 0,    -- 0=Customer, 1=Admin, 2=Seller
    [Created]       DATETIME2(7)     NOT NULL DEFAULT GETDATE(),
    [Address]       NVARCHAR(500)    NULL,
    [AvatarUrl]     NVARCHAR(500)    NULL,
    [LastLoginAt]   DATETIME2(7)     NULL,
    [OAuthProvider] NVARCHAR(20)     NULL,
    [OAuthId]       NVARCHAR(200)    NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_Users_UserName] ON [dbo].[Users]([UserName] ASC);
CREATE NONCLUSTERED INDEX [IX_Users_OAuth] ON [dbo].[Users]([OAuthProvider], [OAuthId]);
GO

-- ============================================================================
-- 2. Categories
-- ============================================================================
CREATE TABLE [dbo].[Categories] (
    [Id]          INT             IDENTITY(1,1) NOT NULL,
    [Name]        NVARCHAR(100)   NOT NULL,
    [Description] NVARCHAR(500)   NULL,
    [IsDeleted]   BIT             NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Categories] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

-- ============================================================================
-- 3. Shops  (depends on Users)
-- ============================================================================
CREATE TABLE [dbo].[Shops] (
    [Id]             NVARCHAR(450)  NOT NULL,
    [SellerId]       NVARCHAR(450)  NOT NULL,
    [ShopName]       NVARCHAR(100)  NOT NULL,
    [Description]    NVARCHAR(500)  NOT NULL DEFAULT (''),
    [Logo]           NVARCHAR(500)  NOT NULL DEFAULT (''),
    [Address]        NVARCHAR(300)  NOT NULL DEFAULT (''),
    [Phone]          NVARCHAR(20)   NOT NULL DEFAULT (''),
    [Status]         INT            NOT NULL DEFAULT 0,
    [CommissionRate] DECIMAL(5,2)   NOT NULL DEFAULT 10.00,
    [Province]       NVARCHAR(100)  NULL,
    [Region]         NVARCHAR(20)   NULL,
    [CreatedAt]      DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]      DATETIME2(7)   NULL,
    CONSTRAINT [PK_Shops]            PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Shops_Users_SellerId]
        FOREIGN KEY ([SellerId]) REFERENCES [dbo].[Users]([Id])
);
GO

-- ============================================================================
-- 4. Products  (depends on Categories, Shops)
-- ============================================================================
CREATE TABLE [dbo].[Products] (
    [Id]             INT              IDENTITY(1,1) NOT NULL,
    [Name]           NVARCHAR(200)    NOT NULL,
    [Price]          DECIMAL(18,2)    NOT NULL,
    [Stock]          INT              NOT NULL DEFAULT 0,
    [ImageUrl]       NVARCHAR(500)    NOT NULL DEFAULT (''),
    [Description]    NVARCHAR(1000)   NOT NULL DEFAULT (''),
    [CategoryId]     INT              NOT NULL,
    [IsActive]       BIT              NOT NULL DEFAULT 1,
    [DiscountPrice]  DECIMAL(18,2)    NULL,
    [IsNew]          BIT              NOT NULL DEFAULT 0,
    [CreatedAt]      DATETIME2(7)     NOT NULL DEFAULT GETDATE(),
    [SortOrder]      INT              NOT NULL DEFAULT 0,
    [ShopId]         NVARCHAR(450)    NULL,
    [Images]         NVARCHAR(2000)   NULL,
    [Specifications] NVARCHAR(2000)   NULL,
    [SoldCount]      INT              NOT NULL DEFAULT 0,
    [ViewCount]      INT              NOT NULL DEFAULT 0,
    [WeightGram]     INT              NOT NULL DEFAULT 500,
    [RowVersion]     ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Products_Categories_CategoryId]
        FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Categories]([Id]),
    CONSTRAINT [FK_Products_Shops_ShopId]
        FOREIGN KEY ([ShopId])     REFERENCES [dbo].[Shops]([Id]) ON DELETE SET NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_Products_CategoryId] ON [dbo].[Products]([CategoryId] ASC);
CREATE NONCLUSTERED INDEX [IX_Products_ShopId]     ON [dbo].[Products]([ShopId] ASC);
GO

-- ============================================================================
-- 5. ShopProducts  (junction Shops × Products)
-- ============================================================================
CREATE TABLE [dbo].[ShopProducts] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [ShopId]    NVARCHAR(450)  NOT NULL,
    [ProductId] INT            NOT NULL,
    [AddedAt]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ShopProducts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ShopProducts_Shops_ShopId]
        FOREIGN KEY ([ShopId])    REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShopProducts_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_ShopProducts_ShopId_ProductId]
    ON [dbo].[ShopProducts]([ShopId] ASC, [ProductId] ASC);
GO

-- ============================================================================
-- 6. Orders
-- ============================================================================
CREATE TABLE [dbo].[Orders] (
    [Id]                       INT             IDENTITY(1,1) NOT NULL,
    [UserId]                   NVARCHAR(450)   NOT NULL,
    [OrderDate]                DATETIME2(7)    NOT NULL DEFAULT GETDATE(),
    [UpdatedAt]                DATETIME2(7)    NULL,
    [TotalAmount]              DECIMAL(18,2)   NOT NULL,
    [Status]                   NVARCHAR(20)    NOT NULL DEFAULT 'PENDING',
    [ShippingAddress]          NVARCHAR(500)   NOT NULL,
    [Note]                     NVARCHAR(500)   NULL,
    [ReceiverName]             NVARCHAR(100)   NOT NULL DEFAULT (''),
    [ReceiverPhone]            NVARCHAR(20)    NOT NULL DEFAULT (''),
    [PaymentMethod]            NVARCHAR(20)    NULL,
    [PaymentStatus]            NVARCHAR(20)    NULL,
    [OrderCode]                NVARCHAR(20)    NULL,
    [ShippingFee]              DECIMAL(18,2)   NULL DEFAULT 30000,
    [FinalAmount]              DECIMAL(18,2)   NULL,
    [EstimatedDelivery]        DATETIME2(7)    NULL,
    [CancelReason]             NVARCHAR(500)   NULL,
    [TrackingCode]             NVARCHAR(100)   NULL,
    [ShipVoucherCode]          NVARCHAR(50)    NULL,
    [Discount]                 DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [ShopId]                   NVARCHAR(450)   NULL,
    [CommissionRate]           DECIMAL(5,2)    NOT NULL DEFAULT 0,
    [ProductRevenue]           DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [CommissionAmount]         DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [SellerPayoutAmount]       DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [ShopVoucherDiscount]      DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [SystemVoucherDiscount]    DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [FreeshipDiscount]         DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [PayoutStatus]             NVARCHAR(20)    NOT NULL DEFAULT 'PENDING',
    [WalletReleaseAt]          DATETIME2(7)    NULL,
    [ToProvince]               NVARCHAR(100)   NULL,
    [VNPayTransactionId]       NVARCHAR(100)   NULL,
    [PaymentExpireAt]          DATETIME2(7)    NULL,
    [BankTransferConfirmedAt]  DATETIME2(7)    NULL,
    [BankTransferConfirmedBy]  NVARCHAR(450)   NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Orders_Users_UserId]
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [FK_Orders_Shops_ShopId]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE SET NULL
);
GO

CREATE NONCLUSTERED INDEX [IX_Orders_UserId]            ON [dbo].[Orders]([UserId] ASC);
CREATE NONCLUSTERED INDEX [IX_Orders_Status]            ON [dbo].[Orders]([Status] ASC);
CREATE NONCLUSTERED INDEX [IX_Orders_ShopId]            ON [dbo].[Orders]([ShopId] ASC);
CREATE NONCLUSTERED INDEX [IX_Orders_PayoutStatus]      ON [dbo].[Orders]([PayoutStatus] ASC);
CREATE NONCLUSTERED INDEX [IX_Orders_PaymentExpireAt]   ON [dbo].[Orders]([PaymentExpireAt]) WHERE [PaymentExpireAt] IS NOT NULL;
GO

-- ============================================================================
-- 7. OrderDetails
-- ============================================================================
CREATE TABLE [dbo].[OrderDetails] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [OrderId]   INT             NOT NULL,
    [ProductId] INT             NOT NULL,
    [Quantity]  INT             NOT NULL,
    [UnitPrice] DECIMAL(18,2)   NOT NULL,
    CONSTRAINT [PK_OrderDetails] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrderDetails_Orders_OrderId]
        FOREIGN KEY ([OrderId])   REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderDetails_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrderDetails_OrderId]   ON [dbo].[OrderDetails]([OrderId] ASC);
CREATE NONCLUSTERED INDEX [IX_OrderDetails_ProductId] ON [dbo].[OrderDetails]([ProductId] ASC);
GO

-- ============================================================================
-- 8. SubOrders + SubOrderItems  (per-shop split of an Order)
-- ============================================================================
CREATE TABLE [dbo].[SubOrders] (
    [Id]                  INT             IDENTITY(1,1) NOT NULL,
    [OrderId]             INT             NOT NULL,
    [ShopId]              NVARCHAR(450)   NOT NULL,
    [SubOrderCode]        NVARCHAR(20)    NULL,
    [Status]              NVARCHAR(20)    NOT NULL DEFAULT 'PENDING',
    [TotalAmount]         DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [ShippingFee]         DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [FinalAmount]         DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [ProductRevenue]      DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [CommissionRate]      DECIMAL(5,2)    NOT NULL DEFAULT 0,
    [CommissionAmount]    DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [SellerPayoutAmount]  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [ShopVoucherDiscount] DECIMAL(18,2)   NOT NULL DEFAULT 0,
    [PayoutStatus]        NVARCHAR(20)    NOT NULL DEFAULT 'PENDING',
    [WalletReleaseAt]     DATETIME2(7)    NULL,
    [TrackingCode]        NVARCHAR(100)   NULL,
    [CancelReason]        NVARCHAR(500)   NULL,
    [Note]                NVARCHAR(500)   NULL,
    [ToProvince]          NVARCHAR(100)   NULL,
    [CreatedAt]           DATETIME2(7)    NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]           DATETIME2(7)    NULL,
    CONSTRAINT [PK_SubOrders] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SubOrders_Orders] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubOrders_Shops]  FOREIGN KEY ([ShopId])  REFERENCES [dbo].[Shops]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_SubOrders_OrderId] ON [dbo].[SubOrders]([OrderId] ASC);
CREATE NONCLUSTERED INDEX [IX_SubOrders_ShopId]  ON [dbo].[SubOrders]([ShopId] ASC);
GO

CREATE TABLE [dbo].[SubOrderItems] (
    [Id]         INT             IDENTITY(1,1) NOT NULL,
    [SubOrderId] INT             NOT NULL,
    [ProductId]  INT             NOT NULL,
    [Quantity]   INT             NOT NULL DEFAULT 1,
    [UnitPrice]  DECIMAL(18,2)   NOT NULL DEFAULT 0,
    CONSTRAINT [PK_SubOrderItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_SubOrderItems_SubOrders]
        FOREIGN KEY ([SubOrderId]) REFERENCES [dbo].[SubOrders]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_SubOrderItems_Products]
        FOREIGN KEY ([ProductId])  REFERENCES [dbo].[Products]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_SubOrderItems_SubOrderId] ON [dbo].[SubOrderItems]([SubOrderId] ASC);
GO

-- ============================================================================
-- 9. CartItems
-- ============================================================================
CREATE TABLE [dbo].[CartItems] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [UserId]    NVARCHAR(450)   NOT NULL,
    [ProductId] INT             NOT NULL,
    [Quantity]  INT             NOT NULL DEFAULT 1,
    [AddedAt]   DATETIME2(7)    NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_CartItems] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CartItems_Users_UserId]
        FOREIGN KEY ([UserId])    REFERENCES [dbo].[Users]([Id]),
    CONSTRAINT [FK_CartItems_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id])
);
GO

CREATE NONCLUSTERED INDEX        [IX_CartItems_UserId]            ON [dbo].[CartItems]([UserId] ASC);
CREATE UNIQUE NONCLUSTERED INDEX [UX_CartItems_User_Product]     ON [dbo].[CartItems]([UserId] ASC, [ProductId] ASC);
GO

-- ============================================================================
-- 10. Reviews
-- ============================================================================
CREATE TABLE [dbo].[Reviews] (
    [Id]                  INT             IDENTITY(1,1) NOT NULL,
    [ProductId]           INT             NOT NULL,
    [UserId]              NVARCHAR(450)   NOT NULL,
    [Rating]              INT             NOT NULL,
    [Comment]             NVARCHAR(1000)  NULL,
    [SellerReply]         NVARCHAR(500)   NULL,
    [ReplyAt]             DATETIME2(7)    NULL,
    [Images]              NVARCHAR(1000)  NULL,
    [IsVerifiedPurchase]  BIT             NOT NULL DEFAULT 0,
    [CreatedAt]           DATETIME2(7)    NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_Reviews]      PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
    CONSTRAINT [FK_Reviews_Products_ProductId]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Reviews_Users_UserId]
        FOREIGN KEY ([UserId])    REFERENCES [dbo].[Users]([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Reviews_User_Product] ON [dbo].[Reviews]([UserId] ASC, [ProductId] ASC);
CREATE NONCLUSTERED INDEX        [IX_Reviews_ProductId]    ON [dbo].[Reviews]([ProductId] ASC);
GO

-- ============================================================================
-- 11. OrderStatusLogs  (admin status-change audit per Order)
-- ============================================================================
CREATE TABLE [dbo].[OrderStatusLogs] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [OrderId]   INT             NOT NULL,
    [OldStatus] NVARCHAR(20)    NULL,
    [NewStatus] NVARCHAR(20)    NOT NULL,
    [ChangedBy] NVARCHAR(450)   NOT NULL,
    [Note]      NVARCHAR(500)   NULL,
    [CreatedAt] DATETIME2(7)    NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_OrderStatusLogs] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrderStatusLogs_Orders_OrderId]
        FOREIGN KEY ([OrderId])   REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderStatusLogs_Users_ChangedBy]
        FOREIGN KEY ([ChangedBy]) REFERENCES [dbo].[Users]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrderStatusLogs_OrderId] ON [dbo].[OrderStatusLogs]([OrderId] ASC);
GO

-- ============================================================================
-- 12. OrderStatusHistories  (customer-facing status timeline)
-- ============================================================================
CREATE TABLE [dbo].[OrderStatusHistories] (
    [Id]        INT             IDENTITY(1,1) NOT NULL,
    [OrderId]   INT             NOT NULL,
    [Status]    NVARCHAR(20)    NOT NULL,
    [Note]      NVARCHAR(500)   NULL,
    [ChangedBy] NVARCHAR(450)   NULL,
    [ChangedAt] DATETIME2(7)    NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrderStatusHistories_Orders]
        FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id]) ON DELETE CASCADE
);
GO

-- ============================================================================
-- 13. UserAddresses
-- ============================================================================
CREATE TABLE [dbo].[UserAddresses] (
    [Id]           INT            IDENTITY(1,1) NOT NULL,
    [UserId]       NVARCHAR(450)  NOT NULL,
    [ReceiverName] NVARCHAR(100)  NOT NULL,
    [Phone]        NVARCHAR(20)   NOT NULL,
    [Province]     NVARCHAR(100)  NOT NULL,
    [ProvinceCode] NVARCHAR(20)   NOT NULL DEFAULT (''),
    [District]     NVARCHAR(100)  NOT NULL,
    [DistrictCode] NVARCHAR(20)   NOT NULL DEFAULT (''),
    [Ward]         NVARCHAR(100)  NOT NULL DEFAULT (''),
    [WardCode]     NVARCHAR(20)   NOT NULL DEFAULT (''),
    [Detail]       NVARCHAR(300)  NOT NULL,
    [Latitude]     FLOAT          NULL,
    [Longitude]    FLOAT          NULL,
    [MapAddress]   NVARCHAR(500)  NULL,
    [Region]       NVARCHAR(20)   NULL,
    [IsDefault]    BIT            NOT NULL DEFAULT 0,
    [CreatedAt]    DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]    DATETIME2(7)   NULL,
    CONSTRAINT [PK_UserAddresses] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserAddresses_Users_UserId]
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_UserAddresses_UserId] ON [dbo].[UserAddresses]([UserId] ASC);
GO

-- ============================================================================
-- 14. ShopFollows
-- ============================================================================
CREATE TABLE [dbo].[ShopFollows] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [UserId]    NVARCHAR(450)  NOT NULL,
    [ShopId]    NVARCHAR(450)  NOT NULL,
    [CreatedAt] DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_ShopFollows]       PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ShopFollows_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ShopFollows_Shops] FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_ShopFollows_UserShop] ON [dbo].[ShopFollows]([UserId], [ShopId]);
GO

-- ============================================================================
-- 15. QnA
-- ============================================================================
CREATE TABLE [dbo].[QnA] (
    [Id]         INT            IDENTITY(1,1) NOT NULL,
    [ProductId]  INT            NOT NULL,
    [CustomerId] NVARCHAR(450)  NOT NULL,
    [Question]   NVARCHAR(1000) NOT NULL,
    [AskedAt]    DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [Answer]     NVARCHAR(2000) NULL,
    [AnsweredAt] DATETIME2(7)   NULL,
    [IsActive]   BIT            NOT NULL DEFAULT 1,
    CONSTRAINT [PK_QnA]          PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_QnA_Products] FOREIGN KEY ([ProductId])  REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_QnA_Users]    FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Users]([Id])
);
GO

-- ============================================================================
-- 16. Notifications
-- ============================================================================
CREATE TABLE [dbo].[Notifications] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [UserId]    NVARCHAR(450)  NOT NULL,
    [Type]      INT            NOT NULL DEFAULT 0,
    [Title]     NVARCHAR(200)  NOT NULL,
    [Message]   NVARCHAR(500)  NOT NULL,
    [IsRead]    BIT            NOT NULL DEFAULT 0,
    [Link]      NVARCHAR(500)  NULL,
    [CreatedAt] DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Notifications]       PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Notifications_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_Notifications_UserId_IsRead] ON [dbo].[Notifications]([UserId], [IsRead]);
GO

-- ============================================================================
-- 17. Wishlists  (DbContext maps Wishlist → "Wishlists")
-- ============================================================================
CREATE TABLE [dbo].[Wishlists] (
    [Id]         INT            IDENTITY(1,1) NOT NULL,
    [CustomerId] NVARCHAR(450)  NOT NULL,
    [ProductId]  INT            NOT NULL,
    [CreatedAt]  DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Wishlists]            PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Wishlists_Users]      FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Users]([Id])    ON DELETE CASCADE,
    CONSTRAINT [FK_Wishlists_Products]   FOREIGN KEY ([ProductId])  REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE,
    CONSTRAINT [UQ_Wishlists_Customer_Product] UNIQUE ([CustomerId], [ProductId])
);
GO

-- ============================================================================
-- 18. Banners
-- ============================================================================
CREATE TABLE [dbo].[Banners] (
    [Id]         INT            IDENTITY(1,1) NOT NULL,
    [Title]      NVARCHAR(200)  NOT NULL,
    [Subtitle]   NVARCHAR(500)  NULL,
    [ImageUrl]   NVARCHAR(500)  NOT NULL DEFAULT (''),
    [LinkUrl]    NVARCHAR(500)  NULL,
    [ButtonText] NVARCHAR(50)   NULL,
    [BgColor]    NVARCHAR(20)   NULL,
    [SortOrder]  INT            NOT NULL DEFAULT 0,
    [IsActive]   BIT            NOT NULL DEFAULT 1,
    [CreatedAt]  DATETIME2(7)   NOT NULL DEFAULT GETDATE(),
    [UpdatedAt]  DATETIME2(7)   NULL,
    [CreatedBy]  NVARCHAR(450)  NULL,
    CONSTRAINT [PK_Banners] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_Banners_IsActive_SortOrder] ON [dbo].[Banners]([IsActive] ASC, [SortOrder] ASC);
GO

-- ============================================================================
-- 19. FeaturedProducts
-- ============================================================================
CREATE TABLE [dbo].[FeaturedProducts] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [ProductId] INT            NOT NULL,
    [Section]   NVARCHAR(50)   NOT NULL,        -- 'new_arrivals' | 'best_sellers' | 'hero_slider'
    [SortOrder] INT            NOT NULL DEFAULT 0,
    [IsActive]  BIT            NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2(7)   NOT NULL DEFAULT GETDATE(),
    [CreatedBy] NVARCHAR(450)  NULL,
    CONSTRAINT [PK_FeaturedProducts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FeaturedProducts_Products]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_FeaturedProducts_Product_Section]
    ON [dbo].[FeaturedProducts]([ProductId] ASC, [Section] ASC);
CREATE NONCLUSTERED INDEX [IX_FeaturedProducts_Section_Sort]
    ON [dbo].[FeaturedProducts]([Section] ASC, [SortOrder] ASC);
GO

-- ============================================================================
-- 20. SiteSettings  (Key is PK)
-- ============================================================================
CREATE TABLE [dbo].[SiteSettings] (
    [Key]       NVARCHAR(100)  NOT NULL,
    [Value]     NVARCHAR(MAX)  NULL,
    [Type]      NVARCHAR(20)   NOT NULL DEFAULT 'text',
    [Group]     NVARCHAR(50)   NOT NULL DEFAULT 'general',
    [Label]     NVARCHAR(100)  NULL,
    [UpdatedAt] DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedBy] NVARCHAR(450)  NULL,
    CONSTRAINT [PK_SiteSettings] PRIMARY KEY CLUSTERED ([Key] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_SiteSettings_Group] ON [dbo].[SiteSettings]([Group] ASC);
GO

-- ============================================================================
-- 21. Vouchers  (Code unique; optional FK to Shops)
-- ============================================================================
CREATE TABLE [dbo].[Vouchers] (
    [Id]              INT            IDENTITY(1,1) NOT NULL,
    [Code]            NVARCHAR(50)   NOT NULL,
    [Description]     NVARCHAR(200)  NULL,
    [DiscountType]    NVARCHAR(20)   NOT NULL DEFAULT 'percent',
    [DiscountValue]   DECIMAL(18,2)  NOT NULL,
    [MinOrderAmount]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [MaxDiscount]     DECIMAL(18,2)  NULL,
    [UsageLimit]      INT            NULL,
    [UsedCount]       INT            NOT NULL DEFAULT 0,
    [StartDate]       DATETIME2(7)   NULL,
    [ExpiryDate]      DATETIME2(7)   NULL,
    [IsActive]        BIT            NOT NULL DEFAULT 1,
    [CreatedAt]       DATETIME2(7)   NOT NULL DEFAULT GETDATE(),
    [ShopId]          NVARCHAR(450)  NULL,
    CONSTRAINT [PK_Vouchers] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Vouchers_Shops_ShopId]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE SET NULL
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_Vouchers_Code] ON [dbo].[Vouchers]([Code] ASC);
GO

-- ============================================================================
-- 22. CustomerVouchers  (user-saved vouchers)
-- ============================================================================
CREATE TABLE [dbo].[CustomerVouchers] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [UserId]    NVARCHAR(450)  NOT NULL,
    [VoucherId] INT            NOT NULL,
    [SavedAt]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [IsUsed]    BIT            NOT NULL DEFAULT 0,
    CONSTRAINT [PK_CustomerVouchers]          PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CustomerVouchers_Users]    FOREIGN KEY ([UserId])    REFERENCES [dbo].[Users]([Id])    ON DELETE CASCADE,
    CONSTRAINT [FK_CustomerVouchers_Vouchers] FOREIGN KEY ([VoucherId]) REFERENCES [dbo].[Vouchers]([Id]) ON DELETE CASCADE
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_CustomerVouchers_UserId_VoucherId]
    ON [dbo].[CustomerVouchers]([UserId], [VoucherId]);
GO

-- ============================================================================
-- 23. MediaFiles  (uploaded media library)
-- ============================================================================
CREATE TABLE [dbo].[MediaFiles] (
    [Id]           INT            IDENTITY(1,1) NOT NULL,
    [FileName]     NVARCHAR(255)  NOT NULL,
    [OriginalName] NVARCHAR(255)  NOT NULL,
    [Url]          NVARCHAR(500)  NOT NULL,
    [MimeType]     NVARCHAR(100)  NULL,
    [FileSize]     BIGINT         NULL,
    [Width]        INT            NULL,
    [Height]       INT            NULL,
    [Alt]          NVARCHAR(200)  NULL,
    [Folder]       NVARCHAR(100)  NOT NULL DEFAULT 'uploads',
    [UploadedBy]   NVARCHAR(450)  NULL,
    [CreatedAt]    DATETIME2(7)   NOT NULL DEFAULT GETDATE(),
    CONSTRAINT [PK_MediaFiles] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_MediaFiles_Folder_CreatedAt] ON [dbo].[MediaFiles]([Folder] ASC, [CreatedAt] DESC);
GO

-- ============================================================================
-- 24. AuditLogs
--     Note: EF entity property `EntityType` maps to column [Entity]
-- ============================================================================
CREATE TABLE [dbo].[AuditLogs] (
    [Id]          INT            IDENTITY(1,1) NOT NULL,
    [UserId]      NVARCHAR(450)  NULL,
    [UserName]    NVARCHAR(256)  NULL,
    [Action]      NVARCHAR(100)  NOT NULL,
    [Entity]      NVARCHAR(100)  NULL,           -- mapped from AuditLog.EntityType
    [EntityId]    NVARCHAR(450)  NULL,
    [OldValue]    NVARCHAR(MAX)  NULL,
    [NewValue]    NVARCHAR(MAX)  NULL,
    [IpAddress]   NVARCHAR(50)   NULL,
    [CreatedAt]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [IX_AuditLogs_Entity_EntityId]   ON [dbo].[AuditLogs]([Entity] ASC, [EntityId] ASC);
CREATE NONCLUSTERED INDEX [IX_AuditLogs_UserId_CreatedAt]  ON [dbo].[AuditLogs]([UserId] ASC, [CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_AuditLogs_CreatedAt]         ON [dbo].[AuditLogs]([CreatedAt] DESC);
GO

-- ============================================================================
-- 25. FlashSales + FlashSaleProducts
-- ============================================================================
CREATE TABLE [dbo].[FlashSales] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [Name]      NVARCHAR(200)  NOT NULL,
    [StartTime] DATETIME2(7)   NOT NULL,
    [EndTime]   DATETIME2(7)   NOT NULL,
    [IsActive]  BIT            NOT NULL DEFAULT 1,
    [CreatedAt] DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] NVARCHAR(450)  NULL,
    CONSTRAINT [PK_FlashSales] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE TABLE [dbo].[FlashSaleProducts] (
    [Id]                INT            IDENTITY(1,1) NOT NULL,
    [FlashSaleId]       INT            NOT NULL,
    [ProductId]         INT            NOT NULL,
    [SalePrice]         DECIMAL(18,2)  NOT NULL,
    [OriginalPrice]     DECIMAL(18,2)  NOT NULL,
    [Quantity]          INT            NOT NULL DEFAULT 0,
    [SoldCount]         INT            NOT NULL DEFAULT 0,
    [RemainingQuantity] INT            NOT NULL DEFAULT 0,
    [IsActive]          BIT            NOT NULL DEFAULT 1,
    CONSTRAINT [PK_FlashSaleProducts] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_FlashSaleProducts_FlashSales]
        FOREIGN KEY ([FlashSaleId]) REFERENCES [dbo].[FlashSales]([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_FlashSaleProducts_Products]
        FOREIGN KEY ([ProductId])   REFERENCES [dbo].[Products]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_FlashSaleProducts_FlashSaleId] ON [dbo].[FlashSaleProducts]([FlashSaleId]);
CREATE NONCLUSTERED INDEX [IX_FlashSaleProducts_ProductId]   ON [dbo].[FlashSaleProducts]([ProductId]);
GO

-- ============================================================================
-- 26. RecentlyVieweds  (DbContext maps RecentlyViewed → "RecentlyVieweds")
-- ============================================================================
CREATE TABLE [dbo].[RecentlyVieweds] (
    [Id]        INT            IDENTITY(1,1) NOT NULL,
    [UserId]    NVARCHAR(450)  NOT NULL,
    [ProductId] INT            NOT NULL,
    [ViewedAt]  DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_RecentlyVieweds] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_RecentlyVieweds_Users]
        FOREIGN KEY ([UserId])    REFERENCES [dbo].[Users]([Id])    ON DELETE CASCADE,
    CONSTRAINT [FK_RecentlyVieweds_Products]
        FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products]([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_RecentlyVieweds_UserId_ViewedAt]
    ON [dbo].[RecentlyVieweds]([UserId], [ViewedAt] DESC);
GO

-- ============================================================================
-- 27. PayoutHistories
-- ============================================================================
CREATE TABLE [dbo].[PayoutHistories] (
    [Id]          INT            IDENTITY(1,1) NOT NULL,
    [ShopId]      NVARCHAR(450)  NOT NULL,
    [Amount]      DECIMAL(18,2)  NOT NULL,
    [Note]        NVARCHAR(500)  NULL,
    [PayoutDate]  DATETIME2(7)   NOT NULL,
    [ProcessedBy] NVARCHAR(450)  NULL,
    [CreatedAt]   DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_PayoutHistories] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PayoutHistories_Shops]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE
);
GO

-- ============================================================================
-- 28. Disputes
-- ============================================================================
CREATE TABLE [dbo].[Disputes] (
    [Id]            INT            IDENTITY(1,1) NOT NULL,
    [OrderId]       INT            NOT NULL,
    [CustomerId]    NVARCHAR(450)  NOT NULL,
    [Reason]        NVARCHAR(200)  NOT NULL,
    [Description]   NVARCHAR(MAX)  NULL,
    [Evidence]      NVARCHAR(MAX)  NULL,
    [Status]        NVARCHAR(20)   NOT NULL DEFAULT 'OPEN',
    [Resolution]    NVARCHAR(MAX)  NULL,
    [RefundAmount]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [FavorCustomer] BIT            NOT NULL DEFAULT 0,
    [CreatedAt]     DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    [ResolvedAt]    DATETIME2(7)   NULL,
    [ResolvedBy]    NVARCHAR(450)  NULL,
    CONSTRAINT [PK_Disputes] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Disputes_Orders]
        FOREIGN KEY ([OrderId])    REFERENCES [dbo].[Orders]([Id]),
    CONSTRAINT [FK_Disputes_Users]
        FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Users]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_Disputes_Status] ON [dbo].[Disputes]([Status]);
GO

-- ============================================================================
-- 29. SellerWallets + WalletTransactions
-- ============================================================================
CREATE TABLE [dbo].[SellerWallets] (
    [ShopId]         NVARCHAR(450)  NOT NULL,
    [Balance]        DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [TotalEarned]    DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [TotalWithdrawn] DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [TotalRefunded]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [UpdatedAt]      DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_SellerWallets] PRIMARY KEY CLUSTERED ([ShopId] ASC),
    CONSTRAINT [FK_SellerWallets_Shops]
        FOREIGN KEY ([ShopId]) REFERENCES [dbo].[Shops]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[WalletTransactions] (
    [Id]            INT            IDENTITY(1,1) NOT NULL,
    [ShopId]        NVARCHAR(450)  NOT NULL,
    [OrderId]       INT            NULL,
    [Type]          NVARCHAR(20)   NOT NULL,    -- EARNING | REFUND | WITHDRAWAL | ADJUSTMENT
    [Amount]        DECIMAL(18,2)  NOT NULL,
    [BalanceBefore] DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [BalanceAfter]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [Note]          NVARCHAR(500)  NULL,
    [CreatedAt]     DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_WalletTransactions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WalletTx_Shops]
        FOREIGN KEY ([ShopId])  REFERENCES [dbo].[Shops]([Id])  ON DELETE CASCADE,
    CONSTRAINT [FK_WalletTx_Orders]
        FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_WalletTx_ShopId]  ON [dbo].[WalletTransactions]([ShopId], [CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_WalletTx_OrderId] ON [dbo].[WalletTransactions]([OrderId]);
GO

-- ============================================================================
-- 30. CustomerWallets + CustomerWalletTransactions
-- ============================================================================
CREATE TABLE [dbo].[CustomerWallets] (
    [UserId]        NVARCHAR(450)  NOT NULL,
    [Balance]       DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [TotalReceived] DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [TotalSpent]    DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [UpdatedAt]     DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_CustomerWallets] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_CustomerWallets_Users]
        FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [dbo].[CustomerWalletTransactions] (
    [Id]            INT            IDENTITY(1,1) NOT NULL,
    [UserId]        NVARCHAR(450)  NOT NULL,
    [Type]          NVARCHAR(20)   NOT NULL,
    [Amount]        DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [BalanceBefore] DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [BalanceAfter]  DECIMAL(18,2)  NOT NULL DEFAULT 0,
    [Note]          NVARCHAR(500)  NULL,
    [OrderId]       INT            NULL,
    [CreatedAt]     DATETIME2(7)   NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_CustomerWalletTransactions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_CustWalletTx_Users]
        FOREIGN KEY ([UserId])  REFERENCES [dbo].[Users]([Id])  ON DELETE CASCADE,
    CONSTRAINT [FK_CustWalletTx_Orders]
        FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Orders]([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_CustWalletTx_UserId] ON [dbo].[CustomerWalletTransactions]([UserId]);
GO

-- ============================================================================
-- EF Migration history — mark known migrations as already applied
-- so `dotnet ef database update` does nothing on this schema.
-- ============================================================================
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES
    ('20260318073813_InitialDatabase',   '8.0.0'),
    ('20260420092352_InitialDatabase2',  '8.0.0'),
    ('20260428083620_AddProductFields',  '8.0.0'),
    ('20260428084814_FixDecimalTypes',   '8.0.0'),
    ('20260429134153_AddUserAddresses',  '8.0.0'),
    ('20260527161108_AddSiteSettings',   '8.0.0');
GO

-- ============================================================================
-- Sanity report
-- ============================================================================
SELECT
    t.name                                            AS [Table],
    SUM(CASE WHEN i.index_id IN (0,1) THEN p.rows END) AS [Rows]
FROM sys.tables t
LEFT JOIN sys.partitions p ON t.object_id = p.object_id
LEFT JOIN sys.indexes    i ON p.object_id = i.object_id AND p.index_id = i.index_id
WHERE t.name <> '__EFMigrationsHistory'
GROUP BY t.name
ORDER BY t.name;
GO

PRINT N'';
PRINT N'======================================================';
PRINT N'  BaseCoreDB created. All tables empty (no seed data).';
PRINT N'======================================================';
GO
