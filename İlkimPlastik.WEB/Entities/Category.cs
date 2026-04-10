using System.ComponentModel.DataAnnotations;

namespace ilkimPlastik.WEB.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Slug { get; set; }
        public List<Product> Products { get; set; } = new();
        public List<SubCategory> SubCategories { get; set; } = new();
    }
}
