using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class CustomerWalletTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(450)]
        public string UserId { get; set; } = "";

        /// <summary>TOPUP | REFUND | SPEND | ADJUSTMENT</summary>
        [Required, MaxLength(20)]
        public string Type { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBefore { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; } = 0m;

        [MaxLength(500)]
        public string? Note { get; set; }

        public int? OrderId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public Order? Order { get; set; }
    }
}
