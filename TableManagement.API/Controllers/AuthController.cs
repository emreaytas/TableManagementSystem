using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;
using TableManagement.Core.DTOs.Requests;

namespace TableManagement.API.Controllers
{
    /// <summary>
    /// Kullanıcı kimlik doğrulama işlemleri
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly ILoggingService _loggingService;
        private readonly UserManager<TableManagement.Core.Entities.User> _userManager;
        public AuthController(IAuthService authService, ILogger<AuthController> logger,ILoggingService loggingService, UserManager<TableManagement.Core.Entities.User> userManager)
        {
            _loggingService = loggingService;
            _authService = authService;
            _logger = logger;
            _userManager = userManager;
        }

        /// <summary>
        /// Yeni kullanıcı kaydı oluşturur
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                await _loggingService.LogRequestAsync(
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    HttpContext.Request.Path,
                    HttpContext.Request.Method,
                    request.ToString(),
                    HttpContext.Request.QueryString.ToString());

                return BadRequest(ModelState);
            }

            var result = await _authService.RegisterAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Kullanıcı giriş işlemi
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                await _loggingService.LogRequestAsync(
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    HttpContext.Request.Path,
                    HttpContext.Request.Method,
                    request.ToString(),
                    HttpContext.Request.QueryString.ToString());

                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                await _loggingService.LogRequestAsync(
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    HttpContext.Request.Path,
                    HttpContext.Request.Method,
                    request.ToString(),
                    HttpContext.Request.QueryString.ToString());
                return Ok(result);
            }

            await _loggingService.LogResponseAsync(
                HttpContext.Request.Path,
                HttpContext.Request.Method,
                result.ToString(),
                result.Success ? "200" : "401");

            return Unauthorized(result);
        
        }

        /// <summary>
        /// Email doğrulama (GET - Email'den gelen link)
        /// </summary>
        [HttpGet("confirm-email")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public async Task<IActionResult> ConfirmEmailGet([FromQuery] string token, [FromQuery] string email)
        {
            try
            {
                _logger.LogInformation($"Email confirmation attempt for email: {email}");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Token or email is missing");

                    var errorUrl = HttpContext.RequestServices
                        .GetService<IConfiguration>()?
                        .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                    return Redirect($"{errorUrl}/auth?error=invalid-confirmation-link");
                }

                // Email doğrulama işlemini gerçekleştir
                var result = await _authService.ConfirmEmailAsync(token, email);

                if (result.Success)
                {
                    _logger.LogInformation($"Email confirmation successful for: {email}");

                    // Başarılı doğrulama sonrası doğrudan giriş sayfasına yönlendir
                    var frontendUrl = HttpContext.RequestServices
                        .GetService<IConfiguration>()?
                        .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                    // Doğrudan auth sayfasına yönlendir
                    var redirectUrl = $"{frontendUrl}/auth?verified=true";
                    _logger.LogInformation($"Redirecting to: {redirectUrl}");

                    return Redirect(redirectUrl);
                }

                // Hata durumunda da giriş sayfasına yönlendir ama hata mesajıyla
                var errorFrontendUrl = HttpContext.RequestServices
                    .GetService<IConfiguration>()?
                    .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                var errorRedirectUrl = $"{errorFrontendUrl}/auth?verified=false";
                _logger.LogWarning($"Email confirmation failed, redirecting to: {errorRedirectUrl}");

                return Redirect(errorRedirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email confirmation");

                var errorUrl = HttpContext.RequestServices
                    .GetService<IConfiguration>()?
                    .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                return Redirect($"{errorUrl}/auth?verified=false");
            }
        }

        /// <summary>
        /// Email doğrulama (POST - API çağrısı)
        /// </summary>
        [HttpPost("confirm-email")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmEmailPost([FromBody] ConfirmEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.ConfirmEmailAsync(request.Token, request.Email);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Email doğrulama tekrar gönderme
        /// </summary>
        [HttpPost("resend-email-confirmation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ResendEmailConfirmation([FromBody] ResendEmailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.ResendEmailConfirmationAsync(request.Email);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Bir kullanıcı adının sistemde kayıtlı olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="username">Kontrol edilecek kullanıcı adı.</param>
        /// <returns>Kullanıcı adının alınıp alınmadığını belirten bir boolean.</returns>
        [HttpGet("check-username/{username}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckUsernameExists(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            // user null değilse, kullanıcı adı alınmış (taken) demektir.
            return Ok(new { isTaken = user != null });
        }

        /// <summary>
        /// Bir e-posta adresinin sistemde kayıtlı olup olmadığını kontrol eder.
        /// </summary>
        /// <param name="email">Kontrol edilecek e-posta adresi.</param>
        /// <returns>E-postanın alınıp alınmadığını belirten bir boolean.</returns>
        [HttpGet("check-email/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckEmailExists(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            // user null değilse, e-posta alınmış (taken) demektir.
            return Ok(new { isTaken = user != null });
        }



        /// <summary>
        /// Test endpoint - Email doğrulama durumunu kontrol et
        /// </summary>

        [HttpGet("check-email-status/{email}")]
        public async Task<IActionResult> CheckEmailStatus(string email)
        {
            try
            {
                // Bu endpoint sadece development için - production'da kaldırılmalı
                var userManager = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TableManagement.Core.Entities.User>>();
                var user = await userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    return NotFound(new { message = "Kullanıcı bulunamadı" });
                }

                return Ok(new
                {
                    email = user.Email,
                    emailConfirmed = user.EmailConfirmed,
                    isEmailConfirmed = user.IsEmailConfirmed,
                    userName = user.UserName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email status");
                return StatusCode(500, new { message = "Bir hata oluştu" });
            }
        }
    }
}