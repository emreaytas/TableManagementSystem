using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Responses
{
    public class TableResponse
    {
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<ColumnResponse> Columns { get; set; } = new();
    }


}