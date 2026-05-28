using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public enum NotificationType
    {
        NewOrder        = 0,
        LowStock        = 1,
        NewReview       = 2,
        NewQuestion     = 3,
        OrderCancelled  = 4,
        SystemAlert     = 5
    }

    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = "";

        public NotificationType Type { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = "";

        public bool IsRead { get; set; } = false;

        [StringLength(500)]
        public string? Link { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }
}
