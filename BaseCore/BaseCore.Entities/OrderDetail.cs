using MongoDB.Bson.Serialization.Attributes;

namespace BaseCore.Entities
{
    public class OrderDetail
    {
        [BsonId]
        public int Id { get; set; }

        public int OrderId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        [BsonIgnore]
        public Order Order { get; set; }

        [BsonIgnore]
        public Product Product { get; set; }
    }
}
