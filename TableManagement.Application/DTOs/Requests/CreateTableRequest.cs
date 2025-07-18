using System.ComponentModel.DataAnnotations;
using TableManagement.Core.Enums;

namespace TableManagement.Core.DTOs.Requests
{
    public class CreateTableRequest
    {
        [Required]
        [MaxLength(50)]
        public string TableName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "En az bir kolon eklemelisiniz")]
        public List<CreateColumnRequest> Columns { get; set; } = new();


    }

    public class CreateColumnRequest
    {
        [Required]
        [MaxLength(50)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        public ColumnDataType DataType { get; set; }

        public bool IsRequired { get; set; } = false;

        public int DisplayOrder { get; set; }

        [MaxLength(255)]
        public string? DefaultValue { get; set; }
    }
}