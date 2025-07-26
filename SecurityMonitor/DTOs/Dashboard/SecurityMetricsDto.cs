using System;
using System.Collections.Generic;

namespace SecurityMonitor.DTOs.Dashboard
{
    public class SecurityMetricsDto
    {
        public SensitiveEndpointMetrics SensitiveEndpoints { get; set; } = new();
        public AnomalyMetrics Anomalies { get; set; } = new();
        public BehaviorMetrics Behaviors { get; set; } = new();
        public SystemErrorMetrics SystemErrors { get; set; } = new();
        public SecurityKeywordMetrics Keywords { get; set; } = new();
    }

    public class SensitiveEndpointMetrics
    {
        public int TotalAccessAttempts { get; set; }
        public int UnauthorizedAttempts { get; set; }
        public int BlockedAttempts { get; set; }
        public List<EndpointAccess> RecentAccesses { get; set; } = new();
    }

    public class EndpointAccess
    {
        public string Endpoint { get; set; } = null!;
        public string IpAddress { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public int StatusCode { get; set; }
    }

    public class AnomalyMetrics
    {
        public int HighRequestRateIPs { get; set; }
        public int ScanningAttempts { get; set; }
        public int PotentialDDoSAlerts { get; set; }
        public List<IPActivity> SuspiciousIPs { get; set; } = new();
    }

    public class IPActivity
    {
        public string IpAddress { get; set; } = null!;
        public int RequestsPerMinute { get; set; }
        public int ErrorCount { get; set; }
        public string ActivityType { get; set; } = null!;
    }

    public class BehaviorMetrics
    {
        public int PasswordResetAttempts { get; set; }
        public int EmailChangeAttempts { get; set; }
        public int SuspiciousActivities { get; set; }
        public List<UserActivity> RecentActivities { get; set; } = new();
    }

    public class UserActivity
    {
        public string UserId { get; set; } = null!;
        public string ActivityType { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string Details { get; set; } = null!;
    }

    public class SystemErrorMetrics
    {
        public int TotalErrors { get; set; }
        public int ConsecutiveErrors { get; set; }
        public int UniqueErrorTypes { get; set; }
        public List<ErrorEvent> RecentErrors { get; set; } = new();
    }

    public class ErrorEvent
    {
        public string ErrorType { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = null!;
        public int Count { get; set; }
    }

    public class SecurityKeywordMetrics
    {
        public int TotalDetections { get; set; }
        public int HighRiskDetections { get; set; }
        public List<KeywordDetection> RecentDetections { get; set; } = new();
    }

    public class KeywordDetection
    {
        public string Keyword { get; set; } = null!;
        public string Context { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = null!;
    }
}
