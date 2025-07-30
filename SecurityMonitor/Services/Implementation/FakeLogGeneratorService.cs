using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;

namespace SecurityMonitor.Services.Implementation
{
    public class FakeLogGeneratorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FakeLogGeneratorService> _logger;
        private static int _counter = 1;

        public FakeLogGeneratorService(
            IServiceScopeFactory scopeFactory,
            ILogger<FakeLogGeneratorService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🟢 FakeLogGeneratorService is running...");

            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var logService = scope.ServiceProvider.GetRequiredService<ILogService>();

                string fakeIp = $"203.0.113.{_counter++ % 255}";

                var log = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    IpAddress = fakeIp,
                    Message = $"Tấn công brute force từ IP {fakeIp}",
                    LogSourceId = 1
                };

                await logService.CreateLogAsync(log);
                _logger.LogInformation("📝 Đã tạo log giả từ IP: {IP}", fakeIp);

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
