using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public decimal TotalAmount { get; set; }

        //Dùng hằng số từ OrderStatus thay vì string tùy ý
        public string Status { get; set; } = OrderStatus.Pending; // Pending, Completed, Cancelled

        public string ShippingAddress { get; set; }

        public string Note { get; set; }

        public string? CancelReason { get; set; }

        public string? TrackingCode { get; set; }

        public User User { get; set; }

        public List<OrderDetail> OrderDetails { get; set; } = new();
    }

    ///<summary>Hằng số trạng thái đơn hàng - dùng chung toàn bộ hệ thống </summary>
    public static class OrderStatus {
        public const string Pending = "PENDING";
        public const string Confirmed = "CONFIRMED";
        public const string Shipping = "SHIPPING";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";
    }

}
