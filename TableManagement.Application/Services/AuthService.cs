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

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IConfiguration configuration,
            IMapper mapper,
            IUnitOfWork unitOfWork,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
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
                    EmailConfirmed = false, // Email doğrulanana kadar false
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
                var frontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                var encodedToken = HttpUtility.UrlEncode(emailConfirmationToken);
                var encodedEmail = HttpUtility.UrlEncode(user.Email);
                var confirmationLink = $"{frontendUrl}/confirm-email?token={encodedToken}&email={encodedEmail}";

                // Send confirmation email
                var emailSent = await _emailService.SendEmailConfirmationAsync(
                    user.Email,
                    user.FirstName,
                    confirmationLink
                );

                if (!emailSent)
                {
                    // Email gönderilemedi ama kullanıcı oluşturuldu
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
                    User = _mapper.Map<UserDto>(user)
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = "Kayıt sırasında bir hata oluştu." };
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(request.UserName);
                if (user == null)
                {
                    return new AuthResponse { Success = false, Message = "Kullanıcı adı veya şifre hatalı." };
                }

                // Email doğrulanmış mı kontrol et
                if (!user.EmailConfirmed)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Hesabınız henüz aktif değil. Lütfen email adresinizi doğrulayın."
                    };
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
                if (!result.Succeeded)
                {
                    return new AuthResponse { Success = false, Message = "Kullanıcı adı veya şifre hatalı." };
                }

                var token = GenerateJwtToken(user.Id, user.UserName, user.Email);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Giriş başarılı.",
                    Token = token,
                    User = _mapper.Map<UserDto>(user)
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = "Giriş sırasında bir hata oluştu." };
            }
        }

        public async Task<AuthResponse> ConfirmEmailAsync(string token, string email)
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

                var result = await _userManager.ConfirmEmailAsync(user, token);
                if (!result.Succeeded)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Email doğrulama başarısız. Token geçersiz veya süresi dolmuş."
                    };
                }

                // Kullanıcının IsEmailConfirmed özelliğini de güncelle
                user.IsEmailConfirmed = true;
                await _userManager.UpdateAsync(user);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Email adresiniz başarıyla doğrulandı. Artık giriş yapabilirsiniz.",
                    User = _mapper.Map<UserDto>(user)
                };
            }
            catch (Exception ex)
            {
                return new AuthResponse { Success = false, Message = "Email doğrulama sırasında bir hata oluştu." };
            }
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

                // Generate new email confirmation token
                var emailConfirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                // Create confirmation link
                var frontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                var encodedToken = HttpUtility.UrlEncode(emailConfirmationToken);
                var encodedEmail = HttpUtility.UrlEncode(user.Email);
                var confirmationLink = $"{frontendUrl}/confirm-email?token={encodedToken}&email={encodedEmail}";

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
                        Success = false,
                        Message = "Email gönderilemedi. Lütfen daha sonra tekrar deneyin."
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
                return new AuthResponse { Success = false, Message = "Email gönderme sırasında bir hata oluştu." };
            }
        }

        public string GenerateJwtToken(int userId, string userName, string email)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"]);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userName),
                new Claim(ClaimTypes.Email, email)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(jwtSettings["ExpireDays"])),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        
        
        }



    }
}