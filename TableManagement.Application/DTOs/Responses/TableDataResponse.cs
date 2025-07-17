namespace TableManagement.Application.DTOs.Responses
{
    public class TableDataResponse
    {
        public int TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public List<ColumnResponse> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Data { get; set; } = new();
    }

    public class TableRowResponse
    {
        public int RowIdentifier { get; set; }
        public Dictionary<int, string> Values { get; set; } = new Dictionary<int, string>();
    }
}