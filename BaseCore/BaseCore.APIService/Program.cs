using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using BaseCore.Repository;
using BaseCore.Repository.EFCore;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();

// Swagger Configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BaseCore API Service",
        Version = "v1",
        Description = "Business Logic Microservice - Products, Categories, Orders (Bài 10, 11)"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

//app.UseCors("AllowAll");

//MySQL Configuration with EF Core
//var connectionString = builder.Configuration.GetConnectionString("MySQL")
//    ?? "Server=localhost;Database=BaseCoreSales;User=root;Password=;";
//builder.Services.AddDbContext<MySqlDbContext>(options =>
//    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));



builder.Services.AddDbContext<MySqlDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Repository Registration - Products, Categories, Orders
builder.Services.AddScoped<IProductRepositoryEF, ProductRepositoryEF>();
builder.Services.AddScoped<ICategoryRepositoryEF, CategoryRepositoryEF>();
builder.Services.AddScoped<IOrderRepositoryEF, OrderRepositoryEF>();
builder.Services.AddScoped<IOrderDetailRepositoryEF, OrderDetailRepositoryEF>();
builder.Services.AddScoped<ICartRepositoryEF, CartRepositoryEF>();

// JWT Authentication
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:SecretKey"] ?? "YourSecretKeyForAuthenticationShouldBeLongEnough");
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

var app = builder.Build();

// Auto initialize database — EnsureCreated nếu DB chưa có; sau đó patch
// schema để bổ sung các cột mới (idempotent — IF COL_LENGTH IS NULL).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MySqlDbContext>();
    db.Database.EnsureCreated();

    // Schema patch idempotent: đảm bảo Orders có đủ cột checkout mới
    // (CustomerName/Email/Phone, ShippingFee, VoucherCode, DiscountAmount).
    // PaymentMethod đã được thêm bởi migration cũ nên không patch lại.
    // Dùng IF COL_LENGTH để không lỗi nếu cột đã add từ trước.
    db.Database.ExecuteSqlRaw(@"
        IF COL_LENGTH('Orders', 'CustomerName') IS NULL
            ALTER TABLE Orders ADD CustomerName nvarchar(200) NULL;
        IF COL_LENGTH('Orders', 'CustomerEmail') IS NULL
            ALTER TABLE Orders ADD CustomerEmail nvarchar(200) NULL;
        IF COL_LENGTH('Orders', 'CustomerPhone') IS NULL
            ALTER TABLE Orders ADD CustomerPhone nvarchar(50) NULL;
        IF COL_LENGTH('Orders', 'ShippingFee') IS NULL
            ALTER TABLE Orders ADD ShippingFee decimal(18,2) NOT NULL DEFAULT 0;
        IF COL_LENGTH('Orders', 'VoucherCode') IS NULL
            ALTER TABLE Orders ADD VoucherCode nvarchar(50) NULL;
        IF COL_LENGTH('Orders', 'DiscountAmount') IS NULL
            ALTER TABLE Orders ADD DiscountAmount decimal(18,2) NOT NULL DEFAULT 0;
        IF COL_LENGTH('Orders', 'PaymentMethod') IS NULL
            ALTER TABLE Orders ADD PaymentMethod nvarchar(20) NOT NULL DEFAULT 'COD';
    ");
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Đảm bảo thư mục wwwroot tồn tại trước khi serve static (controller tạo
// wwwroot/images/products/ lazy khi có upload đầu tiên, nên có thể chưa có).
var webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(webRoot)) Directory.CreateDirectory(webRoot);

// Serve ảnh upload: /images/products/{file}.jpg → wwwroot/images/products/{file}.jpg
app.UseStaticFiles();

app.UseRouting();
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("BaseCore API Service running on port 5001");
Console.WriteLine("Endpoints: /api/products, /api/categories, /api/orders");
app.Run();
