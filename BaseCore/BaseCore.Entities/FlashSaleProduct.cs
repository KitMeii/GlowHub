using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class FlashSaleProduct
    {
        public int Id { get; set; }

        public int FlashSaleId { get; set; }

        public int ProductId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SalePrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalPrice { get; set; }

        public int Quantity { get; set; }

        public int SoldCount { get; set; } = 0;

        /// <summary>Số lượng còn lại — cập nhật atomic khi mua, tránh race condition</summary>
        public int RemainingQuantity { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public FlashSale FlashSale { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
