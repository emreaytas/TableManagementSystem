using TableManagement.Core.Entities;

namespace TableManagement.Core.Interfaces
{
    public interface ISecurityLogService
    {
        Task<IEnumerable<SecurityLog>> GetSecurityLogsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<SecurityLog>> GetSecurityLogsByIPAsync(string ipAddress);
        Task<IEnumerable<SecurityLog>> GetSecurityLogsByThreatTypeAsync(string threatType);
        Task<SecurityLog> CreateSecurityLogAsync(SecurityLog securityLog);
        Task<bool> IsIPBlockedAsync(string ipAddress);
        Task BlockIPAsync(string ipAddress, string reason);
        Task<Dictionary<string, int>> GetThreatStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<string>> GetTopAttackingIPsAsync(int count = 10);
    }
}