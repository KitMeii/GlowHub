using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class PayoutHistory
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(450)]
        public string ShopId { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime PayoutDate { get; set; }

        [MaxLength(450)]
        public string? ProcessedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Shop? Shop { get; set; }
    }
}
