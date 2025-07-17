using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Responses
{
    public class ColumnResponse
    {
        public int Id { get; set; }
        public string ColumnName { get; set; } = string.Empty;
        public ColumnDataType DataType { get; set; }
        public bool IsRequired { get; set; }
        public int DisplayOrder { get; set; }
        public string? DefaultValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}