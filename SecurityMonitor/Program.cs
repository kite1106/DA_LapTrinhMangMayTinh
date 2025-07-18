using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services;
using SecurityMonitor.Hubs;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 📦 Kết nối DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 🔐 Cấu hình Identity + Role + Token provider
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false; // ✅ Không yêu cầu xác nhận email
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultProvider;

    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 📧 Email giả lập
builder.Services.AddTransient<IEmailSender, FakeEmailSender>();

// 🛡️ Cookie login / access denied
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// 📦 Dịch vụ hệ thống
builder.Services.AddScoped<IAlertService, AlertService>();
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IAuditService, AuditService>();

// 🖼️ Razor Pages + MVC
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

// 🔐 JWT cho SignalR
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
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "default_key"))
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

// ✅ Tạo role & user mặc định
await SeedRolesAndDefaultUsersAsync(app.Services.CreateScope().ServiceProvider);

// 🧱 Middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Route MVC
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Alerts}/{action=Index}/{id?}");

// ✅ Razor Pages cho Identity
app.MapRazorPages();

// ✅ SignalR hub
app.MapHub<AlertHub>("/alertHub");

app.Run();


// 🧩 Hàm seed role & user mặc định
async Task SeedRolesAndDefaultUsersAsync(IServiceProvider services)
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

    // ✅ Tạo tài khoản mặc định
    await CreateDefaultUserAsync(userManager, "admin@gmail.com", "Admin@123", "Admin");
    await CreateDefaultUserAsync(userManager, "analyst@gmail.com", "Analyst@123", "Analyst");
    await CreateDefaultUserAsync(userManager, "user@gmail.com", "User@123", "User");
}

// 🧩 Hàm tạo user mặc định dùng chung
async Task CreateDefaultUserAsync(UserManager<ApplicationUser> userManager, string email, string password, string role)
{
    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = email, // ✅ Đặt UserName là email
            Email = email,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
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

        user.EmailConfirmed = false;
        await userManager.UpdateAsync(user);
    }
}
