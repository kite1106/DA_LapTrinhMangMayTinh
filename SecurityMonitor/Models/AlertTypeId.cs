namespace SecurityMonitor.Models
{
    public enum AlertTypeId
    {
        SQLInjection = 1,
        BruteForce = 2,
        DDoS = 3,
        Malware = 4,
        DataLeak = 5,
        SuspiciousIP = 6,
        ReportedIP = 7,
        BlacklistedIP = 8,
        XSSAttack = 9,
        DDoSAttack = 10
    }
}
