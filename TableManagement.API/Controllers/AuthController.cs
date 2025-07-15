using Microsoft.AspNetCore.Mvc;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;
using System.Web;
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

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
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
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(request);

            if (result.Success)
            {
                return Ok(result);
            }

            return Unauthorized(result);
        }

        /// <summary>
        /// Email doğrulama (GET - Email'den gelen link)
        /// </summary>
        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string email)
        {
            _logger.LogInformation($"Confirm email GET request received - Email: {email}");
            _logger.LogInformation($"Token length: {token?.Length}");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Missing token or email in confirm email request");
                return BadRequest(new
                {
                    success = false,
                    message = "Geçersiz token veya email adresi"
                });
            }

            try
            {
                // URL decode işlemi
                var decodedToken = HttpUtility.UrlDecode(token);
                var decodedEmail = HttpUtility.UrlDecode(email);

                _logger.LogInformation($"Decoded email: {decodedEmail}");

                var result = await _authService.ConfirmEmailAsync(decodedToken, decodedEmail);

                if (result.Success)
                {
                    // Frontend'e redirect yapabilirsiniz
                    var frontendUrl = HttpContext.RequestServices
                        .GetService<IConfiguration>()?
                        .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                    var redirectUrl = $"{frontendUrl}/email-confirmed?success=true&message={HttpUtility.UrlEncode(result.Message)}";
                    _logger.LogInformation($"Redirecting to: {redirectUrl}");

                    return Redirect(redirectUrl);
                }

                // Hata durumunda da frontend'e yönlendir
                var errorUrl = HttpContext.RequestServices
                    .GetService<IConfiguration>()?
                    .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                var errorRedirectUrl = $"{errorUrl}/email-confirmed?success=false&message={HttpUtility.UrlEncode(result.Message)}";
                _logger.LogWarning($"Email confirmation failed, redirecting to: {errorRedirectUrl}");

                return Redirect(errorRedirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email confirmation");

                var errorUrl = HttpContext.RequestServices
                    .GetService<IConfiguration>()?
                    .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                return Redirect($"{errorUrl}/email-confirmed?success=false&message={HttpUtility.UrlEncode("Bir hata oluştu")}");
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