using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class WalletTransaction
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(450)]
        public string ShopId { get; set; } = "";

        public int? OrderId { get; set; }

        /// <summary>EARNING | REFUND | WITHDRAWAL | ADJUSTMENT</summary>
        [MaxLength(20)]
        public string Type { get; set; } = WalletTransactionType.Earning;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBefore { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; } = 0m;

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Shop? Shop { get; set; }

        public Order? Order { get; set; }
    }
}
