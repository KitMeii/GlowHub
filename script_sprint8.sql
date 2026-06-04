-- ============================================================
-- 29/05/2026
-- Script Sprint 8: Admin Dashboard — SystemConfigs + Banners/Categories check
-- Chạy nhiều lần không lỗi (IF NOT EXISTS / IF COL_LENGTH)
-- ============================================================

USE BaseCoreDB
GO

-- ─────────────────────────────────────────────────────────────
-- 1. SiteSettings — đảm bảo có đủ cột (Label, UpdatedBy)
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Label' AND Object_ID = OBJECT_ID('SiteSettings'))
    ALTER TABLE [dbo].[SiteSettings] ADD [Label] NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UpdatedBy' AND Object_ID = OBJECT_ID('SiteSettings'))
    ALTER TABLE [dbo].[SiteSettings] ADD [UpdatedBy] NVARCHAR(450) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('SiteSettings'))
    ALTER TABLE [dbo].[SiteSettings] ADD [UpdatedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE();
GO

-- ─────────────────────────────────────────────────────────────
-- 2. Banners — đảm bảo có SortOrder, IsActive, Subtitle, BgColor
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'SortOrder' AND Object_ID = OBJECT_ID('Banners'))
    ALTER TABLE [dbo].[Banners] ADD [SortOrder] INT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'IsActive' AND Object_ID = OBJECT_ID('Banners'))
    ALTER TABLE [dbo].[Banners] ADD [IsActive] BIT NOT NULL DEFAULT 1;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Subtitle' AND Object_ID = OBJECT_ID('Banners'))
    ALTER TABLE [dbo].[Banners] ADD [Subtitle] NVARCHAR(500) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'BgColor' AND Object_ID = OBJECT_ID('Banners'))
    ALTER TABLE [dbo].[Banners] ADD [BgColor] NVARCHAR(20) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'UpdatedAt' AND Object_ID = OBJECT_ID('Banners'))
    ALTER TABLE [dbo].[Banners] ADD [UpdatedAt] DATETIME2(7) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'CreatedBy' AND Object_ID = OBJECT_ID('Banners'))
    ALTER TABLE [dbo].[Banners] ADD [CreatedBy] NVARCHAR(450) NULL;
GO

-- ─────────────────────────────────────────────────────────────
-- 3. Categories — đảm bảo có Description
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Description' AND Object_ID = OBJECT_ID('Categories'))
    ALTER TABLE [dbo].[Categories] ADD [Description] NVARCHAR(500) NULL;
GO

-- ─────────────────────────────────────────────────────────────
-- 4. Orders — đảm bảo có Discount column
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = 'Discount' AND Object_ID = OBJECT_ID('Orders'))
    ALTER TABLE [dbo].[Orders] ADD [Discount] DECIMAL(18,2) NOT NULL DEFAULT 0;
GO

-- ─────────────────────────────────────────────────────────────
-- 5. Seed SystemConfigs (SiteSettings) — cài đặt mặc định
-- ─────────────────────────────────────────────────────────────

-- General
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'site.name')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('site.name', N'GlowHub', 'text', 'general', N'Tên Website', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'site.tagline')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('site.tagline', N'Làm Đẹp Mỗi Ngày', 'text', 'general', N'Slogan', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'site.email')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('site.email', 'support@glowhub.vn', 'text', 'general', N'Email Hỗ Trợ', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'site.phone')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('site.phone', '1800 6868', 'text', 'general', N'Số Điện Thoại', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'site.address')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('site.address', N'123 Đường Làm Đẹp, Q.1, TP.HCM', 'text', 'general', N'Địa Chỉ', GETUTCDATE());
GO

-- Homepage
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'home.hero.title')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('home.hero.title', N'Làm Đẹp Mỗi Ngày', 'text', 'homepage', N'Tiêu Đề Hero', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'home.hero.subtitle')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('home.hero.subtitle', N'Khám phá bộ sưu tập mỹ phẩm cao cấp', 'text', 'homepage', N'Phụ Đề Hero', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'home.free_ship_threshold')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('home.free_ship_threshold', '500000', 'text', 'homepage', N'Ngưỡng Miễn Phí Ship (₫)', GETUTCDATE());
GO

-- Social
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'social.facebook')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('social.facebook', 'https://facebook.com/glowhub', 'text', 'social', 'Facebook URL', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'social.instagram')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('social.instagram', 'https://instagram.com/glowhub', 'text', 'social', 'Instagram URL', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'social.tiktok')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('social.tiktok', 'https://tiktok.com/@glowhub', 'text', 'social', 'TikTok URL', GETUTCDATE());
GO

-- SEO
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'seo.title')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('seo.title', 'GlowHub — Mỹ Phẩm Cao Cấp', 'text', 'seo', N'Meta Title', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'seo.description')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('seo.description', N'GlowHub — Nền tảng mua sắm mỹ phẩm cao cấp uy tín hàng đầu Việt Nam', 'text', 'seo', N'Meta Description', GETUTCDATE());
GO

-- Orders / Commerce
IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'order.default_commission')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('order.default_commission', '10', 'text', 'general', N'Hoa Hồng Mặc Định (%)', GETUTCDATE());
GO

IF NOT EXISTS (SELECT TOP 1 1 FROM [dbo].[SiteSettings] WHERE [Key] = 'order.max_cancel_minutes')
    INSERT INTO [dbo].[SiteSettings] ([Key], [Value], [Type], [Group], [Label], [UpdatedAt])
    VALUES ('order.max_cancel_minutes', '1440', 'text', 'general', N'Thời Gian Hủy Đơn (phút)', GETUTCDATE());
GO

-- ─────────────────────────────────────────────────────────────
-- 6. Check kết quả
-- ─────────────────────────────────────────────────────────────
SELECT 'SiteSettings' AS TableName, COUNT(*) AS Rows FROM [dbo].[SiteSettings] UNION ALL
SELECT 'Banners',   COUNT(*) FROM [dbo].[Banners]   UNION ALL
SELECT 'Categories',COUNT(*) FROM [dbo].[Categories] UNION ALL
SELECT 'FlashSales',COUNT(*) FROM [dbo].[FlashSales];
GO
