using MongoDB.Driver;
using BaseCore.Common;
using BaseCore.Entities;

namespace BaseCore.Repository
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Category> Categories => _database.GetCollection<Category>("Categories");
        public IMongoCollection<Product> Products => _database.GetCollection<Product>("Products");
        public IMongoCollection<Order> Orders => _database.GetCollection<Order>("Orders");
        public IMongoCollection<OrderDetail> OrderDetails => _database.GetCollection<OrderDetail>("OrderDetails");

        public async Task SeedDataAsync()
        {
            // Seed Categories if empty
            if (await Categories.CountDocumentsAsync(FilterDefinition<Category>.Empty) == 0)
            {
                var categories = new List<Category>
                {
                    new Category { Id = 1, Name = "Electronics", Description = "Electronic devices and gadgets" },
                    new Category { Id = 2, Name = "Clothing", Description = "Apparel and fashion items" },
                    new Category { Id = 3, Name = "Books", Description = "Books and publications" },
                    new Category { Id = 4, Name = "Home & Garden", Description = "Home and garden products" },
                    new Category { Id = 5, Name = "Sports", Description = "Sports equipment and accessories" }
                };
                await Categories.InsertManyAsync(categories);
            }

            // Seed Admin User if not exists
            var adminFilter = Builders<User>.Filter.Eq(u => u.UserName, "admin");
            var existingAdmin = await Users.Find(adminFilter).FirstOrDefaultAsync();
            if (existingAdmin == null)
            {
                // Hash password using PBKDF2
                byte[] salt;
                string hashedPassword = TokenHelper.HashPassword("admin123", out salt);

                var adminUser = new User
                {
                    Id = "507f1f77bcf86cd799439011", // Valid ObjectId
                    UserName = "admin",
                    Password = hashedPassword,
                    Salt = salt,
                    Name = "Administrator",
                    Email = "admin@robotvibot.com",
                    Phone = "0123456789",
                    Position = "System Administrator",
                    Contact = "",
                    Image = "",
                    IsActive = true,
                    UserType = 1,
                    Created = DateTime.Now
                };
                await Users.InsertOneAsync(adminUser);
                Console.WriteLine("Admin user created with hashed password: admin / admin123");
            }
            else if (existingAdmin.Salt == null || existingAdmin.Salt.Length == 0)
            {
                // Update existing admin with hashed password if currently plain text
                byte[] salt;
                string hashedPassword = TokenHelper.HashPassword("admin123", out salt);

                var update = Builders<User>.Update
                    .Set(u => u.Password, hashedPassword)
                    .Set(u => u.Salt, salt)
                    .Set(u => u.IsActive, true);
                await Users.UpdateOneAsync(adminFilter, update);
                Console.WriteLine("Admin user password hashed: admin / admin123");
            }

            // Seed Products if empty
            if (await Products.CountDocumentsAsync(FilterDefinition<Product>.Empty) == 0)
            {
                var products = new List<Product>
                {
                    new Product { Id = 1, Name = "Laptop Dell XPS 15", Price = 35000000, Stock = 10, CategoryId = 1, Description = "High-performance laptop", ImageUrl = "" },
                    new Product { Id = 2, Name = "iPhone 15 Pro", Price = 28000000, Stock = 15, CategoryId = 1, Description = "Latest Apple smartphone", ImageUrl = "" },
                    new Product { Id = 3, Name = "T-Shirt Cotton", Price = 250000, Stock = 100, CategoryId = 2, Description = "Comfortable cotton t-shirt", ImageUrl = "" },
                    new Product { Id = 4, Name = "Programming Book", Price = 450000, Stock = 50, CategoryId = 3, Description = "Learn programming basics", ImageUrl = "" },
                    new Product { Id = 5, Name = "Garden Tools Set", Price = 850000, Stock = 25, CategoryId = 4, Description = "Complete gardening toolkit", ImageUrl = "" }
                };
                await Products.InsertManyAsync(products);
            }
        }
    }
}
