using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;   // Thêm dòng này

namespace BaseCore.Entities
{
    public class Product
    {
        [BsonId]
        public int Id { get; set; }

        public string Name { get; set; }

        public decimal Price { get; set; }

        public int Stock { get; set; }

        public string ImageUrl { get; set; }

        public string Description { get; set; }

        public int CategoryId { get; set; }

        public bool IsActive { get; set; } = true;

        [Timestamp]
        public byte[] RowVersion { get; set; }

        [BsonIgnore]
        public Category Category { get; set; }

        // 👇 Thêm [NotMapped] cho các thuộc tính không có trong DB
        [NotMapped]
        public decimal? DiscountPrice { get; set; }

        [NotMapped]
        public bool IsNew { get; set; } = false;

        [NotMapped]
        public int SortOrder { get; set; } = 0;

        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}