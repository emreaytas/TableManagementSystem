using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Core.Entities
{
    public class CustomTable : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string TableName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; } // Nullable yapıldı

        [Required]
        public int UserId { get; set; }

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<CustomColumn> Columns { get; set; } = new List<CustomColumn>();
        public virtual ICollection<CustomTableData> TableData { get; set; } = new List<CustomTableData>();
    }
}