using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TableManagement.Core.Entities;
using TableManagement.Infrastructure.Data;

namespace TableManagement.API.Middleware
{
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;

        // SQL Injection kalıpları
        private readonly List<Regex> _sqlInjectionPatterns = new()
        {
            new Regex(@"(\bunion\b.*\bselect\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bselect\b.*\bfrom\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\binsert\b.*\binto\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bupdate\b.*\bset\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bdelete\b.*\bfrom\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bdrop\b.*\btable\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\btruncate\b.*\btable\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bexec\b.*\bsp_)", RegexOptions.IgnoreCase),
            new Regex(@"(\bexecute\b.*\bsp_)", RegexOptions.IgnoreCase),
            new Regex(@"(\';.*--)", RegexOptions.IgnoreCase),
            new Regex(@"(\bor\b.*\b1\s*=\s*1\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\band\b.*\b1\s*=\s*1\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\b1\s*=\s*1\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\'\s*or\s*\'\w*\'\s*=\s*\')", RegexOptions.IgnoreCase),
            new Regex(@"(\bxp_cmdshell\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bsp_configure\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bhaving\b.*\b1\s*=\s*1\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bgroup\s+by\b.*\bhaving\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bcast\(.*\bas\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bconvert\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bchar\(\d+\))", RegexOptions.IgnoreCase),
            new Regex(@"(\bhex\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bunhex\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bascii\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bordinal\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bsubstring\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bwaitfor\s+delay\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\bbenchmark\()", RegexOptions.IgnoreCase),
            new Regex(@"(\bsleep\(\d+\))", RegexOptions.IgnoreCase),
            new Regex(@"(\bpg_sleep\(\d+\))", RegexOptions.IgnoreCase),
            new Regex(@"(/\*.*\*/)", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            new Regex(@"(\-\-.*)", RegexOptions.IgnoreCase),
            new Regex(@"(\#.*)", RegexOptions.IgnoreCase),
            new Regex(@"(\bload_file\()", RegexOptions.IgnoreCase),
            new Regex(@"(\binto\s+outfile\b)", RegexOptions.IgnoreCase),
            new Regex(@"(\binto\s+dumpfile\b)", RegexOptions.IgnoreCase)
        };

        // XSS kalıpları
        private readonly List<Regex> _xssPatterns = new()
        {
            new Regex(@"<script[^>]*>.*?</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            new Regex(@"javascript:", RegexOptions.IgnoreCase),
            new Regex(@"on\w+\s*=", RegexOptions.IgnoreCase),
            new Regex(@"<iframe[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<object[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<embed[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<link[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"<meta[^>]*>", RegexOptions.IgnoreCase),
            new Regex(@"vbscript:", RegexOptions.IgnoreCase),
            new Regex(@"data:\s*text/html", RegexOptions.IgnoreCase)
        };

        // Güvenilir IP adresleri (opsiyonel whitelist)
        private readonly HashSet<string> _trustedIPs = new()
        {
            "127.0.0.1", // localhost
            "::1"        // localhost IPv6
        };

        public SecurityMiddleware(RequestDelegate next, ILogger<SecurityMiddleware> logger, IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIP = GetClientIPAddress(context);

            // Güvenilir IP kontrolü
            if (_trustedIPs.Contains(clientIP))
            {
                await _next(context);
                return;
            }

            // Request body'yi okuma
            context.Request.EnableBuffering();
            var requestBody = await ReadRequestBodyAsync(context.Request);

            // Query string parametrelerini kontrol et
            var queryString = context.Request.QueryString.ToString();

            // Form data kontrolü
            var formData = await ReadFormDataAsync(context.Request);

            // URL path kontrolü
            var requestPath = context.Request.Path.ToString();

            // Güvenlik kontrolü
            var threatDetected = await DetectThreatsAsync(requestBody, queryString, formData, requestPath, clientIP, context);

            if (threatDetected.IsBlocked)
            {
                // Saldırı tespit edildi, engelle
                _logger.LogWarning("Security threat detected from IP {IP}: {ThreatType}",
                    clientIP, threatDetected.ThreatType);

                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Forbidden: Security violation detected");
                return;
            }

            // Request body pozisyonunu sıfırla
            context.Request.Body.Position = 0;

            await _next(context);
        }

        private async Task<(bool IsBlocked, string ThreatType, string Payload)> DetectThreatsAsync(
            string requestBody, string queryString, string formData, string requestPath, string clientIP, HttpContext context)
        {
            var allContent = $"{requestBody} {queryString} {formData} {requestPath}";

            // SQL Injection kontrolü
            foreach (var pattern in _sqlInjectionPatterns)
            {
                var match = pattern.Match(allContent);
                if (match.Success)
                {
                    await LogSecurityThreatAsync(clientIP, "SQL Injection", requestPath,
                        context.Request.Method, context.Request.Headers["User-Agent"],
                        match.Value, context.User?.Identity?.Name, true, allContent);

                    return (true, "SQL Injection", match.Value);
                }
            }

            // XSS kontrolü
            foreach (var pattern in _xssPatterns)
            {
                var match = pattern.Match(allContent);
                if (match.Success)
                {
                    await LogSecurityThreatAsync(clientIP, "XSS", requestPath,
                        context.Request.Method, context.Request.Headers["User-Agent"],
                        match.Value, context.User?.Identity?.Name, true, allContent);

                    return (true, "XSS", match.Value);
                }
            }

            // Şüpheli aktivite kontrolü (çok fazla özel karakter)
            var specialCharCount = allContent.Count(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c));
            var totalLength = allContent.Length;

            if (totalLength > 0 && (double)specialCharCount / totalLength > 0.3)
            {
                await LogSecurityThreatAsync(clientIP, "Suspicious Pattern", requestPath,
                    context.Request.Method, context.Request.Headers["User-Agent"],
                    "High special character ratio", context.User?.Identity?.Name, false, allContent);
            }

            return (false, string.Empty, string.Empty);
        }

        private async Task LogSecurityThreatAsync(string ipAddress, string threatType, string requestPath,
            string requestMethod, string userAgent, string attackPayload, string? userId, bool isBlocked, string additionalInfo)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var securityLog = new SecurityLog
                {
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    ThreatType = threatType,
                    RequestPath = requestPath,
                    RequestMethod = requestMethod,
                    UserAgent = userAgent ?? "Unknown",
                    AttackPayload = attackPayload,
                    UserId = userId,
                    IsBlocked = isBlocked,
                    AdditionalInfo = additionalInfo
                };

                dbContext.SecurityLogs.Add(securityLog);
                await dbContext.SaveChangesAsync();

                // Serilog ile de logla
                _logger.LogWarning("Security threat: {ThreatType} from {IP} on {Path}. Payload: {Payload}",
                    threatType, ipAddress, requestPath, attackPayload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving security log");
            }
        }

        private string GetClientIPAddress(HttpContext context)
        {
            // X-Forwarded-For header'ı kontrol et (proxy/load balancer arkasında)
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            // X-Real-IP header'ı kontrol et
            var xRealIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIP))
            {
                return xRealIP;
            }

            // Remote IP address
            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            try
            {
                if (request.ContentLength == 0 || request.ContentLength == null)
                    return string.Empty;

                request.Body.Position = 0;
                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                request.Body.Position = 0;
                return body;
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task<string> ReadFormDataAsync(HttpRequest request)
        {
            try
            {
                if (request.HasFormContentType && request.Form != null)
                {
                    var formData = new StringBuilder();
                    foreach (var key in request.Form.Keys)
                    {
                        formData.Append($"{key}={request.Form[key]} ");
                    }
                    return formData.ToString();
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}