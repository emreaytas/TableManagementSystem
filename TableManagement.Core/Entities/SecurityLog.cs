namespace TableManagement.Core.Entities
{
    public class SecurityLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string ThreatType { get; set; } = string.Empty; // SqlInjection, XSS, etc.
        public string RequestPath { get; set; } = string.Empty;
        public string RequestMethod { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string? AttackPayload { get; set; }
        public string? UserId { get; set; }
        public bool IsBlocked { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}