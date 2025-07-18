using System.ComponentModel.DataAnnotations;

namespace TableManagement.Application.DTOs.Requests
{
    public class AddTableDataRequest
    {
        [Required]
        public int TableId { get; set; }

        [Required]
        public Dictionary<string, string> ColumnValues { get; set; } = new Dictionary<string, string>();
    }

    // Backward compatibility için ID bazlı
    public class AddTableDataByIdRequest
    {
        [Required]
        public int TableId { get; set; }

        [Required]
        public Dictionary<int, string> ColumnValues { get; set; } = new Dictionary<int, string>();
    }





}