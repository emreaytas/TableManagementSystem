using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableManagement.Core.Enums;

namespace TableManagement.Application.DTOs.Requests
{
    public class UpdateColumnRequest
    {
        [Required]
        public int ColumnId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ColumnName { get; set; } = string.Empty;

        [Required]
        public ColumnDataType DataType { get; set; }

        public bool IsRequired { get; set; } = false;

        public int DisplayOrder { get; set; }

        [MaxLength(255)]
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Veri uyumsuzluğu durumunda zorla güncelleme yapılsın mı?
        /// </summary>
        public bool ForceUpdate { get; set; } = false;
    }
}
