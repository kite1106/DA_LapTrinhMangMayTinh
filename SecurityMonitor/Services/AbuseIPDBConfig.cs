namespace SecurityMonitor.Services
{
    public class AbuseIPDBConfig
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = "https://api.abuseipdb.com/api/v2";
        public int MaxAgeInDays { get; set; } = 30;
        public int ConfidenceMinimum { get; set; } = 90;
    }
}
