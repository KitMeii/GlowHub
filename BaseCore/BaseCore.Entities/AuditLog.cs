using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(256)]
        public string? UserName { get; set; }

        [MaxLength(100)]
        public string Action { get; set; } = "";

        [MaxLength(100)]
        public string? EntityType { get; set; }

        [MaxLength(450)]
        public string? EntityId { get; set; }

        public string? OldValue { get; set; }

        public string? NewValue { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
