using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class CustomerWallet
    {
        [Key]
        [MaxLength(450)]
        public string UserId { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalReceived { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSpent { get; set; } = 0m;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }

    public static class CustomerWalletTxType
    {
        public const string TopUp      = "TOPUP";
        public const string Refund     = "REFUND";
        public const string Spend      = "SPEND";
        public const string Adjustment = "ADJUSTMENT";
    }
}
