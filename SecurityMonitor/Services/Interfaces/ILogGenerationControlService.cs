namespace SecurityMonitor.Services.Interfaces
{
    public interface ILogGenerationControlService
    {
        bool IsLogGenerationEnabled { get; }
        Task<bool> EnableLogGenerationAsync();
        Task<bool> DisableLogGenerationAsync();
        Task<bool> ToggleLogGenerationAsync();
        Task<bool> GetLogGenerationStatusAsync();
    }
} 