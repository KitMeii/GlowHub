using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class RecentlyViewed
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string UserId { get; set; } = "";

        public int ProductId { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
