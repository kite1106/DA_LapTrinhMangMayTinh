using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecurityMonitor.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SecurityMonitor.Services.Implementation;

/// <summary>
/// Background service để quản lý real-time updates
/// </summary>
public class RealTimeUpdateBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RealTimeUpdateBackgroundService> _logger;
    private Timer? _updateTimer;

    public RealTimeUpdateBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RealTimeUpdateBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RealTimeUpdateBackgroundService is starting.");

        try
        {
            // Bắt đầu periodic updates
            using var scope = _serviceProvider.CreateScope();
            var realTimeUpdateService = scope.ServiceProvider.GetRequiredService<IRealTimeUpdateService>();
            await realTimeUpdateService.StartPeriodicUpdatesAsync();

            // Chờ cho đến khi service bị dừng
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RealTimeUpdateBackgroundService is stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in RealTimeUpdateBackgroundService.");
        }
        finally
        {
            // Dừng periodic updates
            using var scope = _serviceProvider.CreateScope();
            var realTimeUpdateService = scope.ServiceProvider.GetRequiredService<IRealTimeUpdateService>();
            await realTimeUpdateService.StopPeriodicUpdatesAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RealTimeUpdateBackgroundService is stopping.");

        // Dừng periodic updates
        using var scope = _serviceProvider.CreateScope();
        var realTimeUpdateService = scope.ServiceProvider.GetRequiredService<IRealTimeUpdateService>();
        await realTimeUpdateService.StopPeriodicUpdatesAsync();

        await base.StopAsync(cancellationToken);
    }
} 