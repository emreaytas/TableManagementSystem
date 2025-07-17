using System.ComponentModel.DataAnnotations;
using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Requests
{
    public class ValidateTableUpdateRequest
    {
        [Required]
        public int TableId { get; set; }

        [Required]
        [MaxLength(50)]
        public string TableName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public List<UpdateColumnRequest>? Columns { get; set; }
    }
}
