using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ilkimPlastik.WEB.ViewComponents
{
    public class HeaderViewComponent : ViewComponent
    {
        private readonly EfCoreContext _db;
        private readonly IMemoryCache _cache;
        private const string CART_KEY = "CART_V1";
        private const string SETTINGS_CACHE_KEY = "SITE_SETTINGS_V1";

        public HeaderViewComponent(EfCoreContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var cart = GetCart();

            // ✅ SiteSettings (tek kayıt) - cache
            var settings = await GetSiteSettingsCached();

            // ✅ Kategoriler
            var categories = await _db.Categories.AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            var catIds = categories.Select(x => x.Id).ToList();

            // ✅ Alt kategoriler
            var subs = await _db.SubCategories.AsNoTracking()
                .Where(s => catIds.Contains(s.CategoryId))
                .OrderBy(s => s.Name)
                .ToListAsync();

            var subMap = subs
                .GroupBy(s => s.CategoryId)
                .ToDictionary(g => g.Key, g => g
                    .Select(s => new HeaderSubCategoryVm { Id = s.Id, Name = s.Name })
                    .ToList()
                );

            var catVm = categories.Select(c => new HeaderCategoryVm
            {
                Id = c.Id,
                Name = c.Name,
                SubCategories = subMap.ContainsKey(c.Id) ? subMap[c.Id] : new List<HeaderSubCategoryVm>()
            }).ToList();

            // Sepet ürünleri
            var productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();

            var products = await _db.Products.AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.ImageItems)
                .Include(p => p.ProductSizes)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var items = new List<HeaderCartItemVm>();

            foreach (var ci in cart.Items)
            {
                var p = products.FirstOrDefault(x => x.Id == ci.ProductId);
                if (p == null) continue;

                var size = p.ProductSizes.FirstOrDefault(s => s.Id == ci.SizeId);
                if (size == null) continue;

                var img = p.ImageItems.FirstOrDefault()?.Filename;
                var qty = ci.Quantity < 1 ? 1 : ci.Quantity;

                if (size.StockCount <= 0) continue;
                if (qty > size.StockCount) qty = size.StockCount;

                var unit = p.Price;
                items.Add(new HeaderCartItemVm
                {
                    ProductId = p.Id,
                    SizeId = size.Id,
                    Title = p.Title,
                    SizeName = size.Name,
                    ImageFile = img,
                    UnitPrice = unit,
                    Quantity = qty,
                    LineTotal = unit * qty
                });
            }

            var vm = new HeaderVm
            {
                Settings = settings, // ✅
                Categories = catVm,
                CartItems = items,
                CartCount = items.Sum(x => x.Quantity),
                CartTotal = items.Sum(x => x.LineTotal)
            };

            // Sepet normalize edip session güncelle
            var normalized = new CartDto
            {
                Items = items.Select(x => new CartItemDto
                {
                    ProductId = x.ProductId,
                    SizeId = x.SizeId,
                    Quantity = x.Quantity
                }).ToList()
            };
            SaveCart(normalized);

            return View(vm);
        }

        private async Task<SiteSettings?> GetSiteSettingsCached()
        {
            // 1 dakikalık cache (istersen 5 dk yap)
            if (_cache.TryGetValue(SETTINGS_CACHE_KEY, out SiteSettings? cached) && cached != null)
                return cached;

            var s = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            _cache.Set(SETTINGS_CACHE_KEY, s, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
            });
            return s;
        }

        private CartDto GetCart()
        {
            var json = HttpContext.Session.GetString(CART_KEY);
            if (string.IsNullOrWhiteSpace(json)) return new CartDto();
            try { return JsonSerializer.Deserialize<CartDto>(json) ?? new CartDto(); }
            catch { return new CartDto(); }
        }

        private void SaveCart(CartDto cart)
        {
            var json = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CART_KEY, json);
        }

        public class HeaderVm
        {
            public SiteSettings? Settings { get; set; } // ✅ eklendi
            public List<HeaderCategoryVm> Categories { get; set; } = new();
            public List<HeaderCartItemVm> CartItems { get; set; } = new();
            public int CartCount { get; set; }
            public decimal CartTotal { get; set; }
        }

        public class HeaderCategoryVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public List<HeaderSubCategoryVm> SubCategories { get; set; } = new();
        }

        public class HeaderSubCategoryVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class HeaderCartItemVm
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public string Title { get; set; } = "";
            public string SizeName { get; set; } = "-";
            public string? ImageFile { get; set; }
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public decimal LineTotal { get; set; }
        }

        private class CartDto
        {
            public List<CartItemDto> Items { get; set; } = new();
        }

        private class CartItemDto
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public int Quantity { get; set; }
        }
    }
}