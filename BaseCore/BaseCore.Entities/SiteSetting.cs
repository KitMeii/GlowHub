using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class SiteSetting
    {
        [Key, MaxLength(100)]
        public string Key { get; set; } = "";

        public string? Value { get; set; }

        // 'text' | 'html' | 'image' | 'color' | 'bool' | 'json'
        [MaxLength(20)]
        public string Type { get; set; } = "text";

        // 'general' | 'homepage' | 'seo' | 'social'
        [MaxLength(50)]
        public string Group { get; set; } = "general";

        [MaxLength(100)]
        public string? Label { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string? UpdatedBy { get; set; }
    }
}