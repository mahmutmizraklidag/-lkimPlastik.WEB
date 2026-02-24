namespace ilkimPlastik.WEB.Entities
{
    public class ProductDevice
    {
        public int Id { get; set; }
        public string Name { get; set; } // ürün ile uyumlu olan cihaz adı (örnek: arçelik elektrikli süpürge)
        public List<Product> Products { get; set; } = new();
    }
}
