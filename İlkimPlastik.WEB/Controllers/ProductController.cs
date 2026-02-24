using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Controllers
{
    public class ProductController : Controller
    {
        private readonly EfCoreContext _db;
        public ProductController(EfCoreContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Index(
            string? q,
            int? categoryId,
            int? subCategoryId,
            int? deviceId,
            decimal? minPrice,
            decimal? maxPrice,
            bool offerOnly = false,
            string sort = "new",
            int page = 1)
        {
            q = (q ?? "").Trim();
            if (page < 1) page = 1;

            const int pageSize = 9;

            // ============== KATEGORİ + ALT KATEGORİ SAYIMLARI ==============
            // Kategori sayıları (toplam ürün sayısı)
            var categoriesRaw = await _db.Categories.AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(c => new CategoryTreeVm
                {
                    Id = c.Id,
                    Name = c.Name,
                    Count = c.Products.Count,
                    SubCategories = new()
                })
                .ToListAsync();

            var catIds = categoriesRaw.Select(x => x.Id).ToList();

            // Alt kategori sayıları
            var subCounts = await _db.SubCategories.AsNoTracking()
                .Where(s => catIds.Contains(s.CategoryId))
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.CategoryId,
                    Count = s.Products.Count
                })
                .ToListAsync();

            var subMap = subCounts
                .GroupBy(x => x.CategoryId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(x => x.Name)
                          .Select(x => new SubCategoryRowVm { Id = x.Id, Name = x.Name, Count = x.Count })
                          .ToList()
                );

            foreach (var c in categoriesRaw)
            {
                if (subMap.ContainsKey(c.Id))
                    c.SubCategories = subMap[c.Id];
            }

            // ============== CİHAZLAR ==============
            var devices = await _db.ProductDevices.AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            // ============== ÜRÜN SORGUSU (Filtreler) ==============
            var query = _db.Products.AsNoTracking()
                .Include(p => p.ImageItems)
                .Include(p => p.ProductDevices)
                .AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (subCategoryId.HasValue)
                query = query.Where(p => p.SubCategoryId == subCategoryId.Value);

            if (deviceId.HasValue)
                query = query.Where(p => p.ProductDevices.Any(d => d.Id == deviceId.Value));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(qq) ||
                    (p.ModelCode ?? "").ToLower().Contains(qq) ||
                    (p.Barcode ?? "").ToLower().Contains(qq)
                );
            }

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            if (offerOnly)
                query = query.Where(p => p.OfferRate > 0);

            // ============== SIRALAMA ==============
            sort = (sort ?? "new").Trim().ToLowerInvariant();

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price).ThenByDescending(p => p.Id),
                "price_desc" => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.Id),
                "discount_desc" => query.OrderByDescending(p => p.OfferRate).ThenByDescending(p => p.Id),
                "title_asc" => query.OrderBy(p => p.Title).ThenByDescending(p => p.Id),
                _ => query.OrderByDescending(p => p.Id)
            };

            // ============== GLOBAL FİYAT ARALIĞI + GLOBAL COUNT ==============
            var priceStats = await _db.Products.AsNoTracking()
                .GroupBy(x => 1)
                .Select(g => new
                {
                    Min = g.Min(x => x.Price),
                    Max = g.Max(x => x.Price),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync();

            // ============== SAYFALAMA ==============
            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductCardVm
                {
                    Id = p.Id,
                    Title = p.Title,
                    Price = p.Price,
                    OfferRate = p.OfferRate,
                    ImageUrl = p.ImageItems.Select(i => i.Filename).FirstOrDefault()
                })
                .ToListAsync();

            // Seçili adlar (chip'ler için)
            var selectedCatName = categoryId.HasValue ? categoriesRaw.FirstOrDefault(x => x.Id == categoryId.Value)?.Name : null;
            var selectedSubName = subCategoryId.HasValue
                ? subCounts.FirstOrDefault(x => x.Id == subCategoryId.Value)?.Name
                : null;
            var selectedDeviceName = deviceId.HasValue ? devices.FirstOrDefault(x => x.Id == deviceId.Value)?.Name : null;

            var vm = new ProductIndexVm
            {
                Products = products,
                Categories = categoriesRaw,
                Devices = devices,

                Query = q,
                CategoryId = categoryId,
                SubCategoryId = subCategoryId,
                DeviceId = deviceId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                OfferOnly = offerOnly,
                Sort = sort,

                Page = page,
                PageSize = pageSize,
                Total = total,
                TotalPages = totalPages,

                GlobalMinPrice = priceStats?.Min ?? 0,
                GlobalMaxPrice = priceStats?.Max ?? 0,
                GlobalCount = priceStats?.Count ?? 0,

                SelectedCategoryName = selectedCatName ?? "",
                SelectedSubCategoryName = selectedSubName ?? "",
                SelectedDeviceName = selectedDeviceName ?? ""
            };

            return View(vm);
        }

        // HEADER ARAMA
        [HttpGet]
        public IActionResult Search(string? q)
        {
            return RedirectToAction(nameof(Index), new { q });
        }

        // DETAY
        [HttpGet("/product/details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var item = await _db.Products.AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.ImageItems)
                .Include(p => p.ProductSizes)
                .Include(p => p.ProductFeatures)
                .Include(p => p.ProductDevices)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (item == null)
                return NotFound();

            // AYNI MODEL KODU ALTERNATİFLERİ
            var alternatives = new List<Product>();
            if (!string.IsNullOrWhiteSpace(item.ModelCode))
            {
                alternatives = await _db.Products.AsNoTracking()
                    .Include(p => p.ImageItems)
                    .Where(p => p.ModelCode == item.ModelCode && p.Id != item.Id)
                    .OrderByDescending(p => p.Id)
                    .ToListAsync();
            }

            var latest = await _db.Products.AsNoTracking()
                .Include(p => p.ImageItems)
                .OrderByDescending(p => p.Id)
                .Take(4)
                .ToListAsync();

            ViewBag.Alternatives = alternatives;
            ViewBag.Latest = latest;

            return View(item);
        }

        // ==========================
        // VIEW MODELS
        // ==========================

        public class ProductIndexVm
        {
            public List<ProductCardVm> Products { get; set; } = new();

            // ✅ kategori + alt kategori ağacı
            public List<CategoryTreeVm> Categories { get; set; } = new();

            public List<ProductDevice> Devices { get; set; } = new();

            public string? Query { get; set; }
            public int? CategoryId { get; set; }
            public int? SubCategoryId { get; set; }
            public int? DeviceId { get; set; }
            public decimal? MinPrice { get; set; }
            public decimal? MaxPrice { get; set; }
            public bool OfferOnly { get; set; }
            public string Sort { get; set; } = "new";

            public int Page { get; set; }
            public int PageSize { get; set; }
            public int Total { get; set; }
            public int TotalPages { get; set; }

            public decimal GlobalMinPrice { get; set; }
            public decimal GlobalMaxPrice { get; set; }
            public int GlobalCount { get; set; }

            // chip'ler için
            public string SelectedCategoryName { get; set; } = "";
            public string SelectedSubCategoryName { get; set; } = "";
            public string SelectedDeviceName { get; set; } = "";
        }

        public class ProductCardVm
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public decimal Price { get; set; }
            public int OfferRate { get; set; }
            public string? ImageUrl { get; set; }
        }

        public class CategoryTreeVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public List<SubCategoryRowVm> SubCategories { get; set; } = new();
        }

        public class SubCategoryRowVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int Count { get; set; }
        }
    }
}
