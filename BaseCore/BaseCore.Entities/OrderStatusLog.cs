using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    /// <summary>
    /// Nhật ký thay đổi trạng thái đơn hàng — ghi mỗi lần admin update Order.Status
    /// </summary>
    public class OrderStatusLog
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        [MaxLength(20)]
        public string? OldStatus { get; set; }

        [Required, MaxLength(20)]
        public string NewStatus { get; set; } = "";

        /// <summary>UserId (string) của admin đã đổi trạng thái</summary>
        [Required, MaxLength(450)]
        public string ChangedBy { get; set; } = "";

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
