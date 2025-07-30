using SecurityMonitor.Data;
using SecurityMonitor.Models;
using SecurityMonitor.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SecurityMonitor.Services.Implementation
{
    public class LogAnalysisService : ILogAnalysisService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LogAnalysisService> _logger;
        private readonly IAlertService _alertService;

        public LogAnalysisService(
            ApplicationDbContext context,
            ILogger<LogAnalysisService> logger,
            IAlertService alertService)
        {
            _context = context;
            _logger = logger;
            _alertService = alertService;
        }

        public async Task<LogAnalysis> AnalyzeLogEntryAsync(LogEntry logEntry)
        {
            try
            {
                var analysis = new LogAnalysis
                {
                    LogEntryId = logEntry.Id,
                    AnalysisType = "SingleLog",
                    AnalyzedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                // Phân tích mức độ nghiêm trọng
                var severityAnalysis = AnalyzeSeverity(logEntry);
                analysis.RiskLevel = severityAnalysis.RiskLevel;
                analysis.ConfidenceScore = severityAnalysis.ConfidenceScore;

                // Phân tích pattern
                var patternAnalysis = await AnalyzePattern(logEntry);
                analysis.AnalysisResult = patternAnalysis.Result;
                analysis.IsAnomaly = patternAnalysis.IsAnomaly;
                analysis.IsThreat = patternAnalysis.IsThreat;

                // Đưa ra khuyến nghị
                analysis.Recommendations = GenerateRecommendations(logEntry, analysis);

                // Lưu vào database
                _context.LogAnalyses.Add(analysis);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Analyzed log entry {LogId}: {Result}", logEntry.Id, analysis.AnalysisResult);

                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing log entry {LogId}", logEntry.Id);
                throw;
            }
        }

        public async Task<List<LogAnalysis>> AnalyzeLogEntriesAsync(List<LogEntry> logEntries)
        {
            var analyses = new List<LogAnalysis>();
            
            foreach (var logEntry in logEntries)
            {
                var analysis = await AnalyzeLogEntryAsync(logEntry);
                analyses.Add(analysis);
            }

            return analyses;
        }

        public async Task<List<LogAnalysis>> AnalyzePatternAsync(string pattern, DateTime from, DateTime to)
        {
            var logEntries = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .Where(l => l.Message.Contains(pattern))
                .ToListAsync();

            return await AnalyzeLogEntriesAsync(logEntries);
        }

        public async Task<List<LogAnalysis>> DetectAnomaliesAsync(DateTime from, DateTime to)
        {
            var anomalies = new List<LogAnalysis>();

            // Phát hiện login attempts bất thường
            var loginAnomalies = await DetectLoginAnomaliesAsync(from, to);
            anomalies.AddRange(loginAnomalies);

            // Phát hiện IP bất thường
            var ipAnomalies = await DetectIPAnomaliesAsync(from, to);
            anomalies.AddRange(ipAnomalies);

            // Phát hiện error patterns
            var errorAnomalies = await DetectErrorAnomaliesAsync(from, to);
            anomalies.AddRange(errorAnomalies);

            return anomalies;
        }

        public async Task<List<LogAnalysis>> AnalyzeThreatsAsync(DateTime from, DateTime to)
        {
            var threats = new List<LogAnalysis>();

            // Phân tích brute force attacks
            var bruteForceThreats = await AnalyzeBruteForceThreatsAsync(from, to);
            threats.AddRange(bruteForceThreats);

            // Phân tích SQL injection attempts
            var sqlInjectionThreats = await AnalyzeSQLInjectionThreatsAsync(from, to);
            threats.AddRange(sqlInjectionThreats);

            // Phân tích XSS attempts
            var xssThreats = await AnalyzeXSSThreatsAsync(from, to);
            threats.AddRange(xssThreats);

            return threats;
        }

        public async Task<Alert?> CreateAlertFromAnalysisAsync(LogAnalysis analysis)
        {
            try
            {
                if (!analysis.IsThreat && !analysis.IsAnomaly)
                    return null;

                var logEntry = await _context.LogEntries
                    .Include(l => l.LogSource)
                    .FirstOrDefaultAsync(l => l.Id == analysis.LogEntryId);

                if (logEntry == null)
                    return null;

                                        var alert = new Alert
                        {
                            Title = $"Security {analysis.AnalysisType} Detected",
                            Description = analysis.AnalysisResult ?? "Anomaly or threat detected",
                            Timestamp = DateTime.UtcNow,
                            AlertTypeId = DetermineAlertType(analysis.AnalysisType),
                            SeverityLevelId = DetermineSeverityLevel(analysis.RiskLevel),
                            StatusId = 1, // New
                            SourceIp = logEntry.IpAddress,
                            LogId = logEntry.Id
                        };

                await _alertService.CreateAlertAsync(alert);
                return alert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert from analysis {AnalysisId}", analysis.Id);
                return null;
            }
        }

        public async Task<object> GetAnalysisStatsAsync(DateTime from, DateTime to)
        {
            var analyses = await _context.LogAnalyses
                .Where(a => a.AnalyzedAt >= from && a.AnalyzedAt <= to)
                .ToListAsync();

            return new
            {
                TotalAnalyses = analyses.Count,
                Anomalies = analyses.Count(a => a.IsAnomaly),
                Threats = analyses.Count(a => a.IsThreat),
                AverageConfidence = analyses.Average(a => a.ConfidenceScore ?? 0),
                RiskLevels = analyses.GroupBy(a => a.RiskLevel)
                    .Select(g => new { RiskLevel = g.Key, Count = g.Count() })
            };
        }

        #region Private Methods

        private (string RiskLevel, decimal ConfidenceScore) AnalyzeSeverity(LogEntry logEntry)
        {
            var riskLevel = "LOW";
            var confidence = 0.5m;

            // Phân tích dựa trên log level
            if (logEntry.LogLevelType?.Name?.ToUpper() == "ERROR")
            {
                riskLevel = "HIGH";
                confidence = 0.8m;
            }
            else if (logEntry.LogLevelType?.Name?.ToUpper() == "WARN")
            {
                riskLevel = "MEDIUM";
                confidence = 0.6m;
            }

            // Phân tích dựa trên message content
            var message = logEntry.Message?.ToLower() ?? "";
            if (message.Contains("failed") || message.Contains("error") || message.Contains("exception"))
            {
                riskLevel = "HIGH";
                confidence = Math.Max(confidence, 0.7m);
            }

            if (message.Contains("login") || message.Contains("authentication"))
            {
                riskLevel = "MEDIUM";
                confidence = Math.Max(confidence, 0.6m);
            }

            return (riskLevel, confidence);
        }

        private async Task<(string Result, bool IsAnomaly, bool IsThreat)> AnalyzePattern(LogEntry logEntry)
        {
            var result = "Normal activity";
            var isAnomaly = false;
            var isThreat = false;

            var message = logEntry.Message?.ToLower() ?? "";

            // Phân tích login patterns
            if (message.Contains("login") || message.Contains("authentication"))
            {
                var loginAnalysis = await AnalyzeLoginPattern(logEntry);
                result = loginAnalysis.Result;
                isAnomaly = loginAnalysis.IsAnomaly;
                isThreat = loginAnalysis.IsThreat;
            }

            // Phân tích error patterns
            else if (message.Contains("error") || message.Contains("exception"))
            {
                var errorAnalysis = await AnalyzeErrorPattern(logEntry);
                result = errorAnalysis.Result;
                isAnomaly = errorAnalysis.IsAnomaly;
                isThreat = errorAnalysis.IsThreat;
            }

            // Phân tích access patterns
            else if (message.Contains("access") || message.Contains("request"))
            {
                var accessAnalysis = await AnalyzeAccessPattern(logEntry);
                result = accessAnalysis.Result;
                isAnomaly = accessAnalysis.IsAnomaly;
                isThreat = accessAnalysis.IsThreat;
            }

            return (result, isAnomaly, isThreat);
        }

        private async Task<(string Result, bool IsAnomaly, bool IsThreat)> AnalyzeLoginPattern(LogEntry logEntry)
        {
            var isAnomaly = false;
            var isThreat = false;
            var result = "Normal login activity";

            // Kiểm tra failed login attempts
            if (logEntry.Message?.ToLower().Contains("failed") == true)
            {
                // Đếm số lần failed login từ cùng IP
                var failedCount = await _context.LogEntries
                    .Where(l => l.IpAddress == logEntry.IpAddress)
                    .Where(l => l.Message.ToLower().Contains("failed"))
                    .Where(l => l.Timestamp >= logEntry.Timestamp.AddMinutes(-10))
                    .CountAsync();

                if (failedCount > 5)
                {
                    isThreat = true;
                    result = "Potential brute force attack detected";
                }
                else if (failedCount > 3)
                {
                    isAnomaly = true;
                    result = "Multiple failed login attempts";
                }
            }

            return (result, isAnomaly, isThreat);
        }

        private async Task<(string Result, bool IsAnomaly, bool IsThreat)> AnalyzeErrorPattern(LogEntry logEntry)
        {
            var isAnomaly = false;
            var isThreat = false;
            var result = "Normal error activity";

            var message = logEntry.Message?.ToLower() ?? "";

            // Kiểm tra SQL injection attempts
            if (message.Contains("sql") || message.Contains("injection"))
            {
                isThreat = true;
                result = "Potential SQL injection attempt";
            }

            // Kiểm tra XSS attempts
            else if (message.Contains("script") || message.Contains("xss"))
            {
                isThreat = true;
                result = "Potential XSS attempt";
            }

            // Kiểm tra error frequency
            else
            {
                var errorCount = await _context.LogEntries
                    .Where(l => l.LogLevelTypeId == logEntry.LogLevelTypeId)
                    .Where(l => l.Timestamp >= logEntry.Timestamp.AddMinutes(-5))
                    .CountAsync();

                if (errorCount > 10)
                {
                    isAnomaly = true;
                    result = "High error frequency detected";
                }
            }

            return (result, isAnomaly, isThreat);
        }

        private async Task<(string Result, bool IsAnomaly, bool IsThreat)> AnalyzeAccessPattern(LogEntry logEntry)
        {
            var isAnomaly = false;
            var isThreat = false;
            var result = "Normal access activity";

            // Kiểm tra access từ IP lạ
            if (!string.IsNullOrEmpty(logEntry.IpAddress))
            {
                var ipAccessCount = await _context.LogEntries
                    .Where(l => l.IpAddress == logEntry.IpAddress)
                    .Where(l => l.Timestamp >= logEntry.Timestamp.AddHours(-1))
                    .CountAsync();

                if (ipAccessCount > 100)
                {
                    isAnomaly = true;
                    result = "High access frequency from IP";
                }
            }

            return (result, isAnomaly, isThreat);
        }

        private async Task<List<LogAnalysis>> DetectLoginAnomaliesAsync(DateTime from, DateTime to)
        {
            var anomalies = new List<LogAnalysis>();

            // Tìm IP có nhiều failed login
            var suspiciousIPs = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .Where(l => l.Message.ToLower().Contains("failed"))
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() > 5)
                .Select(g => g.Key)
                .ToListAsync();

            foreach (var ip in suspiciousIPs)
            {
                var logEntries = await _context.LogEntries
                    .Where(l => l.IpAddress == ip)
                    .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                    .ToListAsync();

                foreach (var logEntry in logEntries)
                {
                    var analysis = await AnalyzeLogEntryAsync(logEntry);
                    if (analysis.IsAnomaly || analysis.IsThreat)
                        anomalies.Add(analysis);
                }
            }

            return anomalies;
        }

        private async Task<List<LogAnalysis>> DetectIPAnomaliesAsync(DateTime from, DateTime to)
        {
            var anomalies = new List<LogAnalysis>();

            // Tìm IP có access frequency cao
            var highFrequencyIPs = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() > 100)
                .Select(g => g.Key)
                .ToListAsync();

            foreach (var ip in highFrequencyIPs)
            {
                var logEntries = await _context.LogEntries
                    .Where(l => l.IpAddress == ip)
                    .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                    .ToListAsync();

                foreach (var logEntry in logEntries)
                {
                    var analysis = await AnalyzeLogEntryAsync(logEntry);
                    if (analysis.IsAnomaly || analysis.IsThreat)
                        anomalies.Add(analysis);
                }
            }

            return anomalies;
        }

        private async Task<List<LogAnalysis>> DetectErrorAnomaliesAsync(DateTime from, DateTime to)
        {
            var anomalies = new List<LogAnalysis>();

            // Tìm error patterns bất thường
            var errorLogs = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .Where(l => l.LogLevelType.Name.ToUpper() == "ERROR")
                .ToListAsync();

            foreach (var logEntry in errorLogs)
            {
                var analysis = await AnalyzeLogEntryAsync(logEntry);
                if (analysis.IsAnomaly || analysis.IsThreat)
                    anomalies.Add(analysis);
            }

            return anomalies;
        }

        private async Task<List<LogAnalysis>> AnalyzeBruteForceThreatsAsync(DateTime from, DateTime to)
        {
            var threats = new List<LogAnalysis>();

            // Tìm brute force patterns
            var bruteForceIPs = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .Where(l => l.Message.ToLower().Contains("failed"))
                .GroupBy(l => l.IpAddress)
                .Where(g => g.Count() > 10)
                .Select(g => g.Key)
                .ToListAsync();

            foreach (var ip in bruteForceIPs)
            {
                var logEntries = await _context.LogEntries
                    .Where(l => l.IpAddress == ip)
                    .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                    .ToListAsync();

                foreach (var logEntry in logEntries)
                {
                    var analysis = await AnalyzeLogEntryAsync(logEntry);
                    if (analysis.IsThreat)
                        threats.Add(analysis);
                }
            }

            return threats;
        }

        private async Task<List<LogAnalysis>> AnalyzeSQLInjectionThreatsAsync(DateTime from, DateTime to)
        {
            var threats = new List<LogAnalysis>();

            var sqlInjectionLogs = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .Where(l => l.Message.ToLower().Contains("sql") || l.Message.ToLower().Contains("injection"))
                .ToListAsync();

            foreach (var logEntry in sqlInjectionLogs)
            {
                var analysis = await AnalyzeLogEntryAsync(logEntry);
                if (analysis.IsThreat)
                    threats.Add(analysis);
            }

            return threats;
        }

        private async Task<List<LogAnalysis>> AnalyzeXSSThreatsAsync(DateTime from, DateTime to)
        {
            var threats = new List<LogAnalysis>();

            var xssLogs = await _context.LogEntries
                .Where(l => l.Timestamp >= from && l.Timestamp <= to)
                .Where(l => l.Message.ToLower().Contains("script") || l.Message.ToLower().Contains("xss"))
                .ToListAsync();

            foreach (var logEntry in xssLogs)
            {
                var analysis = await AnalyzeLogEntryAsync(logEntry);
                if (analysis.IsThreat)
                    threats.Add(analysis);
            }

            return threats;
        }

        private string GenerateRecommendations(LogEntry logEntry, LogAnalysis analysis)
        {
            var recommendations = new List<string>();

            if (analysis.IsThreat)
            {
                recommendations.Add("Immediate action required");
                recommendations.Add("Block suspicious IP address");
                recommendations.Add("Review security logs");
            }

            if (analysis.IsAnomaly)
            {
                recommendations.Add("Monitor activity closely");
                recommendations.Add("Review access patterns");
            }

            if (analysis.RiskLevel == "HIGH")
            {
                recommendations.Add("Escalate to security team");
                recommendations.Add("Implement additional monitoring");
            }

            return string.Join("; ", recommendations);
        }

        private int DetermineSeverityLevel(string? riskLevel)
        {
            return riskLevel?.ToUpper() switch
            {
                "HIGH" => 1, // Critical
                "MEDIUM" => 2, // High
                "LOW" => 3, // Medium
                _ => 4 // Low
            };
        }

        private int DetermineAlertType(string? analysisType)
        {
            return analysisType?.ToUpper() switch
            {
                "BRUTE_FORCE" => 1, // Security
                "SQL_INJECTION" => 1, // Security
                "XSS" => 1, // Security
                "ANOMALY" => 2, // System
                _ => 3 // General
            };
        }

        #endregion
    }
} 