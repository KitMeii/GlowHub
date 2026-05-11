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

                // RowVersion - Optimistic Concurrency
                entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();

                // Relationship with Category
                entity.HasOne(e => e.Category)
                      .WithMany()
                      .HasForeignKey(e => e.CategoryId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Order entity
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).HasMaxLength(450).IsRequired();
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingAddress).HasMaxLength(500);
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
                entity.Property(e => e.Note).HasMaxLength(500);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
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

            // Configure ReView entity
            modelBuilder.Entity<Review>(e =>
            {
                e.ToTable("Reviews");
                e.HasKey(x => x.Id);
                e.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                e.Property(x => x.Comment).HasMaxLength(1000);

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

            // Configure UserAddress entity
            modelBuilder.Entity<UserAddress>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

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
