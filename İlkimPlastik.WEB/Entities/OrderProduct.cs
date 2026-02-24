namespace ilkimPlastik.WEB.Entities
{
    public class OrderProduct
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        // Sipariş anında ürün snapshot’ı
        public int ProductId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Keywords { get; set; }
        public decimal Price { get; set; }

        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string? ImageName { get; set; }

        public int Count { get; set; }
    }
}
