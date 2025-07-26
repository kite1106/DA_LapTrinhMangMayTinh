using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces;

public interface ILogAnalyzerService
{
    Task AnalyzeRequestAsync(string ipAddress, string endpoint, int statusCode, string userId);
}
