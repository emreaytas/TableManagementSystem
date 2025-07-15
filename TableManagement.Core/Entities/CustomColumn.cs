using Microsoft.Azure.Management.Sql.Models;
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
        public Enums.ColumnDataType DataType { get; set; } // 1 2 3 4 diye kolon tanımlamak yerine enum kullanıyoruz.

        public bool IsRequired { get; set; } = false; // Nullable kolonlar için false olarak başlatıyoruz.

        public int DisplayOrder { get; set; } 

        [MaxLength(255)]
        public string DefaultValue { get; set; } = string.Empty; 

        [Required]
        public int CustomTableId { get; set; }

        // Navigation Properties
        public virtual CustomTable? CustomTable { get; set; } 

    }
}
