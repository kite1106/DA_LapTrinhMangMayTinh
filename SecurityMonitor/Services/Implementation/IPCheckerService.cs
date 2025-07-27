using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SecurityMonitor.Data;
using SecurityMonitor.Hubs;
using SecurityMonitor.Models;
using System.Text.Json;

using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services.Implementation;

public class IPCheckerService : IIPCheckerService
{
    private readonly ILogger<IPCheckerService> _logger;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public async Task<bool> IsBlockedAsync(string ipAddress)
    {
        return await _context.BlockedIPs.AnyAsync(b => b.IpAddress == ipAddress);
    }

    public async Task<IEnumerable<string>> GetBlockedIPsAsync()
    {
        return await _context.BlockedIPs
            .OrderByDescending(b => b.BlockedAt)
            .Select(b => b.IpAddress)
            .ToListAsync();
    }

    public async Task BlockIPAsync(string ipAddress)
    {
        var exists = await _context.BlockedIPs.AnyAsync(b => b.IpAddress == ipAddress);
        if (!exists)
        {
            var blockedIP = new BlockedIP(
                ipAddress,
                "Manual block by admin",
                "System");
            
            _context.BlockedIPs.Add(blockedIP);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"IP address {ipAddress} has been blocked");
            await _hubContext.Clients.All.SendAsync("ReceiveAlert", $"IP address {ipAddress} has been blocked");
        }
    }

    public async Task UnblockIPAsync(string ipAddress)
    {
        var blockedIP = await _context.BlockedIPs.FirstOrDefaultAsync(b => b.IpAddress == ipAddress);
        if (blockedIP != null)
        {
            _context.BlockedIPs.Remove(blockedIP);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"IP address {ipAddress} has been unblocked");
            await _hubContext.Clients.All.SendAsync("ReceiveAlert", $"IP address {ipAddress} has been unblocked");
        }
    }

    public IPCheckerService(
        ILogger<IPCheckerService> logger,
        IHubContext<AlertHub> hubContext,
        IHttpClientFactory clientFactory,
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _logger = logger;
        _hubContext = hubContext;
        _clientFactory = clientFactory;
        _configuration = configuration;
        _context = context;
    }

    public async Task<IEnumerable<Alert>> CheckIPAsync(string ipAddress)
    {
        var alerts = new List<Alert>();

        try
        {
            // Kiểm tra IP trong danh sách đen
            if (await IsBlacklistedAsync(ipAddress))
            {
                var alert = new Alert
                {
                    Title = "IP nằm trong danh sách đen",
                    Description = $"IP {ipAddress} được phát hiện trong danh sách IP nguy hiểm",
                    SourceIp = ipAddress,
                    AlertTypeId = (int)AlertTypeId.BlacklistedIP, // Value is now 8
                    SeverityLevelId = (int)SeverityLevelId.Critical, // Changed from High to Critical
                    StatusId = (int)AlertStatusId.New, // Value is still 1
                    Timestamp = DateTime.UtcNow
                };

                alerts.Add(alert);

                // Gửi cảnh báo realtime qua SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
                {
                    alert.Title,
                    alert.Description,
                    alert.SourceIp,
                    alert.Timestamp,
                    SeverityLevel = "High",
                    Type = "Blacklisted IP"
                });
            }

            // Kiểm tra số lượng request từ IP này
            if (await CheckRequestRateAsync(ipAddress))
            {
                var alert = new Alert
                {
                    Title = "Phát hiện nhiều request từ một IP",
                    Description = $"IP {ipAddress} gửi quá nhiều request trong thời gian ngắn",
                    SourceIp = ipAddress,
                    AlertTypeId = (int)AlertTypeId.BruteForce, // Value is now 2
                    SeverityLevelId = (int)SeverityLevelId.High, // Changed from Medium to High for brute force
                    StatusId = (int)AlertStatusId.New, // Value is still 1
                    Timestamp = DateTime.UtcNow
                };

                alerts.Add(alert);

                await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
                {
                    alert.Title,
                    alert.Description,
                    alert.SourceIp,
                    alert.Timestamp,
                    SeverityLevel = "Medium",
                    Type = "Rate Limit"
                });
            }

            // Kiểm tra nếu IP là từ quốc gia đáng ngờ
            var country = await GetIPCountryAsync(ipAddress);
            if (!string.IsNullOrEmpty(country) && IsSuspiciousCountry(country))
            {
                var alert = new Alert
                {
                    Title = "IP từ khu vực đáng ngờ",
                    Description = $"IP {ipAddress} đến từ {country}, một khu vực có nhiều hoạt động đáng ngờ",
                    SourceIp = ipAddress,
                    AlertTypeId = (int)AlertTypeId.SuspiciousIP, // Changed from SuspiciousLocation to SuspiciousIP (value 6)
                    SeverityLevelId = (int)SeverityLevelId.Medium, // Value is still 2
                    StatusId = (int)AlertStatusId.New, // Value is still 1
                    Timestamp = DateTime.UtcNow
                };

                alerts.Add(alert);

                await _hubContext.Clients.All.SendAsync("ReceiveAlert", new
                {
                    alert.Title,
                    alert.Description,
                    alert.SourceIp,
                    alert.Timestamp,
                    SeverityLevel = "Medium",
                    Type = "Suspicious Location"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra IP {IP}", ipAddress);
        }

        return alerts;
    }

    private static readonly object _lockObj = new();
    private static readonly Dictionary<string, (int Count, DateTime FirstRequest)> _requestCounts = new();
    private const int MAX_REQUESTS = 60;
    private const int WINDOW_MINUTES = 1;

    private Task<bool> CheckRequestRateAsync(string ipAddress)
    {
        lock (_lockObj)
        {
            if (!_requestCounts.ContainsKey(ipAddress))
            {
                _requestCounts[ipAddress] = (1, DateTime.UtcNow);
                return Task.FromResult(false);
            }

            var (count, firstRequest) = _requestCounts[ipAddress];
            var windowEnd = firstRequest.AddMinutes(WINDOW_MINUTES);

            if (DateTime.UtcNow > windowEnd)
            {
                _requestCounts[ipAddress] = (1, DateTime.UtcNow);
                return Task.FromResult(false);
            }

            _requestCounts[ipAddress] = (count + 1, firstRequest);

            // Cleanup old entries
            if (_requestCounts.Count > 10000) // Prevent memory leak
            {
                var now = DateTime.UtcNow;
                var oldEntries = _requestCounts.Where(kvp => now.Subtract(kvp.Value.FirstRequest).TotalMinutes > WINDOW_MINUTES).Select(kvp => kvp.Key).ToList();
                foreach (var key in oldEntries)
                {
                    _requestCounts.Remove(key);
                }
            }

            return Task.FromResult(count + 1 > MAX_REQUESTS);
        }
    }
    

    private Task<bool> IsBlacklistedAsync(string ipAddress)
    {
        // Danh sách IP đen cứng
        var blacklistedIPs = new HashSet<string>
        {
            "1.2.3.4",      // Example
            "5.6.7.8"       // Example
        };

        // TODO: Thêm kiểm tra từ database nếu cần
        return Task.FromResult(blacklistedIPs.Contains(ipAddress));
    }

    private async Task<string?> GetIPCountryAsync(string ipAddress)
    {
        try
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "SecurityMonitor/1.0");
            
            // Rate limiting - đợi nếu cần
            await Task.Delay(100); // Max 10 requests/second
            
            var response = await client.GetFromJsonAsync<IPInfoResponse>($"http://ip-api.com/json/{ipAddress}");
            return response?.Country;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi truy vấn thông tin IP {IP}", ipAddress);
            return null;
        }
    }

    private bool IsSuspiciousCountry(string country)
    {
        // Danh sách các quốc gia cần theo dõi đặc biệt
        var suspiciousCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "North Korea",
            "Russia",
            "Iran",
            "China",
            "Nigeria",
            "Romania"
            // Thêm các quốc gia khác nếu cần
        };

        return suspiciousCountries.Contains(country);
    }
}

public class IPInfoResponse
{
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? City { get; set; }
}
