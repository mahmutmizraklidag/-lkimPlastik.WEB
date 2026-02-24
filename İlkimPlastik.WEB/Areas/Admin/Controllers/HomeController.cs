using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class HomeController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        public HomeController(EfCoreContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            // Ürün toplamları + stok toplamları
            var productCount = await _db.Products.CountAsync();
            var categoryCount = await _db.Categories.CountAsync();
            var subCategoryCount = await _db.SubCategories.CountAsync();
            var orderCount = await _db.Orders.CountAsync();
            var userCount = await _db.Users.CountAsync();
            var deviceCount = await _db.ProductDevices.CountAsync();

            // Stok: ProductSize üzerinden
            var totalStock = await _db.ProductSizes.SumAsync(x => (int?)x.StockCount) ?? 0;
            var stockRows = await _db.ProductSizes.CountAsync();
            var outOfStockRows = await _db.ProductSizes.CountAsync(x => x.StockCount <= 0);
            var lowStockRows = await _db.ProductSizes.CountAsync(x => x.StockCount > 0 && x.StockCount <= 5);

            // ModelCode çeşitliliği
            var modelCodeCount = await _db.Products
                .Where(x => x.ModelCode != null && x.ModelCode != "")
                .Select(x => x.ModelCode)
                .Distinct()
                .CountAsync();

            // Ortalama fiyat (0 ürün için güvenli)
            var avgPrice = productCount == 0 ? 0 : await _db.Products.AverageAsync(x => x.Price);
            var minPrice = productCount == 0 ? 0 : await _db.Products.MinAsync(x => x.Price);
            var maxPrice = productCount == 0 ? 0 : await _db.Products.MaxAsync(x => x.Price);

            // Sipariş durum dağılımı
            var orderByStatus = await _db.Orders
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(x => x.TotalPrice) })
                .ToListAsync();

            // Son 7 gün sipariş trendi (CreatedAt UTC varsayımı)
            var since = DateTime.UtcNow.Date.AddDays(-6);
            var last7 = await _db.Orders
                .Where(x => x.CreatedAt >= since)
                .GroupBy(x => x.CreatedAt.Date)
                .Select(g => new { Day = g.Key, Count = g.Count(), Total = g.Sum(x => x.TotalPrice) })
                .ToListAsync();

            // Kategori -> ürün sayısı (Top 8)
            var topCategories = await _db.Categories
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    ProductCount = c.Products.Count
                })
                .OrderByDescending(x => x.ProductCount)
                .ThenBy(x => x.Name)
                .Take(8)
                .ToListAsync();

            // ModelCode -> ürün sayısı (Top 8)
            var topModelCodes = await _db.Products
                .GroupBy(x => x.ModelCode)
                .Select(g => new { ModelCode = g.Key ?? "(Boş)", Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(8)
                .ToListAsync();

            // Cihaz -> bağlı ürün sayısı (Top 8)
            var topDevices = await _db.ProductDevices
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    ProductCount = d.Products.Count
                })
                .OrderByDescending(x => x.ProductCount)
                .ThenBy(x => x.Name)
                .Take(8)
                .ToListAsync();

            // Cihaza bağlı olmayan ürün sayısı (M2M)
            var productsWithoutDevice = await _db.Products.CountAsync(p => !p.ProductDevices.Any());

            // Resim analizi
            var imageCount = await _db.ImageItems.CountAsync();
            var productsWithNoImage = await _db.Products.CountAsync(p => !p.ImageItems.Any());

            // Eksik veri kontrolü (hızlı “aksiyon” için)
            var missingBarcode = await _db.Products.CountAsync(p => p.Barcode == null || p.Barcode == "");
            var missingModelCode = await _db.Products.CountAsync(p => p.ModelCode == null || p.ModelCode == "");
            var missingCategory = await _db.Products.CountAsync(p => p.CategoryId <= 0);
            var missingPrice = await _db.Products.CountAsync(p => p.Price <= 0);

            // En çok stok taşıyan ürünler (ProductSize topluyoruz)
            var topStockProducts = await _db.Products
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.ModelCode,
                    TotalStock = p.ProductSizes.Sum(s => (int?)s.StockCount) ?? 0,
                    SizeCount = p.ProductSizes.Count
                })
                .OrderByDescending(x => x.TotalStock)
                .ThenBy(x => x.Title)
                .Take(8)
                .ToListAsync();

            // Stok/ürün ortalaması (size satırları bazlı)
            var avgStockPerSize = stockRows == 0 ? 0 : (decimal)totalStock / stockRows;

            // Dashboard VM (dynamic yerine anon. obje yeterli)
            var vm = new
            {
                // Genel
                Products = productCount,
                Categories = categoryCount,
                SubCategories = subCategoryCount,
                Orders = orderCount,
                Users = userCount,
                Devices = deviceCount,
                Images = imageCount,

                // Stok
                TotalStock = totalStock,
                StockRows = stockRows,
                OutOfStockRows = outOfStockRows,
                LowStockRows = lowStockRows,
                AvgStockPerSize = avgStockPerSize,

                // Ürün/Veri kalitesi
                ModelCodes = modelCodeCount,
                ProductsWithoutDevice = productsWithoutDevice,
                ProductsWithNoImage = productsWithNoImage,
                MissingBarcode = missingBarcode,
                MissingModelCode = missingModelCode,
                MissingCategory = missingCategory,
                MissingPrice = missingPrice,

                // Fiyat
                AvgPrice = avgPrice,
                MinPrice = minPrice,
                MaxPrice = maxPrice,

                // Sipariş
                OrderByStatus = orderByStatus,
                Last7 = last7,

                // Top listeler
                TopCategories = topCategories,
                TopDevices = topDevices,
                TopModelCodes = topModelCodes,
                TopStockProducts = topStockProducts
            };

            var notification = new SendMobileNotificationDTO()
            {
                To = _db.Users.FirstOrDefault(x => x.NotificationToken != null).NotificationToken,
                Title = "test",
                Body = "test"
            };
            await new SendExpoNotificationHelper().SendAsync(notification);

            return View(vm);
        }
    }
}