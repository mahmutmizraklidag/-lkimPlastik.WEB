/* =========================================================
   1) Controllers/HomeController.cs (GÜNCEL)
   - Anasayfa için ürünleri hazırlar:
     Yeni Eklenenler, Seçilmiş Ürünler, Çok Tercih Edilenler (heuristik)
     Kategori kartları (Category + thumbnail)
     Cihaz listesi (uyumlu ürün mesajları için)
   ========================================================= */

using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly EfCoreContext _db;
        public HomeController(EfCoreContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var productsBase = _db.Products.AsNoTracking()
                .Include(p => p.ImageItems)
                .Include(p => p.Category)
                .Include(p => p.ProductDevices);

            var all = await productsBase
                .OrderByDescending(x => x.Id)
                .Take(200)
                .ToListAsync();

            var latest = all.Take(12).ToList();

            // Seçilmiş: fiyatı yüksek/orta karışık (vitrin)
            var featured = all
                .OrderByDescending(x => x.Price)
                .Take(10)
                .ToList();

            // Çok tercih edilen: satış verisi yoksa karışık seçki
            var rnd = new Random();
            var bestSeller = all.OrderBy(x => rnd.Next()).Take(10).ToList();

            // Kategoriler + örnek ürün görselleri
            var categories = await _db.Categories.AsNoTracking()
                .Include(c => c.Products)
                    .ThenInclude(p => p.ImageItems)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var catCards = categories
                .Select(c => new CategoryCardVm
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = string.IsNullOrWhiteSpace(c.Description) ? "Bu kategorideki ürünleri keşfedin." : c.Description,
                    Count = c.Products?.Count ?? 0,
                    Thumbs = (c.Products ?? new List<Product>())
                        .OrderByDescending(p => p.Id)
                        .Take(3)
                        .Select(p => p.ImageItems.FirstOrDefault()?.Filename)
                        .ToList()
                })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToList();

            var devices = await _db.ProductDevices.AsNoTracking()
                .OrderBy(x => x.Name)
                .Take(6)
                .ToListAsync();

            var vm = new HomeVm
            {
                Latest = latest,
                Featured = featured,
                BestSeller = bestSeller,
                CategoryCards = catCards,
                Devices = devices
            };

            return View(vm);
        }

        public class HomeVm
        {
            public List<Product> Latest { get; set; } = new();
            public List<Product> Featured { get; set; } = new();
            public List<Product> BestSeller { get; set; } = new();
            public List<CategoryCardVm> CategoryCards { get; set; } = new();
            public List<ProductDevice> Devices { get; set; } = new();
        }

        public class CategoryCardVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public int Count { get; set; }
            public List<string?> Thumbs { get; set; } = new();
        }
    }
}
