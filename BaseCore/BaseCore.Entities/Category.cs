using System.ComponentModel.DataAnnotations;

namespace BaseCore.Entities
{
    public class Category
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
