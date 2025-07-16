using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class ColumnUpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ColumnValidationResult? ValidationResult { get; set; }
        public List<string> ExecutedQueries { get; set; } = new List<string>();
    }

}
