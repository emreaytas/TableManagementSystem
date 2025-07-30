using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly IConfiguration _configuration;
        public AuthController(
            IAuthService authService,
            ILogger<AuthController> logger,
            ILoggingService loggingService,
            UserManager<TableManagement.Core.Entities.User> userManager, IConfiguration configuration)
        {
            _loggingService = loggingService;
            _authService = authService;
            _logger = logger;
            _configuration = configuration;
            _userManager = userManager;
        }


        [HttpGet("allusers")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _userManager.Users.ToListAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all users");
                return StatusCode(500, new { message = "Sunucu hatası. Lütfen daha sonra tekrar deneyin." });
            }
        }

        /// <summary>
        /// Yeni kullanıcı kaydı oluşturur
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation($"Register attempt for username: {request?.UserName}, email: {request?.Email}");

                // Input validation
                if (request == null)
                {
                    _logger.LogWarning("Register request is null");
                    return BadRequest(new { success = false, message = "Geçersiz kayıt verisi." });
                }

                // Log the incoming request for debugging
                _logger.LogInformation($"Register request data: FirstName='{request.FirstName}', LastName='{request.LastName}', Email='{request.Email}', UserName='{request.UserName}', PasswordLength={request.Password?.Length ?? 0}, ConfirmPasswordLength={request.ConfirmPassword?.Length ?? 0}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Register ModelState is invalid");

                    // Log specific validation errors
                    foreach (var error in ModelState)
                    {
                        foreach (var subError in error.Value.Errors)
                        {
                            _logger.LogWarning($"Validation error for {error.Key}: {subError.ErrorMessage}");
                        }
                    }

                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return BadRequest(new
                    {
                        success = false,
                        message = "Form verilerinde hata bulundu.",
                        errors = errors
                    });
                }

                var result = await _authService.RegisterAsync(request);

                _logger.LogInformation($"Register result for {request.UserName}: Success={result.Success}, Message={result.Message}");

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error in Register controller for user: {request?.UserName}");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Sunucu hatası. Lütfen daha sonra tekrar deneyin."
                });
            }
        }




        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"Login attempt for username: {request?.UserName}");

                if (request == null)
                {
                    _logger.LogWarning("Login request is null");
                    return BadRequest(new { success = false, message = "Geçersiz giriş verisi." });
                }

                _logger.LogInformation($"Login request data: UserName='{request.UserName}', PasswordLength={request.Password?.Length ?? 0}");

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Login ModelState is invalid");

                    foreach (var error in ModelState)
                    {
                        foreach (var subError in error.Value.Errors)
                        {
                            _logger.LogWarning($"Validation error for {error.Key}: {subError.ErrorMessage}");
                        }
                    }

                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return BadRequest(new
                    {
                        success = false,
                        message = "Giriş verilerinde hata bulundu.",
                        errors = errors
                    });
                }

                var result = await _authService.LoginAsync(request);

                _logger.LogInformation($"Login result for {request.UserName}: Success={result.Success}, Message={result.Message}");

                if (result.Success)
                {
                    await _loggingService.LogRequestAsync(
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        HttpContext.Request.Path,
                        HttpContext.Request.Method,
                        $"User: {request.UserName}",
                        HttpContext.Request.QueryString.ToString());

                    return Ok(result);
                }

                await _loggingService.LogResponseAsync(
                    HttpContext.Request.Path,
                    HttpContext.Request.Method,
                    result.Message,
                    "401");

                return Unauthorized(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error in Login controller for user: {request?.UserName}");

                return StatusCode(500, new
                {
                    success = false,
                    message = "Sunucu hatası. Lütfen daha sonra tekrar deneyin."
                });
            }
        }



        [HttpGet("check-username/{username}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckUsername(string username)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    return Ok(new { isTaken = false, message = "Kullanıcı adı boş olamaz." });
                }

                var user = await _userManager.FindByNameAsync(username.Trim());
                var isTaken = user != null;

                return Ok(new { isTaken = isTaken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking username: {username}");
                return Ok(new { isTaken = false, message = "Kontrol sırasında bir hata oluştu." });
            }
        }



        [HttpGet("check-email/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckEmail(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return Ok(new { isTaken = false, message = "Email adresi boş olamaz." });
                }

                var user = await _userManager.FindByEmailAsync(email.Trim());
                var isTaken = user != null;

                return Ok(new { isTaken = isTaken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking email: {email}");
                return Ok(new { isTaken = false, message = "Kontrol sırasında bir hata oluştu." });
            }
        }

        [HttpGet("confirm-email")]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public async Task<IActionResult> ConfirmEmailGet([FromQuery] string token, [FromQuery] string email)
        {
            try
            {
                _logger.LogInformation($"=== EMAIL CONFIRMATION START ===");
                _logger.LogInformation($"Raw token: {token}");
                _logger.LogInformation($"Raw email: {email}");

                var decodedToken = Uri.UnescapeDataString(token ?? "");
                var decodedEmail = Uri.UnescapeDataString(email ?? "");

                _logger.LogInformation($"Decoded token: {decodedToken}");
                _logger.LogInformation($"Decoded email: {decodedEmail}");

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Token or email is missing");
                    var errorUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                    return Redirect($"{errorUrl}/auth?error=invalid-confirmation-link");
                }

                var result = await _authService.ConfirmEmailAsync(decodedToken, decodedEmail);

                _logger.LogInformation($"Email confirmation result: Success={result.Success}, Message={result.Message}");

                if (result.Success)
                {
                    _logger.LogInformation($"Email confirmation successful for: {decodedEmail}");
                    var frontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                    var redirectUrl = $"{frontendUrl}/auth?verified=true";
                    _logger.LogInformation($"Redirecting to: {redirectUrl}");
                    return Redirect(redirectUrl);
                }

                var errorFrontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                var errorRedirectUrl = $"{errorFrontendUrl}/auth?verified=false&error={Uri.EscapeDataString(result.Message)}";
                _logger.LogWarning($"Email confirmation failed, redirecting to: {errorRedirectUrl}");
                return Redirect(errorRedirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email confirmation");
                var errorUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                return Redirect($"{errorUrl}/auth?verified=false&error=system-error");
            }
        }

        [HttpPost("confirm-email")]
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

        [HttpPost("resend-email-confirmation")]
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


        [HttpDelete("deleteuser/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser([FromRoute] int id)
        {
            var serviceResult = await _authService.DeleteUserAsync(id);

            // Servisten dönen duruma göre HTTP yanıtını oluştur
            return serviceResult.StatusCode switch
            {
                404 => NotFound(new { message = serviceResult.Message }),
                400 => BadRequest(new { message = serviceResult.Message, errors = serviceResult.Errors }),
                200 => Ok(new { message = serviceResult.Message }),
                // Beklenmedik bir durum için varsayılan hata
                _ => StatusCode(500, new { message = "Beklenmedik bir sunucu hatası oluştu." })
            };
        
        
        }



    }
}