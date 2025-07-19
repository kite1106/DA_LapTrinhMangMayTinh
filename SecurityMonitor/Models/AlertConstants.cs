
namespace SecurityMonitor.Models
{
    public static class AlertConstants
    {
        public static class WorkingHours
        {
            public const int Start = 6;  // 6:00 AM
            public const int End = 18;   // 6:00 PM
        }

        public static class ThresholdConfig
        {
            public const int MaxAlertsPerHour = 10;        // Cảnh báo tối đa mỗi giờ
            public const int MaxAlertsPerDay = 100;        // Cảnh báo tối đa mỗi ngày
            public const int HighFrequencyThreshold = 5;   // Ngưỡng tần suất cao
            public const int CriticalFrequencyThreshold = 10; // Ngưỡng tần suất nguy hiểm
            public const int MaxSourcesForDDoS = 100;      // Số nguồn tối đa cho DDoS
            public const int MaxPortsForScan = 10;         // Số cổng tối đa cho quét
            public const double SuspiciousPatternThreshold = 0.5; // 50% cảnh báo
        }

        public static class TimeRanges
        {
            public static readonly TimeSpan ShortTerm = TimeSpan.FromMinutes(5);
            public static readonly TimeSpan MediumTerm = TimeSpan.FromMinutes(30);
            public static readonly TimeSpan LongTerm = TimeSpan.FromHours(1);
            public static readonly TimeSpan ExtendedTerm = TimeSpan.FromHours(24);
        }
        public static class SeverityLevels
        {
            public const int Low = 1;
            public const int Medium = 2;
            public const int High = 3;
            public const int Emergency = 4;
        }

        public static class AlertTypes
        {
            public const int SqlInjection = 1;
            public const int BruteForce = 2;
            public const int DDoS = 3;
            public const int Malware = 4;
            public const int DataLeak = 5;
        }

        public static class AlertStatus
        {
            public const int New = 1;
            public const int Processing = 2;
            public const int Resolved = 3;
            public const int FalsePositive = 4;
            public const int Ignored = 5;
        }

        public static class Roles
        {
            public const string Admin = "Admin";
            public const string Analyst = "Analyst";
            public const string User = "User";
        }

    }
}
