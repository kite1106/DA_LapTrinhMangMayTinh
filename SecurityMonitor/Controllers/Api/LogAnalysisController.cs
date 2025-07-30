using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecurityMonitor.Services.Interfaces;
using SecurityMonitor.Models;
using SecurityMonitor.Data;
using Microsoft.EntityFrameworkCore;

namespace SecurityMonitor.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LogAnalysisController : ControllerBase
    {
        private readonly ILogAnalysisService _logAnalysisService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LogAnalysisController> _logger;

        public LogAnalysisController(
            ILogAnalysisService logAnalysisService,
            ApplicationDbContext context,
            ILogger<LogAnalysisController> logger)
        {
            _logAnalysisService = logAnalysisService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("analyze-single")]
        public async Task<IActionResult> AnalyzeSingleLog([FromBody] LogEntryRequest request)
        {
            try
            {
                // Tạo log entry từ request
                var logEntry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    LogSourceId = request.LogSourceId,
                    LogLevelTypeId = request.LogLevelTypeId,
                    Message = request.Message,
                    Details = request.Details,
                    IpAddress = request.IpAddress,
                    UserId = request.UserId,
                    WasSuccessful = request.WasSuccessful
                };

                // Lưu log entry
                _context.LogEntries.Add(logEntry);
                await _context.SaveChangesAsync();

                // Phân tích log entry
                var analysis = await _logAnalysisService.AnalyzeLogEntryAsync(logEntry);

                // Tạo alert nếu cần
                var alert = await _logAnalysisService.CreateAlertFromAnalysisAsync(analysis);

                return Ok(new
                {
                    success = true,
                    logEntry = new
                    {
                        id = logEntry.Id,
                        message = logEntry.Message,
                        timestamp = logEntry.Timestamp
                    },
                    analysis = new
                    {
                        id = analysis.Id,
                        analysisType = analysis.AnalysisType,
                        analysisResult = analysis.AnalysisResult,
                        riskLevel = analysis.RiskLevel,
                        confidenceScore = analysis.ConfidenceScore,
                        isAnomaly = analysis.IsAnomaly,
                        isThreat = analysis.IsThreat,
                        recommendations = analysis.Recommendations
                    },
                    alert = alert != null ? new
                    {
                        id = alert.Id,
                        title = alert.Title,
                        description = alert.Description,
                        severity = alert.SeverityLevelId
                    } : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing single log");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("analyze-batch")]
        public async Task<IActionResult> AnalyzeBatchLogs([FromBody] List<LogEntryRequest> requests)
        {
            try
            {
                var logEntries = new List<LogEntry>();
                var analyses = new List<object>();

                foreach (var request in requests)
                {
                    var logEntry = new LogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        LogSourceId = request.LogSourceId,
                        LogLevelTypeId = request.LogLevelTypeId,
                        Message = request.Message,
                        Details = request.Details,
                        IpAddress = request.IpAddress,
                        UserId = request.UserId,
                        WasSuccessful = request.WasSuccessful
                    };

                    logEntries.Add(logEntry);
                }

                // Lưu tất cả log entries
                _context.LogEntries.AddRange(logEntries);
                await _context.SaveChangesAsync();

                // Phân tích batch
                var batchAnalyses = await _logAnalysisService.AnalyzeLogEntriesAsync(logEntries);

                // Tạo alerts
                var alerts = new List<object>();
                foreach (var analysis in batchAnalyses)
                {
                    var alert = await _logAnalysisService.CreateAlertFromAnalysisAsync(analysis);
                    if (alert != null)
                    {
                        alerts.Add(new
                        {
                            id = alert.Id,
                            title = alert.Title,
                            description = alert.Description,
                            severity = alert.SeverityLevelId
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    totalLogs = logEntries.Count,
                    totalAnalyses = batchAnalyses.Count,
                    anomalies = batchAnalyses.Count(a => a.IsAnomaly),
                    threats = batchAnalyses.Count(a => a.IsThreat),
                    alerts = alerts.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing batch logs");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("detect-anomalies")]
        public async Task<IActionResult> DetectAnomalies([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
                var toDate = to ?? DateTime.UtcNow;

                var anomalies = await _logAnalysisService.DetectAnomaliesAsync(fromDate, toDate);

                return Ok(new
                {
                    success = true,
                    from = fromDate,
                    to = toDate,
                    totalAnomalies = anomalies.Count,
                    anomalies = anomalies.Select(a => new
                    {
                        id = a.Id,
                        analysisType = a.AnalysisType,
                        analysisResult = a.AnalysisResult,
                        riskLevel = a.RiskLevel,
                        isAnomaly = a.IsAnomaly,
                        isThreat = a.IsThreat,
                        recommendations = a.Recommendations
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting anomalies");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("analyze-threats")]
        public async Task<IActionResult> AnalyzeThreats([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
                var toDate = to ?? DateTime.UtcNow;

                var threats = await _logAnalysisService.AnalyzeThreatsAsync(fromDate, toDate);

                return Ok(new
                {
                    success = true,
                    from = fromDate,
                    to = toDate,
                    totalThreats = threats.Count,
                    threats = threats.Select(t => new
                    {
                        id = t.Id,
                        analysisType = t.AnalysisType,
                        analysisResult = t.AnalysisResult,
                        riskLevel = t.RiskLevel,
                        isThreat = t.IsThreat,
                        recommendations = t.Recommendations
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing threats");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetAnalysisStats([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            try
            {
                var fromDate = from ?? DateTime.UtcNow.AddDays(-7);
                var toDate = to ?? DateTime.UtcNow;

                var stats = await _logAnalysisService.GetAnalysisStatsAsync(fromDate, toDate);

                return Ok(new
                {
                    success = true,
                    from = fromDate,
                    to = toDate,
                    stats = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting analysis stats");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class LogEntryRequest
    {
        public int LogSourceId { get; set; }
        public int LogLevelTypeId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public string? UserId { get; set; }
        public bool WasSuccessful { get; set; } = true;
    }
} 