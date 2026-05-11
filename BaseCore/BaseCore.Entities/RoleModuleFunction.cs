using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using BaseCore.Common;
using BaseCore.Entities.Audit;
using System;

namespace BaseCore.Entities
{
    public partial class RoleModuleFunction : Entity, IAuditable
    {
        public Guid Guid { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string RoleId { get; set; }
        [BsonRepresentation(BsonType.ObjectId)]
        public string ModuleFunctionId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }

    }
}
