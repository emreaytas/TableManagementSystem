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

        [MaxLength(1000)]
        public string? Value { get; set; } // Nullable yapıldı

        public int RowIdentifier { get; set; } // Aynı satırdaki verileri gruplamak için

        // Navigation Properties
        public virtual CustomTable CustomTable { get; set; } = null!;
        public virtual CustomColumn Column { get; set; } = null!;
    }
}