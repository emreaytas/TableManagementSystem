using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TableManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        /// <summary>
        /// Herkes erişebilir - Token gereksiz
        /// </summary>
        [HttpGet("public")]
        public IActionResult Public()
        {
            return Ok(new { message = "Bu endpoint herkese açık", timestamp = DateTime.Now });
        }

        /// <summary>
        /// JWT Token gerekli
        /// </summary>
        [HttpGet("protected")]
        [Authorize]
        public IActionResult Protected()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userName = User.FindFirst(ClaimTypes.Name)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            return Ok(new
            {
                message = "Bu endpoint JWT token gerektirir",
                user = new
                {
                    id = userId,
                    userName = userName,
                    email = email
                },
                timestamp = DateTime.Now,
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }

        /// <summary>
        /// Token bilgilerini göster
        /// </summary>
        [HttpGet("token-info")]
        [Authorize]
        public IActionResult TokenInfo()
        {
            var claims = User.Claims.ToDictionary(c => c.Type, c => c.Value);

            return Ok(new
            {
                isAuthenticated = User.Identity?.IsAuthenticated,
                authenticationType = User.Identity?.AuthenticationType,
                name = User.Identity?.Name,
                claims = claims,
                headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
            });
        }
    }
}