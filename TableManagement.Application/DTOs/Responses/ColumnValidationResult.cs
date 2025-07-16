using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class ColumnValidationResult
    {
        public bool IsValid { get; set; }
        public bool HasDataCompatibilityIssues { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public List<DataConversionIssue> DataIssues { get; set; } = new List<DataConversionIssue>();
        public int AffectedRowCount { get; set; }
        public bool RequiresForceUpdate { get; set; }
    }

}
