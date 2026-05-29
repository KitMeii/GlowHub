using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class FlashSale
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Name { get; set; } = "";

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string? CreatedBy { get; set; }

        public List<FlashSaleProduct> Products { get; set; } = new();
    }
}
