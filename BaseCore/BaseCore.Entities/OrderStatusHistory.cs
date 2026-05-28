using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class OrderStatusHistory
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "";

        [MaxLength(500)]
        public string? Note { get; set; }

        [MaxLength(450)]
        public string? ChangedBy { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        public Order Order { get; set; } = null!;
    }
}
