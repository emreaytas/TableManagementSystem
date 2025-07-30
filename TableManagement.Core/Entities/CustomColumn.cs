using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableManagement.Core.Enums;

namespace TableManagement.Core.Entities
{
    public class CustomColumn : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        public ColumnDataType DataType { get; set; } // Enum kullanıyoruz

        public bool IsRequired { get; set; } = false; // Nullable kolonlar için false olarak başlatıyoruz

        public int DisplayOrder { get; set; }

        [MaxLength(255)]
        public string? DefaultValue { get; set; } // Nullable yapıldı

        [Required]
        public int CustomTableId { get; set; }

        public virtual CustomTable? CustomTable { get; set; }

    }
}