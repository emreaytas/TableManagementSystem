using System.ComponentModel.DataAnnotations;

namespace TableManagement.Application.DTOs.Requests
{
    public class AddTableDataRequest
    {
        [Required]
        public int TableId { get; set; }

        [Required]
        public Dictionary<int, string> ColumnValues { get; set; } = new();
    }
}