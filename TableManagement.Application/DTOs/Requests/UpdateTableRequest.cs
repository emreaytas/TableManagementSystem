using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TableManagement.Application.DTOs.Requests
{
    public class UpdateTableRequest
    {
        [Required]
        public int TableId { get; set; }

        [MaxLength(100)]
        public string? TableName { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public List<UpdateColumnRequest> Columns { get; set; } = new List<UpdateColumnRequest>();
    }

}
