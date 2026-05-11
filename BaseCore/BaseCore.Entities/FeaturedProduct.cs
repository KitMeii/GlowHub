using System;

namespace BaseCore.Entities
{
    public class FeaturedProduct
    {
        public int Id { get; set; }
        public int ProductId { get; set; }

        // 'new_arrivals' | 'best_sellers' | 'hero_slider'
        public string Section { get; set; } = "new_arrivals";

        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public Product? Product { get; set; }
    }
}