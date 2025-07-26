using System;
using System.Collections.Generic;

namespace SecurityMonitor.DTOs.Security
{
    public class SecurityMetricsDto
    {
        public string TimeWindow { get; set; }
        public Dictionary<string, int> EndpointMetrics { get; set; }
        public List<UserBehaviorMetric> UserBehaviorMetrics { get; set; }
        public List<SecurityAlert> SecurityAlerts { get; set; }
        public IEnumerable<AnomalyDetection> Anomalies { get; set; }
        public SystemStatsDto SystemStats { get; set; }
    }

    public class UserBehaviorMetric
    {
        public string UserId { get; set; }
        public int RequestCount { get; set; }
        public int UniqueEndpoints { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastActivity { get; set; }
    }

    public class SecurityAlert
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string UserId { get; set; }
    }

    public class AnomalyDetection
    {
        public string Type { get; set; }
        public string Severity { get; set; }
        public string Description { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SystemStatsDto
    {
        public int TotalRequests { get; set; }
        public int ErrorCount { get; set; }
        public int UniqueUsers { get; set; }
        public int MaxConsecutiveErrors { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }
}
