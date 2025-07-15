using Microsoft.AspNetCore.Mvc;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.Services;
using System.Web;
using TableManagement.Core.DTOs.Requests;

namespace TableManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
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

        [HttpPost("login")]
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

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token, [FromQuery] string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Geçersiz token veya email adresi"
                });
            }

            // URL decode işlemi
            var decodedToken = HttpUtility.UrlDecode(token);
            var decodedEmail = HttpUtility.UrlDecode(email);

            var result = await _authService.ConfirmEmailAsync(decodedToken, decodedEmail);

            if (result.Success)
            {
                // Frontend'e redirect yapabilirsiniz
                var frontendUrl = HttpContext.RequestServices
                    .GetService<IConfiguration>()?
                    .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

                return Redirect($"{frontendUrl}/email-confirmed?success=true");
            }

            // Hata durumunda da frontend'e yönlendir
            var errorUrl = HttpContext.RequestServices
                .GetService<IConfiguration>()?
                .GetValue<string>("FrontendSettings:BaseUrl") ?? "http://localhost:5173";

            return Redirect($"{errorUrl}/email-confirmed?success=false&message={HttpUtility.UrlEncode(result.Message)}");
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
    }
}