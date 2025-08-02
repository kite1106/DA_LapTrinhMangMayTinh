namespace SecurityMonitor.Services.Interfaces;

public interface IFailedLoginService
{
    Task<int> GetFailedAttemptsAsync(string ipAddress);
    Task AddFailedAttemptAsync(string ipAddress, string username);
    Task ResetFailedAttemptsAsync(string ipAddress);
}
