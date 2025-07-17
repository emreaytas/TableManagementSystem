using System.ComponentModel.DataAnnotations;

namespace TableManagement.Application.DTOs.Requests
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-50 karakter arasında olmalıdır.")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Şifre en az 6 karakter olmalıdır.")]
        public string Password { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"LoginRequest: UserName='{UserName}'";
        }
    }
}