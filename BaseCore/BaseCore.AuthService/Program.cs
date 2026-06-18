using BaseCore.Repository;
using BaseCore.Repository.Authen;
using BaseCore.Services.Authen;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BaseCore Auth Service API",
        Version = "v1",
        Description = "Authentication Microservice - Login, Register, OAuth, User Management"
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
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            },
            new string[]{}
        }
    });
});

builder.Services.AddDbContext<MySqlDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// DI for Authentication Services and Repositories only
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// JWT key
var jwtKey = Encoding.ASCII.GetBytes(
    builder.Configuration["AppSettings:Secret"] ??
    builder.Configuration["Jwt:SecretKey"] ??
    "YourSecretKeyForAuthenticationShouldBeLongEnough");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme       = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.SameSite    = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath          = "/api/oauth/google";
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ValidateIssuer = false,
        ValidateAudience = false
    };
    // Cùng cơ chế OnTokenValidated như APIService — đối chiếu tv claim với DB.
    x.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var tvStr  = context.Principal?.FindFirst("tv")?.Value;

            if (string.IsNullOrEmpty(userId) || !int.TryParse(tvStr, out var tv))
            {
                context.Fail("Token thiếu claim — vui lòng đăng nhập lại");
                return;
            }

            var db   = context.HttpContext.RequestServices.GetRequiredService<MySqlDbContext>();
            var info = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.TokenVersion, u.IsActive })
                .FirstOrDefaultAsync();

            if (info == null)            { context.Fail("Tài khoản không tồn tại"); return; }
            if (!info.IsActive)          { context.Fail("Tài khoản đã bị khóa"); return; }
            if (info.TokenVersion != tv) { context.Fail("Phiên đã bị thu hồi"); return; }
        }
    };
})
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    options.ClientId     = builder.Configuration["Authentication:Google:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
    options.CallbackPath = "/api/oauth/google/callback";
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddFacebook(FacebookDefaults.AuthenticationScheme, options =>
{
    options.AppId      = builder.Configuration["Authentication:Facebook:AppId"] ?? "";
    options.AppSecret  = builder.Configuration["Authentication:Facebook:AppSecret"] ?? "";
    options.CallbackPath = "/api/oauth/facebook/callback";
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

var app = builder.Build();

// Seed data — initialize database if not exists
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MySqlDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("BaseCore Auth Service running on port 5002");
Console.WriteLine("Endpoints: /api/auth, /api/users, /api/roles, /api/oauth");
app.Run();
