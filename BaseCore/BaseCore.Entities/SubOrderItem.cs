using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class SubOrderItem
    {
        [Key]
        public int Id { get; set; }

        public int SubOrderId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } = 0m;

        public SubOrder SubOrder { get; set; } = null!;
        public Product? Product { get; set; }
    }
}
