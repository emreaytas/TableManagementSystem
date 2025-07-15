namespace TableManagement.Application.DTOs.Responses
{
    public class TableDataResponse
    {
        public int TableId { get; set; }
        public string TableName { get; set; }
        public List<ColumnResponse> Columns { get; set; } = new List<ColumnResponse>();
        public List<TableRowResponse> Rows { get; set; } = new List<TableRowResponse>();
    }

    public class TableRowResponse
    {
        public int RowIdentifier { get; set; }
        public Dictionary<int, string> Values { get; set; } = new Dictionary<int, string>();
    }
}