using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BaseCore.Entities
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public decimal Price { get; set; }

        public int Stock { get; set; }

        public string ImageUrl { get; set; }

        public string Description { get; set; }

        public int CategoryId { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Optimistic Concurrency — SQL Server tự cập nhật, EF tự kiểm tra khi UPDATE</summary>
        [Timestamp]
        public byte[] RowVersion { get; set; }

        public Category Category { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountPrice { get; set; }

        public bool IsNew { get; set; } = false;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Shop owning this product — null if seeded or admin-created</summary>
        public string? ShopId { get; set; }

        public Shop? Shop { get; set; }

        /// <summary>JSON array of additional image URLs (max 5)</summary>
        public string? Images { get; set; }

        /// <summary>JSON key-value specifications</summary>
        public string? Specifications { get; set; }

        /// <summary>Cached sold count — incremented when order completes</summary>
        public int SoldCount { get; set; } = 0;
    }
}