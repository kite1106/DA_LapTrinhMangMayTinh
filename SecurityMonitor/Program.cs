using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Hubs;
using SecurityMonitor.Services;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Services.Implementation;
using SecurityMonitor.Middleware;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình để lắng nghe trên tất cả các địa chỉ IP và ports
builder.WebHost.UseUrls("http://*:5100", "https://*:5101");

// Cấu hình Kestrel chi tiết
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // HTTP - Port 5100
    serverOptions.ListenAnyIP(5100, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    // HTTPS - Port 5101
    serverOptions.ListenAnyIP(5101, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        listenOptions.UseHttps();
    });

    // Cấu hình giới hạn request
    serverOptions.Limits.MaxConcurrentConnections = 100;
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.Limits.MinRequestBodyDataRate = null; // Disable rate limiting for upload
    serverOptions.Limits.MinResponseDataRate = null; // Disable rate limiting for download
});

// Cấu hình CORS với chính sách bảo mật chặt chẽ hơn
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.SetIsOriginAllowed(origin => 
            {
                // Chỉ cho phép các domain cụ thể
                var allowedOrigins = new[] 
                { 
                    "https://localhost:5101",
                    "http://localhost:5100",
                    // Thêm domain của ngrok nếu cần
                };
                return allowedOrigins.Contains(origin);
            })
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "DELETE") // Chỉ cho phép các methods cần thiết
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With"); // Chỉ cho phép các headers cần thiết
    });
});

// Cấu hình ForwardedHeaders với bảo mật tốt hơn
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Chỉ chấp nhận các headers cần thiết
    options.ForwardedHeaders = 
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    
    // Chỉ tin tưởng proxy của ngrok
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();
    
    // Giới hạn số lần forward để tránh tấn công
    options.ForwardLimit = 2; // Đủ cho ngrok và 1 proxy
    options.RequireHeaderSymmetry = true;
    
    // Thêm tất cả các header có thể chứa IP
    options.ForwardedForHeaderName = "X-Forwarded-For";
    options.ForwardedProtoHeaderName = "X-Forwarded-Proto";
    options.ForwardedHostHeaderName = "X-Forwarded-Host";
    options.OriginalForHeaderName = "X-Original-For";
    options.OriginalHostHeaderName = "X-Original-Host";
    options.OriginalProtoHeaderName = "X-Original-Proto";
});

// Kết nối DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Cấu hình Identity + Role + Token provider
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // Tắt xác thực email khi đăng ký
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    
    // Cấu hình mật khẩu
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Cấu hình Identity Options
builder.Services.Configure<IdentityOptions>(options =>
{
    // Cấu hình Password
    options.Password.RequiredLength = 8;
    
    // Cấu hình Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
});

// Cấu hình Cookie Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Email giả lập và các service chính
builder.Services.AddScoped<IEmailSender, FakeEmailSender>();
builder.Services.AddHttpClient();

// Register core services
builder.Services.AddScoped<IIPCheckerService, IPCheckerService>();
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IIPBlockingService, SecurityMonitor.Services.IPBlocking.IPBlockingService>();

// Register background services
builder.Services.AddSingleton<LoginMonitorService>();
builder.Services.AddHostedService<LoginMonitorService>();
builder.Services.AddSingleton<IIpCheckCache, IpCheckCache>();

// Đăng ký cấu hình và IP intelligence services
builder.Services.Configure<AbuseIPDBConfig>(
    builder.Configuration.GetSection("AbuseIPDB"));
builder.Services.AddScoped<IAbuseIPDBService, SecurityMonitor.Services.Implementation.AbuseIPDBService>();

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
                var accessToken = context.Request.Query["access_token"].ToString();
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
    app.UseHttpsRedirection();
}

// Serve files from wwwroot folder
app.UseStaticFiles();

// Serve files from lib folder
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "lib")),
    RequestPath = "/lib"
});

// Serve files from node_modules
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "node_modules")),
    RequestPath = "/node_modules"
});

// IMPORTANT: Các middleware quan trọng phải đặt đầu tiên
app.UseForwardedHeaders(); // Headers forwarding phải đặt trước các middleware khác
app.UseMiddleware<DetailedRequestLoggingMiddleware>(); // Logging request
app.UseCors(); // CORS policy
app.UseRouting(); // Routing
app.UseAuthentication(); // Authentication phải đặt trước Authorization
app.UseAuthorization(); // Authorization
app.UseMiddleware<LoginMonitorMiddleware>(); // Login monitoring

// Endpoints
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Alerts}/{action=Index}/{id?}");
app.MapRazorPages(); // Identity pages
app.MapHub<AlertHub>("/alertHub"); // SignalR hub

// Khởi tạo Roles và Admin user
using (var scope = app.Services.CreateScope())
{
    await RoleInitializer.InitializeAsync(scope.ServiceProvider);
}

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
