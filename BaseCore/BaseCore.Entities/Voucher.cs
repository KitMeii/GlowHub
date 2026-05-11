using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class Voucher
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = "";

        [MaxLength(200)]
        public string? Description { get; set; }

        // 'percent' | 'fixed'
        [MaxLength(20)]
        public string DiscountType { get; set; } = "percent";

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinOrderAmount { get; set; } = 0;
        public int? UsageLimit { get; set; }
        public int UsedCount { get; set; } = 0;
        public DateTime? StartDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}