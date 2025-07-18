using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Requests
{
    public class UpdateTableDataRequest
    {
        [Required]
        public int TableId { get; set; }

        [Required]
        public int RowIdentifier { get; set; }

        [Required]
        public Dictionary<string, string> ColumnValues { get; set; } = new Dictionary<string, string>();
    }

}
