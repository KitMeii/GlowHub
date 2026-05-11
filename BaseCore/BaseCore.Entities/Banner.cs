using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class Banner
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [MaxLength(500)]
        public string? Subtitle { get; set; }

        [MaxLength(500)]
        public string ImageUrl { get; set; } = "";

        [MaxLength(500)]
        public string? LinkUrl { get; set; }

        [MaxLength(50)]
        public string? ButtonText { get; set; }

        [MaxLength(20)]
        public string? BgColor { get; set; }

        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [MaxLength(450)]
        public string? CreatedBy { get; set; }
    }
}