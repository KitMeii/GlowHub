-- ============================================================
--  Tạo bảng Wishlists (nếu chưa tồn tại)
--  Chạy script này trên SQL Server nếu không dùng EF migrations
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Wishlists')
BEGIN
    CREATE TABLE [dbo].[Wishlists] (
        [Id]         INT           IDENTITY(1,1) NOT NULL,
        [CustomerId] NVARCHAR(450) NOT NULL,
        [ProductId]  INT           NOT NULL,
        [CreatedAt]  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),

        CONSTRAINT [PK_Wishlists] PRIMARY KEY CLUSTERED ([Id] ASC),

        CONSTRAINT [FK_Wishlists_Users_CustomerId]
            FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Users] ([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_Wishlists_Products_ProductId]
            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id])
            ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX [IX_Wishlists_CustomerId_ProductId]
        ON [dbo].[Wishlists] ([CustomerId], [ProductId]);

    CREATE INDEX [IX_Wishlists_ProductId]
        ON [dbo].[Wishlists] ([ProductId]);

    PRINT 'Tạo bảng Wishlists thành công.';
END
ELSE
BEGIN
    PRINT 'Bảng Wishlists đã tồn tại, bỏ qua.';
END
