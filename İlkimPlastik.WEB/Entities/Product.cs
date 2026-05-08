namespace ilkimPlastik.WEB.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Keywords { get; set; }
        public string Barcode { get; set; }
        public string ModelCode { get; set; } // ürünlerin birbirinin alternetifi olduğunu anlayabilmek için örnek; kırmızı , beyaz gibi renk farkı olan ürünlerde aynı model kodu verilecek
        public string? AverageDeliveryTime { get; set; } // ortalama teslim süresi (örnek: 2-3 iş günü)

        // fiyat string olmasın (hatalı/riski yüksek). decimal daha doğru
        public decimal Price { get; set; }
        public string? Slug { get; set; }
        public int CategoryId { get; set; }
        public int? SubCategoryId { get; set; }          // ✅ EKLE
        // indrimli fiyat eklenebilir
        public int OfferRate { get; set; } // yüzde olarak indirim oranı
        // İlk sayfada göster özelliği
        public bool IsFeatured { get; set; } // ürünün ilk sayfada gösterilip gösterilmeyeceği bilgisi
        // minimum satış adedi
        public int? MinimumOrderQuantity { get; set; } // minimum satış adedi bilgisi
        public Category? Category { get; set; }
        public SubCategory? SubCategory { get; set; }
        public List<ImageItem> ImageItems { get; set; } = new();
        public List<ProductFeature> ProductFeatures { get; set; } = new();
        public List<ProductSize> ProductSizes { get; set; } = new();
        public List<ProductDevice> ProductDevices { get; set; } = new();// uyumlu olduğu cihazlar seçilerek ürünün hangi cihazlarla uyumlu olduğunu göstermek için
    }
}
