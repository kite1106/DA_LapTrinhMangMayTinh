using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Hubs;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Services.Implementation;
using SecurityMonitor.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình để lắng nghe trên tất cả các địa chỉ IP
builder.WebHost.UseUrls("http://*:5100");

// Cấu hình Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(5100);
});

// Kết nối DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Cấu hình Identity + Role + Token provider
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Tắt xác thực email khi đăng ký
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    
    // Cấu hình mật khẩu
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Cấu hình khóa tài khoản
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Email giả lập
builder.Services.AddTransient<IEmailSender, FakeEmailSender>();

// Cookie login / access denied
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Add HttpClient support
builder.Services.AddHttpClient();

// Register services
builder.Services.AddScoped<IIPCheckerService, IPCheckerService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// Register background services
builder.Services.AddSingleton<LoginMonitorService>();
builder.Services.AddHostedService<LoginMonitorService>();
builder.Services.AddSingleton<IIpCheckCache, IpCheckCache>();

// Đăng ký cấu hình
builder.Services.Configure<AbuseIPDBConfig>(
    builder.Configuration.GetSection("AbuseIPDB"));

// Đăng ký services cốt lõi
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ILogService, LogService>();

// Đăng ký IP threat intelligence services
builder.Services.AddScoped<IAbuseIPDBService, SecurityMonitor.Services.Implementation.AbuseIPDBService>();
builder.Services.AddHostedService<FakeLogGeneratorService>();

// Background service cho việc kiểm tra IP định kỳ
builder.Services.AddHostedService<IpCheckerBackgroundService>();

// HTTP Context Accessor cho audit logging
builder.Services.AddHttpContextAccessor();

// Razor Pages + MVC
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

// JWT cho SignalR
builder.Services.AddAuthentication()
    .AddJwtBearer("SignalRJwt", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is missing.")))
        };

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/alertHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Tạo role & user mặc định
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedRolesAndDefaultUsersAsync(services);
}

// Khởi tạo dữ liệu
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    SeedData.Initialize(services);
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Add login monitoring middleware
app.UseMiddleware<LoginMonitorMiddleware>();

// Route MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Alerts}/{action=Index}/{id?}");

// Razor Pages cho Identity
app.MapRazorPages();

// SignalR hub
app.MapHub<AlertHub>("/alertHub");

app.Run();


// Hàm seed role & user mặc định
static async Task SeedRolesAndDefaultUsersAsync(IServiceProvider services)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = { "Admin", "Analyst", "User" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Tạo tài khoản mặc định
    await CreateDefaultUserAsync(userManager, "admin@gmail.com", "Admin@123", "Admin");
    await CreateDefaultUserAsync(userManager, "analyst@gmail.com", "Analyst@123", "Analyst");
    await CreateDefaultUserAsync(userManager, "user@gmail.com", "User@123", "User");
}

// Hàm tạo user mặc định dùng chung
static async Task CreateDefaultUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role)
{
    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true // Có thể set true nếu muốn bỏ xác nhận email
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
        else
        {
            throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    else
    {
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains(role))
        {
            await userManager.AddToRoleAsync(user, role);
        }

        if (user.UserName != email)
        {
            user.UserName = email;
        }

        // Có thể set EmailConfirmed tùy ý:
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
        }

        await userManager.UpdateAsync(user);
    }
}
