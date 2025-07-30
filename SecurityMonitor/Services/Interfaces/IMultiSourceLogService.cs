using SecurityMonitor.Models;

namespace SecurityMonitor.Services.Interfaces;

public interface IMultiSourceLogService
{
    // Apache Access Log
    Task ProcessApacheAccessLogAsync(string logContent, string sourceIp);
    
    // Nginx Error Log
    Task ProcessNginxErrorLogAsync(string logContent, string sourceIp);
    
    // Windows Event Log
    Task ProcessWindowsEventLogAsync(string evtxContent, string sourceIp);
    
    // Linux Syslog
    Task ProcessLinuxSyslogAsync(string logContent, string sourceIp);
    
    // MySQL Error Log
    Task ProcessMySQLErrorLogAsync(string logContent, string sourceIp);
    
    // Firewall Log
    Task ProcessFirewallLogAsync(string logContent, string sourceIp);
    
    // Custom App JSON Log
    Task ProcessCustomAppLogAsync(string jsonContent, string sourceIp);
    
    // Generic log processor
    Task ProcessGenericLogAsync(string logContent, string sourceType, string sourceIp);
} 