using System.ComponentModel.DataAnnotations;

namespace ilkimPlastik.WEB.Entities
{
    public class SubCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Slug { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public List<Product> Products { get; set; } = new();
    }
}
