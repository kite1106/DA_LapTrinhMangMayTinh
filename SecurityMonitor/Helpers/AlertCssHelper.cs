namespace SecurityMonitor.Helpers;

/// <summary>
/// Helper class cho việc chuyển đổi trạng thái và mức độ nghiêm trọng thành CSS classes
/// </summary>
public static class AlertCssHelper
{
    private static readonly Dictionary<string, string> _statusClasses = new()
    {
        { "new", "primary" },
        { "in progress", "info" },
        { "resolved", "success" }
    };

    private static readonly Dictionary<string, string> _severityClasses = new()
    {
        { "critical", "danger" },
        { "high", "warning" },
        { "medium", "info" },
        { "low", "secondary" }
    };

    /// <summary>
    /// Chuyển đổi trạng thái cảnh báo thành CSS class
    /// </summary>
    public static string GetStatusClass(string status) 
        => _statusClasses.TryGetValue(status.ToLower(), out var cssClass) ? cssClass : "secondary";

    /// <summary>
    /// Chuyển đổi mức độ nghiêm trọng thành CSS class
    /// </summary>
    public static string GetSeverityClass(string severityLevel)
        => _severityClasses.TryGetValue(severityLevel.ToLower(), out var cssClass) ? cssClass : "secondary";
}
