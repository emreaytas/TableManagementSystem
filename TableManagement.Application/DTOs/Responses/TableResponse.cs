using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Responses
{
    public class TableResponse
    {
        public int Id { get; set; }
        public string TableName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ColumnResponse> Columns { get; set; } = new List<ColumnResponse>();
    }

    public class ColumnResponse
    {
        public int Id { get; set; }
        public string ColumnName { get; set; }
        public ColumnDataType DataType { get; set; }
        public bool IsRequired { get; set; }
        public int DisplayOrder { get; set; }
        public string DefaultValue { get; set; }
    }
}