using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace BaseCore.Entities
{
    public class Order
    {
        [BsonId]
        public int Id { get; set; }

        [BsonRepresentation(BsonType.String)]
        public string UserId { get; set; } //Trước đó của thầy là Guid

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public decimal TotalAmount { get; set; }

        //Dùng hằng số từ OrderStatus thay vì string tùy ý
        public string Status { get; set; } = OrderStatus.Pending; // Pending, Completed, Cancelled

        public string ShippingAddress { get; set; }

        public string Note { get; set; }

        [BsonIgnore]
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
