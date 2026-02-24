namespace ilkimPlastik.WEB.Entities
{
    public class ProductSize
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int StockCount { get; set; } //stok adedi

        public int ProductId { get; set; }
        public Product Product { get; set; }
    }
}
