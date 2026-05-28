using System;
using System.ComponentModel.DataAnnotations;

namespace BaseCore.Common
{
    public class Entity
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public DateTime CreatedDateTime { get; set; } = new DateTime();
        public string CreatedUser { get; set; }
    }
}
