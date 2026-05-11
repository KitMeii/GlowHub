using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class UserAddress
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string ReceiverName { get; set; } = "";

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string Province { get; set; } = "";

        [MaxLength(20)]
        public string ProvinceCode { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string District { get; set; } = "";

        [MaxLength(20)]
        public string DistrictCode { get; set; } = "";

        [MaxLength(100)]
        public string Ward { get; set; } = "";

        [MaxLength(20)]
        public string WardCode { get; set; } = "";

        [Required]
        [MaxLength(300)]
        public string Detail { get; set; } = "";

        // Tọa độ Google Maps
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [MaxLength(500)]
        public string? MapAddress { get; set; }

        public bool IsDefault { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public User? User { get; set; }

        // Computed: full address string
        [NotMapped]
        public string FullAddress =>
            $"{Detail}, {(string.IsNullOrEmpty(Ward) ? "" : Ward + ", ")}{District}, {Province}";
    }
}