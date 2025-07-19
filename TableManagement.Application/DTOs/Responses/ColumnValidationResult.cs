using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Responses
{
    public class ColumnValidationResult
    {
        public bool HasData { get; set; }
        public bool HasNullValues { get; set; }
        public bool CanConvert { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Issues { get; set; } = new List<string>();
    }


}
