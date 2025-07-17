using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class ColumnValidationResult
    {
        public bool IsValid { get; set; } = true;
        public bool HasDataCompatibilityIssues { get; set; }
        public bool RequiresForceUpdate { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> DataIssues { get; set; } = new();
        public int AffectedRowCount { get; set; }
    }

}
