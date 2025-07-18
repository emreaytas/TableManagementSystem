using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using AutoMapper;
using System.Web;
using TableManagement.Core.DTOs.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TableManagement.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IActionResult> GetAllUsers()
        {
            try
            {

                return new OkObjectResult(await _userManager.Users.ToListAsync());


            }
            catch (Exception ex)
            {
                return new StatusCodeResult(500); 

            }


        }


        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"Login attempt for username: {request?.UserName}");

                // Input validation
                if (string.IsNullOrWhiteSpace(request?.UserName) || string.IsNullOrWhiteSpace(request?.Password))
                {
                    _logger.LogWarning("Login attempt with empty username or password");
                    return new AuthResponse { Success = false, Message = "Kullanıcı adı ve şifre boş olamaz." };
                }

                // Kullanıcıyı bul
                var user = await _userManager.FindByNameAsync(request.UserName.Trim());
                if (user == null)
                {
                    _logger.LogWarning($"Login failed: User not found for username: {request.UserName}");
                    return new AuthResponse { Success = false, Message = "Kullanıcı adı veya şifre hatalı." };
                }

                _logger.LogInformation($"User found: {user.UserName}, EmailConfirmed: {user.EmailConfirmed}");

                // Email doğrulanmış mı kontrol et
                if (!user.EmailConfirmed)
                {
                    _logger.LogWarning($"Login failed: Email not confirmed for user: {user.UserName}");
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Hesabınız henüz aktif değil. Lütfen email adresinizi doğrulayın."
                    };
                }

                // Şifre kontrolü
                var passwordResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
                if (!passwordResult.Succeeded)
                {
                    _logger.LogWarning($"Login failed: Invalid password for user: {user.UserName}");

                    if (passwordResult.IsLockedOut)
                    {
                        return new AuthResponse { Success = false, Message = "Hesabınız geçici olarak kilitlenmiştir." };
                    }

                    if (passwordResult.IsNotAllowed)
                    {
                        return new AuthResponse { Success = false, Message = "Giriş izni bulunmuyor." };
                    }

                    return new AuthResponse { Success = false, Message = "Kullanıcı adı veya şifre hatalı." };
                }

                // Token oluştur
                _logger.LogInformation($"Creating JWT token for user: {user.UserName} with ID: {user.Id}");
                var token = GenerateJwtToken(user.Id, user.UserName, user.Email);

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError($"Failed to generate JWT token for user: {user.UserName}");
                    return new AuthResponse { Success = false, Message = "Token oluşturulurken bir hata oluştu." };
                }

                _logger.LogInformation($"Login successful for user: {user.UserName}");

                return new AuthResponse
                {
                    Success = true,
                    Message = "Giriş başarılı.",
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        UserName = user.UserName,
                        IsEmailConfirmed = user.IsEmailConfirmed,
                        EmailConfirmed = user.EmailConfirmed
                    }
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during login for username: {request?.UserName}");
                return new AuthResponse
                {
                    Success = false,
                    Message = "Giriş sırasında beklenmeyen bir hata oluştu. Lütfen tekrar deneyin."
                };
            }
        }

        public string GenerateJwtToken(int userId, string userName, string email)
        {
            try
            {
                _logger.LogInformation($"Generating JWT token for userId: {userId}, userName: {userName}");

                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"];

                if (string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogError("JWT SecretKey is missing in configuration");
                    return null;
                }

                var key = Encoding.ASCII.GetBytes(secretKey);

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, userName ?? ""),
                    new Claim(ClaimTypes.Email, email ?? ""),
                    new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(JwtRegisteredClaimNames.Iat,
                        new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                        ClaimValueTypes.Integer64)
                };

                var expireDays = jwtSettings["ExpireDays"];
                var expireTime = string.IsNullOrEmpty(expireDays) ?
                    DateTime.UtcNow.AddDays(7) :
                    DateTime.UtcNow.AddDays(Convert.ToDouble(expireDays));

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expireTime,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature),
                    Issuer = jwtSettings["Issuer"],
                    Audience = jwtSettings["Audience"]
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation($"JWT token generated successfully for user: {userName}");
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating JWT token for userId: {userId}, userName: {userName}");
                return null;
            }
        }

        // Diğer metodları burada aynı kalacak...
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // Mevcut register kodu aynı kalır
            try
            {
                // Check if user exists
                var existingUser = await _userManager.FindByEmailAsync(request.Email);
                if (existingUser != null)
                {
                    return new AuthResponse { Success = false, Message = "Bu email adresi zaten kullanılıyor." };
                }

                existingUser = await _userManager.FindByNameAsync(request.UserName);
                if (existingUser != null)
                {
                    return new AuthResponse { Success = false, Message = "Bu kullanıcı adı zaten kullanılıyor." };
                }

                // Create new user
                var user = new User
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    UserName = request.UserName,
                    EmailConfirmed = false,
                    IsEmailConfirmed = false
                };

                var result = await _userManager.CreateAsync(user, request.Password);
                if (!result.Succeeded)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = string.Join(", ", result.Errors.Select(e => e.Description))
                    };
                }

                // Generate email confirmation token
                var emailConfirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                // Create confirmation link
                var baseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                var encodedToken = HttpUtility.UrlEncode(emailConfirmationToken);
                var encodedEmail = HttpUtility.UrlEncode(user.Email);

                var confirmationLink = $"{_configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7018"}/api/auth/confirm-email?token={encodedToken}&email={encodedEmail}";

                _logger.LogInformation($"Generated confirmation link: {confirmationLink}");

                // Send confirmation email
                var emailSent = await _emailService.SendEmailConfirmationAsync(
                    user.Email,
                    user.FirstName,
                    confirmationLink
                );

                if (!emailSent)
                {
                    return new AuthResponse
                    {
                        Success = true,
                        Message = "Kayıt başarılı ancak doğrulama email'i gönderilemedi. Lütfen daha sonra tekrar deneyin.",
                        User = _mapper.Map<UserDto>(user)
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "Kayıt başarılı! Email adresinize gönderilen doğrulama linkine tıklayarak hesabınızı aktifleştirin.",
                    User = new UserDto
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        UserName = user.UserName,
                        IsEmailConfirmed = user.IsEmailConfirmed,
                        EmailConfirmed = user.EmailConfirmed
                    }
                };

            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error occurred");
                return new AuthResponse { Success = false, Message = "Kayıt sırasında bir hata oluştu." };
            }
        }

        public async Task<AuthResponse> ConfirmEmailAsync(string token, string email)
        {
            try
            {
                _logger.LogInformation($"=== EMAIL CONFIRMATION SERVICE START ===");
                _logger.LogInformation($"Email confirmation attempt for: {email}");
                _logger.LogInformation($"Token (first 50 chars): {token?.Substring(0, Math.Min(50, token?.Length ?? 0))}...");

                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    _logger.LogWarning($"User not found for email: {email}");
                    return new AuthResponse { Success = false, Message = "Kullanıcı bulunamadı." };
                }

                _logger.LogInformation($"User found: ID={user.Id}, UserName={user.UserName}, EmailConfirmed={user.EmailConfirmed}");

                if (user.EmailConfirmed)
                {
                    _logger.LogInformation($"Email already confirmed for user: {user.UserName}");
                    return new AuthResponse { Success = true, Message = "Email adresi zaten doğrulanmış." };
                }

                // Token doğrulama
                var result = await _userManager.ConfirmEmailAsync(user, token);

                _logger.LogInformation($"ConfirmEmailAsync result: Succeeded={result.Succeeded}");

                if (!result.Succeeded)
                {
                    _logger.LogError($"Email confirmation failed for user: {user.UserName}");
                    foreach (var error in result.Errors)
                    {
                        _logger.LogError($"Identity Error: {error.Code} - {error.Description}");
                    }

                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Email doğrulama başarısız. Token geçersiz veya süresi dolmuş olabilir."
                    };
                }

                // Kullanıcının IsEmailConfirmed özelliğini de güncelle
                user.IsEmailConfirmed = true;
                var updateResult = await _userManager.UpdateAsync(user);

                if (!updateResult.Succeeded)
                {
                    _logger.LogError($"Failed to update IsEmailConfirmed for user: {user.UserName}");
                    foreach (var error in updateResult.Errors)
                    {
                        _logger.LogError($"Update Error: {error.Code} - {error.Description}");
                    }
                }
                else
                {
                    _logger.LogInformation($"User updated successfully: EmailConfirmed={user.EmailConfirmed}, IsEmailConfirmed={user.IsEmailConfirmed}");
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "Email adresiniz başarıyla doğrulandı. Artık giriş yapabilirsiniz.",
                    User = new UserDto  
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        UserName = user.UserName,
                        IsEmailConfirmed = user.IsEmailConfirmed,
                        EmailConfirmed = user.EmailConfirmed
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Email confirmation error for email: {email}");
                return new AuthResponse { Success = false, Message = "Email doğrulama sırasında bir hata oluştu." };
            }
        }


        public async Task<ServiceResponse> DeleteUserAsync(int id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return new ServiceResponse
                {
                    Succeeded = false,
                    Message = "Kullanıcı bulunamadı.",
                    StatusCode = 404 // Not Found
                };
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return new ServiceResponse
                {
                    Succeeded = false,
                    Message = "Kullanıcı silinirken bir hata oluştu.",
                    Errors = result.Errors,
                    StatusCode = 400 // Bad Request
                };
            }

            return new ServiceResponse
            {
                Succeeded = true,
                Message = "Kullanıcı başarıyla silindi.",
                StatusCode = 200 // OK
            };
        }



        public async Task<AuthResponse> ResendEmailConfirmationAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return new AuthResponse { Success = false, Message = "Kullanıcı bulunamadı." };
                }

                if (user.EmailConfirmed)
                {
                    return new AuthResponse { Success = false, Message = "Email adresi zaten doğrulanmış." };
                }

                // Generate new confirmation token
                var emailConfirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var baseUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                var encodedToken = HttpUtility.UrlEncode(emailConfirmationToken);
                var encodedEmail = HttpUtility.UrlEncode(user.Email);

                var confirmationLink = $"{_configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7018"}/api/auth/confirm-email?token={encodedToken}&email={encodedEmail}";

                var emailSent = await _emailService.SendEmailConfirmationAsync(
                    user.Email,
                    user.FirstName,
                    confirmationLink
                );

                if (!emailSent)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Email gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin."
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "Doğrulama email'i yeniden gönderildi. Lütfen gelen kutunuzu kontrol edin."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend email confirmation error occurred");
                return new AuthResponse { Success = false, Message = "Email gönderme sırasında bir hata oluştu." };
            }
        }
    }
}