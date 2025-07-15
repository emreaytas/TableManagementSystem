using System.ComponentModel.DataAnnotations;

namespace TableManagement.Application.DTOs.Requests
{
    public class ResendEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}