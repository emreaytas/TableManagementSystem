using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class DataConversionIssue
    {
        public int RowId { get; set; }
        public string CurrentValue { get; set; } = string.Empty;
        public string IssueDescription { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }
}
