using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class ColumnSummaryDto
    {
        public int Id { get; set; }
        public string ColumnName { get; set; } = string.Empty;
        public int DataType { get; set; }
        public bool IsRequired { get; set; }
        public string DefaultValue { get; set; } = string.Empty;
        public string DataTypeLabel { get; set; } = string.Empty;
        public string DataTypeColor { get; set; } = string.Empty;
    }

}
