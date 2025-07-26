using System;
using System.Collections.Generic;

namespace SecurityMonitor.DTOs.Dashboard
{
    public class DashboardDataDto
    {
        public int TotalUsersCount { get; set; }
        public int TotalAlertsCount { get; set; }
        public int BlockedIPsCount { get; set; }
        public int TotalLogsCount { get; set; }
        public List<RecentActivityDto> RecentActivities { get; set; } = new();
        public ChartDataDto AlertsChartData { get; set; } = new();
        public ChartDataDto AlertTypesChartData { get; set; } = new();
        public SecurityMetricsDto SecurityMetrics { get; set; } = new();
    }

    public class RecentActivityDto
    {
        public DateTime Timestamp { get; set; }
        public string? IpAddress { get; set; }
        public string? UserId { get; set; }
        public string? Action { get; set; }
        public string? Details { get; set; }
    }

    public class ChartDataDto
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Data { get; set; } = new();
    }
}
