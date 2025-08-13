using ChatApp.Hubs;
using ChatApp.Data;
using ChatApp.Services;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;

using Amazon.S3;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;                 // eklendi
using Amazon;                         // eklendi

using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// --- Development'ta User Secrets'ı yükle ---
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// PostgreSQL Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=chatapp;Username=postgres;Password=password";

builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseNpgsql(connectionString));

// Services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IS3Service, S3Service>();
builder.Services.AddScoped<IBlockingService, BlockingService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<ChatApp.Services.ISimpleDataSeederService, ChatApp.Services.SimpleDataSeederService>();
builder.Services.AddMemoryCache();

// ---------------- AWS Configuration (güncellendi) ----------------
var awsOptions = builder.Configuration.GetAWSOptions();

// Region: appsettings veya secrets
var regionStr = builder.Configuration["AWS:Region"] ?? "eu-north-1";
awsOptions.Region = RegionEndpoint.GetBySystemName(regionStr);

// Credentials: User Secrets / Environment / Profile / Role
// - Eğer User Secrets'ta "AWS:AccessKey" ve "AWS:SecretKey" varsa onları kullan.
// - Yoksa awsOptions default credential chain'i (ENV/AWS CLI profile/EC2 role vs.) kullanır.
var accessKey = builder.Configuration["AWS:AccessKey"];
var secretKey = builder.Configuration["AWS:SecretKey"];
if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
{
    awsOptions.Credentials = new BasicAWSCredentials(accessKey, secretKey);
}

builder.Services.AddDefaultAWSOptions(awsOptions);
builder.Services.AddAWSService<IAmazonS3>();
// -----------------------------------------------------------------

// Session ve Cookie Authentication
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"success\": false, \"message\": \"Oturum süresi doldu. Lütfen tekrar giriş yapın.\"}");
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("Role", "Admin"));
});

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

// Database Migration ve Seed Data (Production için de güvenli)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
    try
    {
        context.Database.Migrate();
        Console.WriteLine("✅ Database migrations applied successfully");

        var adminUser = await userService.GetUserByUsernameAsync("admin");
        if (adminUser == null)
        {
            var admin = await userService.CreateUserAsync("admin", "admin123");
            admin.Email = "admin@chatapp.com";
            admin.DisplayName = "Administrator";
            await userService.UpdateUserAsync(admin);
            Console.WriteLine("✅ Admin user created (username: admin, password: admin123)");
        }

        var testUser = await userService.GetUserByUsernameAsync("test");
        if (testUser == null)
        {
            var test = await userService.CreateUserAsync("test", "test123");
            test.Email = "test@chatapp.com";
            test.DisplayName = "Test User";
            await userService.UpdateUserAsync(test);
            Console.WriteLine("✅ Test user created (username: test, password: test123)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database setup failed: {ex.Message}");
    }
}

app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();

// Session middleware (authentication'dan sonra)
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        try
        {
            var userId = int.Parse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var username = context.User.Identity.Name;

            if (userId > 0)
            {
                context.Session.SetString("LastActivity", DateTime.UtcNow.ToString("O"));
                context.Session.SetString("UserId", userId.ToString());
                context.Session.SetString("Username", username ?? "");

                var userService = context.RequestServices.GetRequiredService<IUserService>();
                await userService.UpdateLastActiveAsync(userId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Session middleware error: {ex.Message}");
        }
    }

    await next();
});

app.UseAuthorization();

// Routes
app.MapControllerRoute(
    name: "chat",
    pattern: "Chat",
    defaults: new { controller = "Chat", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// API Controllers
app.MapControllers();

// SignalR Hub
app.MapHub<ChatHub>("/chathub");

// Seed data
using (var scope = app.Services.CreateScope())
{
    var dataSeeder = scope.ServiceProvider.GetRequiredService<ChatApp.Services.ISimpleDataSeederService>();
    await dataSeeder.SeedCountriesAndCitiesAsync();
    await dataSeeder.SeedUsersAsync();
}

app.Run();
