using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.DTOs.Alerts
{
    public class UnblockIPRequest
    {
        [Required(ErrorMessage = "IP address is required")]
        public string IpAddress { get; set; } = string.Empty;
    }
} 