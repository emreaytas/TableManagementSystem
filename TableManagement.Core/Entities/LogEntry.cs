using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TableManagement.Core.Entities
{

    public class LogEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow; // UTC olarak kaydedelim

        [Required]
        [StringLength(50)]
        public string LogLevel { get; set; } = "Information"; // Varsayılan olarak Information

        [StringLength(255)]
        public string ApplicationName { get; set; } = "YourApp"; // Uygulama adınızı buraya yazın

        [StringLength(255)]
        public string? IpAddress { get; set; } // İstek yapanın IP adresi

        [StringLength(255)]
        public string? RequestPath { get; set; } // İsteğin geldiği yol (örn. /api/users)

        [StringLength(255)]
        public string? HttpMethod { get; set; } // HTTP metodu (GET, POST vb.)

        public string? RequestBody { get; set; } // İstek gövdesi (JSON, XML vb.)

        public string? QueryString { get; set; } // Sorgu dizisi

        public string? ResponseBody { get; set; } // Yanıt gövdesi (opsiyonel)

        [StringLength(10)]
        public string? StatusCode { get; set; } // HTTP durum kodu

        public string? Message { get; set; } // Genel log mesajı

        public string? StackTrace { get; set; } // Hata durumunda StackTrace
    }
}
