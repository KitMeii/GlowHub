using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class SubOrder
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }

        [MaxLength(450)]
        public string ShopId { get; set; } = "";

        [MaxLength(20)]
        public string? SubOrderCode { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = OrderStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProductRevenue { get; set; } = 0m;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SellerPayoutAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShopVoucherDiscount { get; set; } = 0m;

        [MaxLength(20)]
        public string PayoutStatus { get; set; } = PayoutStatusValue.Pending;

        public DateTime? WalletReleaseAt { get; set; }

        [MaxLength(100)]
        public string? TrackingCode { get; set; }

        [MaxLength(500)]
        public string? CancelReason { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public Order Order { get; set; } = null!;
        public Shop Shop { get; set; } = null!;
        public List<SubOrderItem> Items { get; set; } = new();
    }
}
