using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    /// <summary>Giỏ hàng lưu DB — đồng bộ đa thiết bị, không mất khi đóng browser</summary>
    public class CartItem
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; }
        public Product Product { get; set; }
    }
}
