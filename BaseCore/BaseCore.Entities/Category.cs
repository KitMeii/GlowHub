using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BaseCore.Entities
{
    public class Category
    {
        [BsonId]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }
    }
}
