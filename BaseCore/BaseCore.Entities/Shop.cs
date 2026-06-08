using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class Shop
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required, MaxLength(450)]
        public string SellerId { get; set; } = "";

        [Required, MaxLength(100)]
        public string ShopName { get; set; } = "";

        [MaxLength(500)]
        public string Description { get; set; } = "";

        [MaxLength(500)]
        public string Logo { get; set; } = "";

        [MaxLength(300)]
        public string Address { get; set; } = "";

        [MaxLength(20)]
        public string Phone { get; set; } = "";

        /// <summary>0=Pending, 1=Active, 2=Banned</summary>
        public int Status { get; set; } = ShopStatus.Pending;

        [Column(TypeName = "decimal(5,2)")]
        public decimal CommissionRate { get; set; } = 10m;

        [MaxLength(100)]
        public string? Province { get; set; }

        /// <summary>Vùng xuất phát: HCM | HN | OTHER — tính phí ship</summary>
        [MaxLength(20)]
        public string? Region { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public User Seller { get; set; }
        public List<ShopProduct> ShopProducts { get; set; } = new();
    }

    public static class ShopStatus
    {
        public const int Pending = 0;
        public const int Active  = 1;
        public const int Banned  = 2;
    }
}
