using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure.Data;

namespace TableManagement.Application.Services
{
    public class SecurityLogService : ISecurityLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SecurityLogService> _logger;

        public SecurityLogService(ApplicationDbContext context, ILogger<SecurityLogService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<SecurityLog>> GetSecurityLogsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.SecurityLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(x => x.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(x => x.Timestamp <= endDate.Value);

            return await query
                .OrderByDescending(x => x.Timestamp)
                .Take(1000) // Limit to prevent memory issues
                .ToListAsync();
        }

        public async Task<IEnumerable<SecurityLog>> GetSecurityLogsByIPAsync(string ipAddress)
        {
            return await _context.SecurityLogs
                .Where(x => x.IpAddress == ipAddress)
                .OrderByDescending(x => x.Timestamp)
                .Take(100)
                .ToListAsync();
        }

        public async Task<IEnumerable<SecurityLog>> GetSecurityLogsByThreatTypeAsync(string threatType)
        {
            return await _context.SecurityLogs
                .Where(x => x.ThreatType == threatType)
                .OrderByDescending(x => x.Timestamp)
                .Take(100)
                .ToListAsync();
        }

        public async Task<SecurityLog> CreateSecurityLogAsync(SecurityLog securityLog)
        {
            _context.SecurityLogs.Add(securityLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Security log created: {ThreatType} from {IP}",
                securityLog.ThreatType, securityLog.IpAddress);

            return securityLog;
        }

        public async Task<bool> IsIPBlockedAsync(string ipAddress)
        {
            var recentAttacks = await _context.SecurityLogs
                .Where(x => x.IpAddress == ipAddress &&
                           x.IsBlocked &&
                           x.Timestamp >= DateTime.UtcNow.AddHours(-24))
                .CountAsync();

            return recentAttacks >= 5; // 24 saat içinde 5+ engellenen istek varsa IP'yi blokla
        }

        public async Task BlockIPAsync(string ipAddress, string reason)
        {
            var blockLog = new SecurityLog
            {
                Timestamp = DateTime.UtcNow,
                IpAddress = ipAddress,
                ThreatType = "IP Blocked",
                RequestPath = "/",
                RequestMethod = "BLOCK",
                UserAgent = "System",
                AttackPayload = reason,
                IsBlocked = true,
                AdditionalInfo = $"IP blocked due to: {reason}"
            };

            await CreateSecurityLogAsync(blockLog);
            _logger.LogWarning("IP {IP} has been blocked: {Reason}", ipAddress, reason);
        }

        public async Task<Dictionary<string, int>> GetThreatStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.SecurityLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(x => x.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(x => x.Timestamp <= endDate.Value);

            return await query
                .GroupBy(x => x.ThreatType)
                .Select(g => new { ThreatType = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ThreatType, x => x.Count);
        }

        public async Task<IEnumerable<string>> GetTopAttackingIPsAsync(int count = 10)
        {
            return await _context.SecurityLogs
                .Where(x => x.IsBlocked && x.Timestamp >= DateTime.UtcNow.AddDays(-7))
                .GroupBy(x => x.IpAddress)
                .OrderByDescending(g => g.Count())
                .Take(count)
                .Select(g => g.Key)
                .ToListAsync();
        }
    }
}