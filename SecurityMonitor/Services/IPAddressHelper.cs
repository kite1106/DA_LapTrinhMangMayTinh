using System.Net;
using Microsoft.AspNetCore.Http;

namespace SecurityMonitor.Services
{
    public static class IPAddressHelper
    {
        public static string GetClientIPAddress(HttpContext context)
        {
            // Check for X-Forwarded-For header first (used by proxies)
            string? ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(ip))
            {
                // X-Forwarded-For can contain multiple IPs (proxy chain)
                // First one is the original client
                return ip.Split(',')[0].Trim();
            }

            // Check for X-Real-IP header (used by some proxies)
            ip = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }

            // Get the remote IP address
            ip = context.Connection.RemoteIpAddress?.ToString();
            
            // Convert IPv6 loopback to IPv4 loopback for consistency
            if (ip == "::1")
            {
                ip = "127.0.0.1";
            }

            // If we're dealing with an IPv6 address that's actually an IPv4 mapped address
            // convert it to the IPv4 representation
            if (IPAddress.TryParse(ip, out var address) && 
                address.IsIPv4MappedToIPv6)
            {
                ip = address.MapToIPv4().ToString();
            }

            return ip ?? "Unknown";
        }
    }
}
