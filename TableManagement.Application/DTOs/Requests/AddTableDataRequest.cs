using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Core.DTOs.Requests
{
    public class AddTableDataRequest
    {
        public int TableId { get; set; }
        public Dictionary<int, string> ColumnValues { get; set; } = new Dictionary<int, string>();
    }
}
