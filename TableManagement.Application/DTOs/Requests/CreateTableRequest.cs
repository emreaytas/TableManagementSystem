using System.ComponentModel.DataAnnotations;
using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Requests
{
    public class CreateTableRequest
    {
        [Required]
        [MaxLength(100)]
        public string TableName { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public List<CreateColumnRequest> Columns { get; set; } = new List<CreateColumnRequest>();
    }

    public class CreateColumnRequest
    {
        [Required]
        [MaxLength(100)]
        public string ColumnName { get; set; }

        [Required]
        public ColumnDataType DataType { get; set; }

        public bool IsRequired { get; set; } = false;

        public int DisplayOrder { get; set; }

        [MaxLength(255)]
        public string DefaultValue { get; set; }
    }
}