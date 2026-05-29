using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = "";

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; } = 30000m;

        [NotMapped]
        public decimal Discount { get; set; } = 0m;

        /// <summary>TotalAmount + ShippingFee - Discount</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalAmount { get; set; } = 0m;

        /// <summary>COD | BANK | MOMO | ZALOPAY</summary>
        [MaxLength(20)]
        public string PaymentMethod { get; set; } = "COD";

        /// <summary>UNPAID | PAID | REFUNDED</summary>
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "UNPAID";

        [MaxLength(20)]
        public string? OrderCode { get; set; }

        public string Status { get; set; } = OrderStatus.Pending;

        public string ShippingAddress { get; set; } = "";

        public string? Note { get; set; }

        public string? CancelReason { get; set; }

        public string? TrackingCode { get; set; }

        [MaxLength(100)]
        public string? ReceiverName { get; set; }

        [MaxLength(20)]
        public string? ReceiverPhone { get; set; }

        public DateTime? EstimatedDelivery { get; set; }

        public User User { get; set; } = null!;

        public List<OrderDetail> OrderDetails { get; set; } = new();

        public List<OrderStatusHistory> StatusHistory { get; set; } = new();
    }

    ///<summary>Hằng số trạng thái đơn hàng - dùng chung toàn bộ hệ thống </summary>
    public static class OrderStatus {
        public const string Pending   = "PENDING";
        public const string Confirmed = "CONFIRMED";
        public const string Shipping  = "SHIPPING";
        public const string Delivered = "DELIVERED";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
    }

    public static class PaymentMethodValue {
        public const string COD     = "COD";
        public const string Bank    = "BANK";
        public const string MoMo    = "MOMO";
        public const string ZaloPay = "ZALOPAY";
    }

    public static class PaymentStatusValue {
        public const string Unpaid   = "UNPAID";
        public const string Paid     = "PAID";
        public const string Refunded = "REFUNDED";
    }
}
