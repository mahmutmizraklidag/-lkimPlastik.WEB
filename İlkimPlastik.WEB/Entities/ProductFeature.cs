namespace ilkimPlastik.WEB.Entities
{
    public class ProductFeature
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        // string -> int düzeltildi
        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }
}
