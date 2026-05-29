using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class SellerWallet
    {
        [Key]
        [MaxLength(450)]
        public string ShopId { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalEarned { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWithdrawn { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRefunded { get; set; } = 0m;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public Shop? Shop { get; set; }
    }

    public static class WalletTransactionType
    {
        public const string Earning    = "EARNING";
        public const string Refund     = "REFUND";
        public const string Withdrawal = "WITHDRAWAL";
        public const string Adjustment = "ADJUSTMENT";
    }
}
