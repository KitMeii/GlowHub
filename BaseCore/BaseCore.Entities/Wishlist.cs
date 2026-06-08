using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class Wishlist
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(450)]
        public string CustomerId { get; set; } = "";

        [Required]
        public int ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CustomerId")]
        public User Customer { get; set; } = null!;

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }
}
