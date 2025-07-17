using System.ComponentModel.DataAnnotations;
using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Requests
{
    public class UpdateColumnRequest
    {
        public int? ColumnId { get; set; } // Null for new columns

        [Required]
        [MaxLength(50)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        public ColumnDataType DataType { get; set; }

        public bool IsRequired { get; set; } = false;

        public int DisplayOrder { get; set; }

        [MaxLength(255)]
        public string? DefaultValue { get; set; }

        public bool ForceUpdate { get; set; } = false;
    }
}