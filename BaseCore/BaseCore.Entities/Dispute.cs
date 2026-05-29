using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class Dispute
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; }

        [Required, MaxLength(450)]
        public string CustomerId { get; set; } = "";

        [Required, MaxLength(200)]
        public string Reason { get; set; } = "";

        public string? Description { get; set; }

        /// <summary>JSON array of evidence image URLs</summary>
        public string? Evidence { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = DisputeStatus.Open;

        public string? Resolution { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RefundAmount { get; set; } = 0;

        public bool FavorCustomer { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(450)]
        public string? ResolvedBy { get; set; }

        public Order? Order { get; set; }
        public User? Customer { get; set; }
    }

    public static class DisputeStatus
    {
        public const string Open       = "OPEN";
        public const string Processing = "PROCESSING";
        public const string Resolved   = "RESOLVED";
    }
}
