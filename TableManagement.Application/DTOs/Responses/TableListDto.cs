using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class TableListDto
    {
        public int Id { get; set; }
        public string TableName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ColumnCount { get; set; }
        public string FormattedDate { get; set; } = string.Empty;
        public string StatusBadge { get; set; } = string.Empty;
        public string StatusColor { get; set; } = string.Empty;
        public List<ColumnSummaryDto> Columns { get; set; } = new();
    }
}
