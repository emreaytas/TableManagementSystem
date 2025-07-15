
using System.ComponentModel.DataAnnotations;


namespace TableManagement.Core.Entities
{
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; } // base olarak Id özelliği
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // ne zaman oluşturulduğu nesnenin bunu belirleriz.
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false; // default olarak false, silinmiş nesne olarak işaretlenebilir. sonra true yapabiliriz.
    }
}
