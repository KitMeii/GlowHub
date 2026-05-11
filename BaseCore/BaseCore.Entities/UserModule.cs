using MongoDB.Bson;
using BaseCore.Common;
using BaseCore.Entities.Audit;
using System;

namespace BaseCore.Entities
{
    public partial class UserModule : Entity, IAuditable
    {
        public Guid Guid { get; set; }
        public ObjectId UserId { get; set; }
        public ObjectId ModuleId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public string ModifiedBy { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDeleted { get; set; }

        public virtual Module Module { get; set; }
        public virtual User User { get; set; }
    }
}
