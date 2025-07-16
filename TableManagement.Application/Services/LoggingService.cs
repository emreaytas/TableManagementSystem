using Microsoft.Extensions.Logging; // Yerleşik ILogger'ı da kullanabiliriz
using System;
using System.Threading.Tasks;
using TableManagement.Core.Entities;
using TableManagement.Infrastructure.Data;

namespace TableManagement.Application.Services
{


    public class LoggingService : ILoggingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoggingService> _logger; 

        public LoggingService(ApplicationDbContext context, ILogger<LoggingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogRequestAsync(
            string ipAddress,
            string requestPath,
            string httpMethod,
            string? requestBody = null,
            string? queryString = null)
        {
            try
            {
                var logEntry = new LogEntry
                {
                    LogLevel = "Information",
                    IpAddress = ipAddress,
                    RequestPath = requestPath,
                    HttpMethod = httpMethod,
                    RequestBody = requestBody,
                    QueryString = queryString,
                    Message = $"Request received: {httpMethod} {requestPath}"
                };

                _context.LogEntries.Add(logEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging request to database.");
                // Hata durumunda loglamanın uygulamanın çökmesine neden olmaması için yakalayın.
            }
        }

        public async Task LogResponseAsync(
            string requestPath,
            string httpMethod,
            string? responseBody = null,
            string? statusCode = null)
        {
            try
            {
                // Bu örnekte yanıtı yeni bir log girdisi olarak kaydediyoruz.
                // İleri düzeyde, ilişkili istek logunu bulup güncellemek daha mantıklı olabilir.
                var logEntry = new LogEntry
                {
                    LogLevel = "Information",
                    RequestPath = requestPath,
                    HttpMethod = httpMethod,
                    ResponseBody = responseBody,
                    StatusCode = statusCode,
                    Message = $"Response sent: {httpMethod} {requestPath} with status {statusCode}"
                };

                _context.LogEntries.Add(logEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging response to database.");
            }
        }

        public async Task LogErrorAsync(
            string message,
            Exception? exception = null,
            string? requestPath = null,
            string? httpMethod = null,
            string? ipAddress = null)
        {
            try
            {
                var logEntry = new LogEntry
                {
                    LogLevel = "Error",
                    Message = message,
                    StackTrace = exception?.ToString(),
                    RequestPath = requestPath,
                    HttpMethod = httpMethod,
                    IpAddress = ipAddress
                };

                _context.LogEntries.Add(logEntry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while logging error to database.");
            }
        }
    }
}
