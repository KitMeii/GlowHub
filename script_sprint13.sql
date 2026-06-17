-- ============================================================
-- Sprint 13: Homepage fixes
--   1. SiteSettings seed for Hero Banner
--   2. Categories.ImageUrl column
-- Run after: script_sprint12.sql
-- ============================================================

USE BaseCoreDB
GO

PRINT 'Sprint 13: Homepage SiteSettings seed + Category.ImageUrl';

-- ─────────────────────────────────────────────────────────────
-- Section 1: SiteSettings — Hero Banner keys
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = 'hero_img')
    INSERT INTO SiteSettings ([Key], [Value], [Group], [Label], [Type], [UpdatedAt])
    VALUES ('hero_img',
            'https://images.unsplash.com/photo-1620916566398-39f1143ab7be?w=1400&q=85',
            'homepage', 'Ảnh nền Hero', 'image', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = 'hero_title')
    INSERT INTO SiteSettings ([Key], [Value], [Group], [Label], [Type], [UpdatedAt])
    VALUES ('hero_title', 'Vẻ Đẹp Rạng Rỡ<br />Từ Bên Trong',
            'homepage', 'Tiêu đề Hero', 'text', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = 'hero_sub')
    INSERT INTO SiteSettings ([Key], [Value], [Group], [Label], [Type], [UpdatedAt])
    VALUES ('hero_sub', 'Skincare · Makeup · Nước Hoa · Chính Hãng',
            'homepage', 'Phụ đề Hero', 'text', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = 'hero_eyebrow')
    INSERT INTO SiteSettings ([Key], [Value], [Group], [Label], [Type], [UpdatedAt])
    VALUES ('hero_eyebrow', 'Bộ Sưu Tập Mới · 2026',
            'homepage', 'Eyebrow Hero', 'text', GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM SiteSettings WHERE [Key] = 'hero_cta')
    INSERT INTO SiteSettings ([Key], [Value], [Group], [Label], [Type], [UpdatedAt])
    VALUES ('hero_cta', 'Khám Phá Ngay',
            'homepage', 'Nút CTA Hero', 'text', GETUTCDATE());

PRINT 'Section 1 done: SiteSettings hero_* keys seeded';

-- ─────────────────────────────────────────────────────────────
-- Section 2: Categories.ImageUrl column
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID(N'Categories') AND name = N'ImageUrl')
    ALTER TABLE Categories ADD ImageUrl NVARCHAR(500) NULL;

PRINT 'Section 2 done: Categories.ImageUrl column added';

-- ─────────────────────────────────────────────────────────────
-- Section 3: Seed ảnh cho các category mỹ phẩm (tùy chỉnh)
-- ─────────────────────────────────────────────────────────────
UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1556228578-8c89e6adf883?w=800&q=80'
WHERE Name = 'Chăm Sóc Da Mặt' AND (ImageUrl IS NULL OR ImageUrl = '');

UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1586495777744-4e6232bf2919?w=700&q=80'
WHERE Name = 'Trang Điểm' AND (ImageUrl IS NULL OR ImageUrl = '');

UPDATE Categories SET ImageUrl = 'https://images.unsplash.com/photo-1541643600914-78b084683702?w=600&q=80'
WHERE Name = 'Nước Hoa' AND (ImageUrl IS NULL OR ImageUrl = '');

PRINT 'Section 3 done: Category image URLs seeded';
PRINT 'Sprint 13 complete.';
