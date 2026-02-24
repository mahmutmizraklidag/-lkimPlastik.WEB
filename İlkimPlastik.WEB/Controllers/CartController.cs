// ===============================
// Controllers/CartController.cs
// (GÜNCEL - İndirim + Toplam İndirim Hesabı Dahil)
// ===============================

using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ilkimPlastik.WEB.Controllers
{
    public class CartController : Controller
    {
        private readonly EfCoreContext _db;
        private const string CART_KEY = "CART_V1";

        public CartController(EfCoreContext db) => _db = db;

        // OfferRate clamp + fiyat hesabı
        private static int ClampOffer(int pct)
        {
            if (pct < 0) pct = 0;
            if (pct > 95) pct = 95;
            return pct;
        }

        private static decimal Round2(decimal v) => decimal.Round(v, 2);

        private static (decimal oldUnit, decimal newUnit, int pct) CalcOffer(decimal price, int pct)
        {
            var oldUnit = Round2(price);
            pct = ClampOffer(pct);

            if (pct <= 0) return (oldUnit, oldUnit, 0);

            var newUnit = Round2(price * (1m - (pct / 100m)));
            if (newUnit < 0m) newUnit = 0m;

            return (oldUnit, newUnit, pct);
        }

        [HttpGet("/cart")]
        public async Task<IActionResult> Index()
        {
            var cart = GetCart();

            var productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();
            var sizeIds = cart.Items.Select(x => x.SizeId).Distinct().ToList();

            var products = await _db.Products.AsNoTracking()
                .Include(p => p.ImageItems)
                .Include(p => p.ProductSizes)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var items = new List<CartLineVm>();
            foreach (var ci in cart.Items)
            {
                var p = products.FirstOrDefault(x => x.Id == ci.ProductId);
                if (p == null) continue;

                var size = p.ProductSizes.FirstOrDefault(s => s.Id == ci.SizeId);
                var sizeName = size?.Name ?? "-";
                var img = p.ImageItems.FirstOrDefault()?.Filename;

                // ✅ indirimli birim fiyat
                var (oldUnit, newUnit, pct) = CalcOffer(p.Price, p.OfferRate);

                // satır
                var oldLineTotal = Round2(oldUnit * ci.Quantity);
                var lineTotal = Round2(newUnit * ci.Quantity);
                var lineDiscount = Round2(oldLineTotal - lineTotal);

                items.Add(new CartLineVm
                {
                    ProductId = p.Id,
                    SizeId = ci.SizeId,
                    Title = p.Title,
                    ModelCode = p.ModelCode,
                    Barcode = p.Barcode,
                    SizeName = sizeName,
                    ImageFile = img,

                    OfferRate = pct,
                    OldUnitPrice = oldUnit,
                    UnitPrice = newUnit,

                    Quantity = ci.Quantity,
                    OldLineTotal = oldLineTotal,
                    LineTotal = lineTotal,
                    LineDiscount = lineDiscount
                });
            }

            // ✅ toplamlar
            var subTotalBeforeDiscount = Round2(items.Sum(x => x.OldLineTotal));
            var subTotal = Round2(items.Sum(x => x.LineTotal));
            var discountTotal = Round2(items.Sum(x => x.LineDiscount));

            // İlişkili ürünler: sepetteki ürünlerin model kodlarına göre (kendileri hariç)
            var modelCodes = items.Select(x => x.ModelCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

            var related = new List<RelatedProductVm>();
            if (modelCodes.Any())
            {
                related = await _db.Products.AsNoTracking()
                    .Include(p => p.ImageItems)
                    .Where(p => modelCodes.Contains(p.ModelCode))
                    .OrderByDescending(p => p.Id)
                    .Take(8)
                    .Select(p => new RelatedProductVm
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Price = p.Price,
                        OfferRate = ClampOffer(p.OfferRate),
                        ImageFile = p.ImageItems.Select(i => i.Filename).FirstOrDefault()
                    })
                    .ToListAsync();

                related = related.Where(r => !productIds.Contains(r.Id)).ToList();
            }

            var vm = new CartIndexVm
            {
                Items = items,

                SubTotalBeforeDiscount = subTotalBeforeDiscount,
                DiscountTotal = discountTotal,

                SubTotal = subTotal,
                ShippingLabel = "Ücretsiz",
                ShippingCost = 0,
                GrandTotal = Round2(subTotal + 0),

                Related = related
            };

            return View(vm);
        }

        [HttpPost("/cart/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, int sizeId, int quantity, string? returnUrl = null)
        {
            if (quantity < 1) quantity = 1;

            var product = await _db.Products.AsNoTracking()
                .Include(p => p.ProductSizes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                TempData["ERR"] = "Ürün bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var size = product.ProductSizes.FirstOrDefault(s => s.Id == sizeId);
            if (size == null)
            {
                TempData["ERR"] = "Beden/ölçü seçimi geçersiz.";
                return RedirectToAction(nameof(Index));
            }

            if (size.StockCount <= 0)
            {
                TempData["ERR"] = "Seçtiğiniz beden/ölçü için stok bulunmuyor.";
                return RedirectToAction(nameof(Index));
            }

            if (quantity > size.StockCount) quantity = size.StockCount;

            var cart = GetCart();
            var line = cart.Items.FirstOrDefault(x => x.ProductId == productId && x.SizeId == sizeId);

            if (line == null)
                cart.Items.Add(new CartItemDto { ProductId = productId, SizeId = sizeId, Quantity = quantity });
            else
            {
                var newQty = line.Quantity + quantity;
                if (newQty > size.StockCount) newQty = size.StockCount;
                line.Quantity = newQty;
            }

            SaveCart(cart);

            TempData["OK"] = "Ürün sepete eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/cart/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(List<CartUpdateDto> items)
        {
            var cart = GetCart();
            if (items == null) items = new();

            var productIds = items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _db.Products.AsNoTracking()
                .Include(p => p.ProductSizes)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            foreach (var u in items)
            {
                var line = cart.Items.FirstOrDefault(x => x.ProductId == u.ProductId && x.SizeId == u.SizeId);
                if (line == null) continue;

                var p = products.FirstOrDefault(x => x.Id == u.ProductId);
                var s = p?.ProductSizes.FirstOrDefault(z => z.Id == u.SizeId);

                if (s == null || s.StockCount <= 0)
                {
                    cart.Items.Remove(line);
                    continue;
                }

                var qty = u.Quantity < 1 ? 1 : u.Quantity;
                if (qty > s.StockCount) qty = s.StockCount;
                line.Quantity = qty;
            }

            SaveCart(cart);

            TempData["OK"] = "Sepet güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/cart/remove")]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productId, int sizeId)
        {
            var cart = GetCart();
            var line = cart.Items.FirstOrDefault(x => x.ProductId == productId && x.SizeId == sizeId);
            if (line != null) cart.Items.Remove(line);
            SaveCart(cart);

            TempData["OK"] = "Ürün sepetten kaldırıldı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("/cart/mini/setqty")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MiniSetQty(int productId, int sizeId, int quantity)
        {
            var cart = GetCart();

            var line = cart.Items.FirstOrDefault(x => x.ProductId == productId && x.SizeId == sizeId);
            if (line == null) return Json(new { ok = false });

            var product = await _db.Products.AsNoTracking()
                .Include(p => p.ProductSizes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            var size = product?.ProductSizes.FirstOrDefault(s => s.Id == sizeId);
            if (product == null || size == null || size.StockCount <= 0)
            {
                cart.Items.Remove(line);
                SaveCart(cart);
                var (count0, total0) = await MiniTotals(cart);
                return Json(new { ok = true, removed = true, count = count0, total = total0 });
            }

            if (quantity <= 0)
            {
                cart.Items.Remove(line);
                SaveCart(cart);
                var (count1, total1) = await MiniTotals(cart);
                return Json(new { ok = true, removed = true, count = count1, total = total1 });
            }

            if (quantity > size.StockCount) quantity = size.StockCount;
            line.Quantity = quantity;

            SaveCart(cart);

            var (count, total) = await MiniTotals(cart);
            return Json(new { ok = true, removed = false, qty = line.Quantity, count, total });
        }

        [HttpPost("/cart/mini/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MiniRemove(int productId, int sizeId)
        {
            var cart = GetCart();
            var line = cart.Items.FirstOrDefault(x => x.ProductId == productId && x.SizeId == sizeId);
            if (line != null) cart.Items.Remove(line);
            SaveCart(cart);

            var (count, total) = await MiniTotals(cart);
            return Json(new { ok = true, count, total });
        }

        private async Task<(int count, decimal total)> MiniTotals(CartDto cart)
        {
            var productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();

            var products = await _db.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Price, p.OfferRate })
                .ToListAsync();

            int count = 0;
            decimal total = 0;

            foreach (var it in cart.Items)
            {
                var p = products.FirstOrDefault(x => x.Id == it.ProductId);
                if (p == null) continue;

                var (oldUnit, newUnit, pct) = CalcOffer(p.Price, p.OfferRate);

                count += it.Quantity;
                total += (newUnit * it.Quantity);
            }

            total = Round2(total);
            return (count, total);
        }

        [HttpGet("/product/suggest")]
        public async Task<IActionResult> Suggest(string? q)
        {
            q = (q ?? "").Trim();
            if (q.Length < 2) return Json(Array.Empty<object>());

            var qq = q.ToLowerInvariant();
            var like = "%" + qq + "%";
            var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

            var list = await _db.Products
                .AsNoTracking()
                .Where(p =>
                    EF.Functions.Like(p.Title.ToLower(), like) ||
                    EF.Functions.Like((p.ModelCode ?? "").ToLower(), like) ||
                    EF.Functions.Like((p.Barcode ?? "").ToLower(), like)
                )
                .OrderByDescending(p => p.Id)
                .Take(6)
                .Select(p => new
                {
                    id = p.Id,
                    title = p.Title,
                    price = p.Price,
                    priceText = p.Price.ToString("N2", tr),
                    imageUrl =
                        p.ImageItems
                            .Select(i => i.Filename)
                            .Where(fn => fn != null && fn != "")
                            .Select(fn => "/uploads/products/" + fn.TrimStart('/'))
                            .FirstOrDefault()
                        ?? "/images/product-9.jpg"
                })
                .ToListAsync();

            return Json(list);
        }

        // ---------------- Session helpers ----------------
        private CartDto GetCart()
        {
            var json = HttpContext.Session.GetString(CART_KEY);
            if (string.IsNullOrWhiteSpace(json)) return new CartDto();
            try
            {
                return JsonSerializer.Deserialize<CartDto>(json) ?? new CartDto();
            }
            catch
            {
                return new CartDto();
            }
        }

        private void SaveCart(CartDto cart)
        {
            var json = JsonSerializer.Serialize(cart);
            HttpContext.Session.SetString(CART_KEY, json);
        }

        // ---------------- DTO / VM ----------------
        public class CartDto
        {
            public List<CartItemDto> Items { get; set; } = new();
        }

        public class CartItemDto
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public int Quantity { get; set; }
        }

        public class CartUpdateDto
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public int Quantity { get; set; }
        }

        public class CartIndexVm
        {
            public List<CartLineVm> Items { get; set; } = new();

            // ✅ indirim toplamları
            public decimal SubTotalBeforeDiscount { get; set; }
            public decimal DiscountTotal { get; set; }

            // mevcut alanlar
            public decimal SubTotal { get; set; }               // indirimli ara toplam
            public string ShippingLabel { get; set; } = "Ücretsiz";
            public decimal ShippingCost { get; set; }
            public decimal GrandTotal { get; set; }
            public List<RelatedProductVm> Related { get; set; } = new();
        }

        public class CartLineVm
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public string Title { get; set; } = "";
            public string? ModelCode { get; set; }
            public string? Barcode { get; set; }
            public string SizeName { get; set; } = "-";
            public string? ImageFile { get; set; }

            public int OfferRate { get; set; }                  // ✅ yüzde
            public decimal OldUnitPrice { get; set; }           // ✅ indirimsiz birim
            public decimal UnitPrice { get; set; }              // ✅ indirimli birim

            public int Quantity { get; set; }

            public decimal OldLineTotal { get; set; }           // ✅ indirimsiz satır toplam
            public decimal LineTotal { get; set; }              // ✅ indirimli satır toplam
            public decimal LineDiscount { get; set; }           // ✅ satır indirimi
        }

        public class RelatedProductVm
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public decimal Price { get; set; }
            public int OfferRate { get; set; } // ✅ badge için
            public string? ImageFile { get; set; }
        }
    }
}
