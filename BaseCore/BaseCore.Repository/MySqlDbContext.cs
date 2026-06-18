using Microsoft.EntityFrameworkCore;
using BaseCore.Entities;

namespace BaseCore.Repository
{
    /// <summary>
    /// Entity Framework Core DbContext for MySQL
    /// Used for teaching EF Core concepts (Bài 10)
    /// </summary>
    public class MySqlDbContext : DbContext
    {
        public MySqlDbContext(DbContextOptions<MySqlDbContext> options) : base(options)
        {
        }

        // DbSet for each entity
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Banner> Banners { get; set; }
        public DbSet<FeaturedProduct> FeaturedProducts { get; set; }
        public DbSet<SiteSetting> SiteSettings { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<Shop> Shops { get; set; }
        public DbSet<ShopProduct> ShopProducts { get; set; }
        public DbSet<QnA> QnAs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Wishlist> Wishlists { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }
        public DbSet<OrderStatusLog> OrderStatusLogs { get; set; }
        public DbSet<FlashSale> FlashSales { get; set; }
        public DbSet<FlashSaleProduct> FlashSaleProducts { get; set; }
        public DbSet<RecentlyViewed> RecentlyVieweds { get; set; }
        public DbSet<CustomerVoucher> CustomerVouchers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PayoutHistory> PayoutHistories { get; set; }
        public DbSet<Dispute> Disputes { get; set; }
        public DbSet<SellerWallet> SellerWallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<SubOrder> SubOrders { get; set; }
        public DbSet<SubOrderItem> SubOrderItems { get; set; }
        public DbSet<CustomerWallet> CustomerWallets { get; set; }
        public DbSet<CustomerWalletTransaction> CustomerWalletTransactions { get; set; }
        public DbSet<ShopFollow> ShopFollows { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(450);
                entity.Property(e => e.UserName).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Password).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.OAuthProvider).HasMaxLength(20);
                entity.Property(e => e.OAuthId).HasMaxLength(200);
                entity.HasIndex(e => e.UserName).IsUnique();
            });

            // Configure Category entity
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // Configure Product entity
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Price).HasPrecision(18, 2);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.Images).HasMaxLength(2000);
                entity.Property(e => e.Specifications).HasMaxLength(2000);

                // RowVersion - Optimistic Concurrency
                entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();

                // Relationship with Category
                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Relationship with Shop (nullable)
                entity.Property(e => e.ShopId).HasMaxLength(450);
                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure Order entity
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingFee).HasPrecision(18, 2);
                entity.Property(e => e.FinalAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingAddress).HasMaxLength(500);
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.OrderCode).HasMaxLength(20);
                entity.Property(e => e.ReceiverName).HasMaxLength(100);
                entity.Property(e => e.ReceiverPhone).HasMaxLength(20);
                entity.Property(e => e.PaymentMethod).HasMaxLength(20);
                entity.Property(e => e.PaymentStatus).HasMaxLength(20);
                entity.Property(e => e.ShopId).HasMaxLength(450);
                entity.Property(e => e.CommissionRate).HasPrecision(5, 2);
                entity.Property(e => e.ProductRevenue).HasPrecision(18, 2);
                entity.Property(e => e.CommissionAmount).HasPrecision(18, 2);
                entity.Property(e => e.SellerPayoutAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShopVoucherDiscount).HasPrecision(18, 2);
                entity.Property(e => e.SystemVoucherDiscount).HasPrecision(18, 2);
                entity.Property(e => e.FreeshipDiscount).HasPrecision(18, 2);
                entity.Property(e => e.PayoutStatus).HasMaxLength(20);
                entity.Property(e => e.ToProvince).HasMaxLength(100);
                entity.Property(e => e.VNPayTransactionId).HasMaxLength(100);
                entity.Property(e => e.BankTransferConfirmedBy).HasMaxLength(450);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Shop)
                    .WithMany()
                    .HasForeignKey(e => e.ShopId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure OrderStatusHistory entity
            modelBuilder.Entity<OrderStatusHistory>(entity =>
            {
                entity.ToTable("OrderStatusHistories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.ChangedBy).HasMaxLength(450);

                entity.HasOne(e => e.Order)
                      .WithMany(o => o.StatusHistory)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure OrderDetail entity
            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.ToTable("OrderDetails");      
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

                // Relationships
                entity.HasOne(e => e.Order)
                      .WithMany(o => o.OrderDetails)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure CartItems entity
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.ToTable("CartItems");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();

                //Mỗi user chỉ có 1 dòng cho 1 sản phẩm
                entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Review entity
            modelBuilder.Entity<Review>(e =>
            {
                e.ToTable("Reviews");
                e.HasKey(x => x.Id);
                e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                e.Property(x => x.Comment).HasMaxLength(1000);
                e.Property(x => x.SellerReply).HasMaxLength(500);
                e.Property(x => x.Images).HasMaxLength(1000);

                // Mỗi user chỉ review 1 sản phẩm 1 lần
                e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();

                e.HasOne(x => x.Product)
                 .WithMany()
                 .HasForeignKey(x => x.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.UserId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // FeaturedProduct — unique (ProductId + Section)
            modelBuilder.Entity<FeaturedProduct>()
                .HasIndex(fp => new { fp.ProductId, fp.Section })
                .IsUnique();

            // FeaturedProduct → Product (cascade delete)
            modelBuilder.Entity<FeaturedProduct>()
                .HasOne(fp => fp.Product)
                .WithMany()
                .HasForeignKey(fp => fp.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // SiteSetting — Key là PK (string)
            modelBuilder.Entity<SiteSetting>()
                .HasKey(s => s.Key);

            // Voucher — Code unique
            modelBuilder.Entity<Voucher>()
                .HasIndex(v => v.Code)
                .IsUnique();

            // Voucher → Shop (nullable)
            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.Property(e => e.ShopId).HasMaxLength(450);
                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure UserAddress entity
            modelBuilder.Entity<UserAddress>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Shop entity
            modelBuilder.Entity<Shop>(entity =>
            {
                entity.ToTable("Shops");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(450);
                entity.Property(e => e.SellerId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.ShopName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Logo).HasMaxLength(500);
                entity.Property(e => e.Address).HasMaxLength(300);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.CommissionRate).HasColumnType("decimal(5,2)");

                entity.Property(e => e.Province).HasMaxLength(100);
                entity.Property(e => e.Region).HasMaxLength(20);

                entity.HasOne(e => e.Seller)
                      .WithMany()
                      .HasForeignKey(e => e.SellerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ShopProduct entity
            modelBuilder.Entity<ShopProduct>(entity =>
            {
                entity.ToTable("ShopProducts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ShopId).HasMaxLength(450).IsRequired();

                entity.HasIndex(e => new { e.ShopId, e.ProductId }).IsUnique();

                entity.HasOne(e => e.Shop)
                      .WithMany(s => s.ShopProducts)
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure QnA entity
            modelBuilder.Entity<QnA>(entity =>
            {
                entity.ToTable("QnA");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Question).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Answer).HasMaxLength(2000);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Customer)
                      .WithMany()
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Notification entity
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
                entity.Property(e => e.Link).HasMaxLength(500);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Wishlist entity
            modelBuilder.Entity<Wishlist>(entity =>
            {
                entity.ToTable("Wishlists");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerId).HasMaxLength(450).IsRequired();
                entity.HasIndex(e => new { e.CustomerId, e.ProductId }).IsUnique();

                entity.HasOne(e => e.Customer)
                      .WithMany()
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure FlashSale entity
            modelBuilder.Entity<FlashSale>(entity =>
            {
                entity.ToTable("FlashSales");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();

                entity.HasMany(e => e.Products)
                      .WithOne(p => p.FlashSale)
                      .HasForeignKey(p => p.FlashSaleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure FlashSaleProduct entity
            modelBuilder.Entity<FlashSaleProduct>(entity =>
            {
                entity.ToTable("FlashSaleProducts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SalePrice).HasPrecision(18, 2);
                entity.Property(e => e.OriginalPrice).HasPrecision(18, 2);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure RecentlyViewed entity
            modelBuilder.Entity<RecentlyViewed>(entity =>
            {
                entity.ToTable("RecentlyVieweds");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CustomerVoucher entity
            modelBuilder.Entity<CustomerVoucher>(entity =>
            {
                entity.ToTable("CustomerVouchers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();

                entity.HasIndex(e => new { e.UserId, e.VoucherId }).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Voucher)
                      .WithMany()
                      .HasForeignKey(e => e.VoucherId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure AuditLog entity
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.Property(e => e.UserName).HasMaxLength(256);
                entity.Property(e => e.EntityType).HasColumnName("Entity").HasMaxLength(100);
                entity.Property(e => e.EntityId).HasMaxLength(450);
                entity.Property(e => e.IpAddress).HasMaxLength(50);
            });

            // Configure PayoutHistory entity
            modelBuilder.Entity<PayoutHistory>(entity =>
            {
                entity.ToTable("PayoutHistories");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ShopId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.ProcessedBy).HasMaxLength(450);

                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Dispute entity
            modelBuilder.Entity<Dispute>(entity =>
            {
                entity.ToTable("Disputes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Reason).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.RefundAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ResolvedBy).HasMaxLength(450);

                entity.HasOne(e => e.Order)
                      .WithMany()
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Customer)
                      .WithMany()
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure SellerWallet entity
            modelBuilder.Entity<SellerWallet>(entity =>
            {
                entity.ToTable("SellerWallets");
                entity.HasKey(e => e.ShopId);
                entity.Property(e => e.ShopId).HasMaxLength(450);
                entity.Property(e => e.Balance).HasPrecision(18, 2);
                entity.Property(e => e.TotalEarned).HasPrecision(18, 2);
                entity.Property(e => e.TotalWithdrawn).HasPrecision(18, 2);
                entity.Property(e => e.TotalRefunded).HasPrecision(18, 2);

                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure WalletTransaction entity
            modelBuilder.Entity<WalletTransaction>(entity =>
            {
                entity.ToTable("WalletTransactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ShopId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.BalanceBefore).HasPrecision(18, 2);
                entity.Property(e => e.BalanceAfter).HasPrecision(18, 2);

                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Order)
                      .WithMany()
                      .HasForeignKey(e => e.OrderId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure CustomerWallet entity
            modelBuilder.Entity<CustomerWallet>(entity =>
            {
                entity.ToTable("CustomerWallets");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasMaxLength(450);
                entity.Property(e => e.Balance).HasPrecision(18, 2);
                entity.Property(e => e.TotalReceived).HasPrecision(18, 2);
                entity.Property(e => e.TotalSpent).HasPrecision(18, 2);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CustomerWalletTransaction entity
            modelBuilder.Entity<CustomerWalletTransaction>(entity =>
            {
                entity.ToTable("CustomerWalletTransactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.BalanceBefore).HasPrecision(18, 2);
                entity.Property(e => e.BalanceAfter).HasPrecision(18, 2);
                entity.Property(e => e.Note).HasMaxLength(500);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Order)
                      .WithMany()
                      .HasForeignKey(e => e.OrderId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure SubOrder entity
            modelBuilder.Entity<SubOrder>(entity =>
            {
                entity.ToTable("SubOrders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ShopId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.SubOrderCode).HasMaxLength(20);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingFee).HasPrecision(18, 2);
                entity.Property(e => e.FinalAmount).HasPrecision(18, 2);
                entity.Property(e => e.ProductRevenue).HasPrecision(18, 2);
                entity.Property(e => e.CommissionRate).HasPrecision(5, 2);
                entity.Property(e => e.CommissionAmount).HasPrecision(18, 2);
                entity.Property(e => e.SellerPayoutAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShopVoucherDiscount).HasPrecision(18, 2);
                entity.Property(e => e.PayoutStatus).HasMaxLength(20);
                entity.Property(e => e.TrackingCode).HasMaxLength(100);
                entity.Property(e => e.CancelReason).HasMaxLength(500);
                entity.Property(e => e.Note).HasMaxLength(500);

                entity.HasOne(e => e.Order)
                      .WithMany(o => o.SubOrders)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.NoAction);
            });

            // Configure SubOrderItem entity
            modelBuilder.Entity<SubOrderItem>(entity =>
            {
                entity.ToTable("SubOrderItems");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);

                entity.HasOne(e => e.SubOrder)
                      .WithMany(s => s.Items)
                      .HasForeignKey(e => e.SubOrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ShopFollow entity
            modelBuilder.Entity<ShopFollow>(entity =>
            {
                entity.ToTable("ShopFollows");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.ShopId).HasMaxLength(450).IsRequired();
                entity.HasIndex(e => new { e.UserId, e.ShopId }).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Shop)
                      .WithMany()
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Electronics", Description = "Electronic devices and gadgets" },
                new Category { Id = 2, Name = "Clothing", Description = "Apparel and fashion items" },
                new Category { Id = 3, Name = "Books", Description = "Books and publications" },
                new Category { Id = 4, Name = "Home & Garden", Description = "Home and garden products" },
                new Category { Id = 5, Name = "Sports", Description = "Sports equipment and accessories" }
            );

            // Seed Products
            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Laptop Dell XPS 15", Price = 35000000, Stock = 10, CategoryId = 1, Description = "High-performance laptop", ImageUrl = "" },
                new Product { Id = 2, Name = "iPhone 15 Pro", Price = 28000000, Stock = 15, CategoryId = 1, Description = "Latest Apple smartphone", ImageUrl = "" },
                new Product { Id = 3, Name = "T-Shirt Cotton", Price = 250000, Stock = 100, CategoryId = 2, Description = "Comfortable cotton t-shirt", ImageUrl = "" },
                new Product { Id = 4, Name = "Programming Book", Price = 450000, Stock = 50, CategoryId = 3, Description = "Learn programming basics", ImageUrl = "" },
                new Product { Id = 5, Name = "Garden Tools Set", Price = 850000, Stock = 25, CategoryId = 4, Description = "Complete gardening toolkit", ImageUrl = "" }
            );

            // Note: Users are managed by AuthService (MongoDB)
            // User seed data is handled by MongoDbContext.SeedDataAsync()
        }
    }
}
