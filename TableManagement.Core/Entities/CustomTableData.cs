using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Core.Entities
{
    public class CustomTableData : BaseEntity
    {
        [Required]
        public int CustomTableId { get; set; }

        [Required]
        public int ColumnId { get; set; }

        public string Value { get; set; }

        public int RowIdentifier { get; set; } // Aynı satırdaki verileri gruplamak için

        // Navigation Properties
        public virtual CustomTable CustomTable { get; set; }
        public virtual CustomColumn Column { get; set; }
    }
}
