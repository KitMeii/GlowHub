using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class ShopProduct
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(450)]
        public string ShopId { get; set; } = "";

        public int ProductId { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public Shop Shop { get; set; }
        public Product Product { get; set; }
    }
}
