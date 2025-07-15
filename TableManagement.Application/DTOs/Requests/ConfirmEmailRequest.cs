using System.ComponentModel.DataAnnotations;

namespace TableManagement.Application.DTOs.Requests
{
    public class ConfirmEmailRequest
    {
        [Required]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }
    }
}