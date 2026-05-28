using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class QnA
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(450)]
        public string CustomerId { get; set; } = "";

        [Required]
        [StringLength(1000)]
        public string Question { get; set; } = "";

        public DateTime AskedAt { get; set; } = DateTime.UtcNow;

        [StringLength(2000)]
        public string? Answer { get; set; }

        public DateTime? AnsweredAt { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [ForeignKey("CustomerId")]
        public User Customer { get; set; } = null!;
    }
}
