using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TableManagement.API.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            // Request bilgilerini topla
            var requestInfo = await CaptureRequestAsync(context);

            // Response'u yakalamak için stream'i wrap et
            var originalResponseStream = context.Response.Body;
            using var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            try
            {
                await _next(context);
                stopwatch.Stop();

                // Response bilgilerini topla
                var responseInfo = await CaptureResponseAsync(context, responseStream);

                // Log işlemi
                await LogRequestAsync(context, requestInfo, responseInfo, stopwatch.ElapsedMilliseconds);

                // Original response stream'e kopyala
                responseStream.Position = 0;
                await responseStream.CopyToAsync(originalResponseStream);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Hata durumunda log
                _logger.LogError(ex, "Request failed for {Method} {Path} from {IP} in {ElapsedMs}ms",
                    context.Request.Method,
                    context.Request.Path,
                    GetClientIPAddress(context),
                    stopwatch.ElapsedMilliseconds);

                // Response stream'i geri yükle
                context.Response.Body = originalResponseStream;
                throw;
            }
            finally
            {
                context.Response.Body = originalResponseStream;
            }
        }

        private async Task<RequestInfo> CaptureRequestAsync(HttpContext context)
        {
            var request = context.Request;

            // Request body'yi oku (eğer varsa)
            string requestBody = string.Empty;
            if (request.ContentLength > 0 && ShouldLogRequestBody(request))
            {
                request.EnableBuffering();
                request.Body.Position = 0;

                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0;

                // Hassas bilgileri temizle
                requestBody = SanitizeRequestBody(requestBody);
            }

            return new RequestInfo
            {
                Method = request.Method,
                Path = request.Path,
                QueryString = request.QueryString.ToString(),
                UserAgent = request.Headers["User-Agent"].ToString(),
                IpAddress = GetClientIPAddress(context),
                UserId = context.User?.Identity?.Name,
                RequestBody = requestBody,
                ContentType = request.ContentType ?? string.Empty,
                Headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value))
            };
        }

        private async Task<ResponseInfo> CaptureResponseAsync(HttpContext context, MemoryStream responseStream)
        {
            string responseBody = string.Empty;

            if (responseStream.Length > 0 && ShouldLogResponseBody(context))
            {
                responseStream.Position = 0;
                using var reader = new StreamReader(responseStream, Encoding.UTF8, leaveOpen: true);
                responseBody = await reader.ReadToEndAsync();

                // Hassas bilgileri temizle
                responseBody = SanitizeResponseBody(responseBody);
            }

            return new ResponseInfo
            {
                StatusCode = context.Response.StatusCode,
                ResponseBody = responseBody,
                ContentType = context.Response.ContentType ?? string.Empty,
                Headers = context.Response.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value))
            };
        }

        private async Task LogRequestAsync(HttpContext context, RequestInfo requestInfo, ResponseInfo responseInfo, long elapsedMs)
        {
            // Structured logging with Serilog
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = context.TraceIdentifier,
                ["RequestPath"] = requestInfo.Path,
                ["RequestMethod"] = requestInfo.Method,
                ["IpAddress"] = requestInfo.IpAddress,
                ["UserAgent"] = requestInfo.UserAgent,
                ["UserId"] = requestInfo.UserId ?? "Anonymous",
                ["StatusCode"] = responseInfo.StatusCode,
                ["ResponseTime"] = elapsedMs,
                ["RequestBody"] = ShouldLogRequestBody(context.Request) ? requestInfo.RequestBody : "[Not Logged]",
                ["ResponseBody"] = ShouldLogResponseBody(context) ? responseInfo.ResponseBody : "[Not Logged]",
                ["IsSuspicious"] = IsSuspiciousRequest(requestInfo, responseInfo),
                ["ThreatType"] = GetThreatType(requestInfo, responseInfo)
            }))
            {
                var logLevel = GetLogLevel(responseInfo.StatusCode, elapsedMs);

                _logger.Log(logLevel,
                    "{Method} {Path} responded {StatusCode} in {ElapsedMs}ms from {IP} for user {UserId}",
                    requestInfo.Method,
                    requestInfo.Path,
                    responseInfo.StatusCode,
                    elapsedMs,
                    requestInfo.IpAddress,
                    requestInfo.UserId ?? "Anonymous");
            }
        }

        private bool ShouldLogRequestBody(HttpRequest request)
        {
            // POST, PUT, PATCH isteklerinde body logla
            if (!new[] { "POST", "PUT", "PATCH" }.Contains(request.Method.ToUpper()))
                return false;

            // Dosya upload'larını loglamasss
            if (request.ContentType?.Contains("multipart/form-data") == true)
                return false;

            // Çok büyük body'leri loglama
            if (request.ContentLength > 10000) // 10KB limit
                return false;

            return true;
        }

        private bool ShouldLogResponseBody(HttpContext context)
        {
            // Sadece JSON response'ları logla
            if (context.Response.ContentType?.Contains("application/json") != true)
                return false;

            // Sadece hata durumlarında veya debug modunda logla
            if (context.Response.StatusCode >= 400)
                return true;

            // Development environment'ta tüm response'ları logla
            var env = context.RequestServices.GetService<IWebHostEnvironment>();
            return env?.IsDevelopment() == true;
        }

        private string SanitizeRequestBody(string requestBody)
        {
            if (string.IsNullOrEmpty(requestBody))
                return requestBody;

            try
            {
                // JSON parse edip hassas alanları temizle
                var jsonDoc = JsonDocument.Parse(requestBody);
                var sanitized = SanitizeJsonElement(jsonDoc.RootElement);
                return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                // JSON değilse olduğu gibi döndür (maksimum 1000 karakter)
                return requestBody.Length > 1000 ? requestBody.Substring(0, 1000) + "..." : requestBody;
            }
        }

        private string SanitizeResponseBody(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
                return responseBody;

            try
            {
                var jsonDoc = JsonDocument.Parse(responseBody);
                var sanitized = SanitizeJsonElement(jsonDoc.RootElement);
                return JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = false });
            }
            catch
            {
                return responseBody.Length > 2000 ? responseBody.Substring(0, 2000) + "..." : responseBody;
            }
        }

        private object SanitizeJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        // Hassas alanları gizle
                        if (IsSensitiveField(property.Name))
                        {
                            obj[property.Name] = "***HIDDEN***";
                        }
                        else
                        {
                            obj[property.Name] = SanitizeJsonElement(property.Value);
                        }
                    }
                    return obj;

                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(SanitizeJsonElement).ToArray();

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.GetDecimal();

                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.ToString();
            }
        }

        private bool IsSensitiveField(string fieldName)
        {
            var sensitiveFields = new[]
            {
                "password", "passwd", "pwd", "secret", "token", "key", "auth",
                "authorization", "credential", "pin", "ssn", "creditcard",
                "cardnumber", "cvv", "cvc", "bankaccount", "iban"
            };

            return sensitiveFields.Any(field =>
                fieldName.ToLowerInvariant().Contains(field));
        }

        private bool IsSuspiciousRequest(RequestInfo requestInfo, ResponseInfo responseInfo)
        {
            // 4xx ve 5xx hata kodları şüpheli
            if (responseInfo.StatusCode >= 400)
                return true;

            // Çok uzun URL'ler şüpheli
            if (requestInfo.Path.Length > 500)
                return true;

            // Çok fazla parametre şüpheli
            if (requestInfo.QueryString.Length > 1000)
                return true;

            return false;
        }

        private string? GetThreatType(RequestInfo requestInfo, ResponseInfo responseInfo)
        {
            if (responseInfo.StatusCode == 403)
                return "Access Denied";

            if (responseInfo.StatusCode == 401)
                return "Unauthorized";

            if (responseInfo.StatusCode >= 500)
                return "Server Error";

            if (requestInfo.Path.Length > 500)
                return "Long URL";

            return null;
        }

        private LogLevel GetLogLevel(int statusCode, long elapsedMs)
        {
            // Server error -> Error
            if (statusCode >= 500)
                return LogLevel.Error;

            // Client error -> Warning
            if (statusCode >= 400)
                return LogLevel.Warning;

            // Yavaş istekler -> Warning
            if (elapsedMs > 5000) // 5 saniye
                return LogLevel.Warning;

            // Normal istekler -> Information
            return LogLevel.Information;
        }

        private string GetClientIPAddress(HttpContext context)
        {
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                return xForwardedFor.Split(',')[0].Trim();
            }

            var xRealIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIP))
            {
                return xRealIP;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }
    }

    // Helper classes
    public class RequestInfo
    {
        public string Method { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string QueryString { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string RequestBody { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
    }

    public class ResponseInfo
    {
        public int StatusCode { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new();
    }
}