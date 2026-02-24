
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class DeviceController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        public DeviceController(EfCoreContext db) => _db = db;

        public async Task<IActionResult> Index(int? editId = null, int? viewId = null)
        {
            // Cihaz listesi + bağlı ürün sayısı
            var devices = await _db.ProductDevices.AsNoTracking()
                .Select(d => new DeviceRowVm
                {
                    Id = d.Id,
                    Name = d.Name,
                    ProductCount = d.Products.Count
                })
                .OrderByDescending(x => x.ProductCount)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            ProductDevice? edit = null;
            if (editId.HasValue)
                edit = await _db.ProductDevices.FindAsync(editId.Value);

            // Detay paneli: seçilen cihaz (viewId) veya edit edilen cihaz
            int? selectedId = viewId ?? editId;
            DeviceDetailVm? detail = null;

            if (selectedId.HasValue)
            {
                var dev = await _db.ProductDevices.AsNoTracking()
                    .Include(x => x.Products)
                        .ThenInclude(p => p.Category)
                    .Include(x => x.Products)
                        .ThenInclude(p => p.SubCategory)
                    .Include(x => x.Products)
                        .ThenInclude(p => p.ProductSizes)
                    .Include(x => x.Products)
                        .ThenInclude(p => p.ImageItems)
                    .FirstOrDefaultAsync(x => x.Id == selectedId.Value);

                if (dev != null)
                {
                    var products = dev.Products
                        .OrderByDescending(p => p.Id)
                        .Select(p => new DeviceProductVm
                        {
                            Id = p.Id,
                            Title = p.Title,
                            ModelCode = p.ModelCode,
                            Barcode = p.Barcode,
                            Price = p.Price,
                            Category = p.Category?.Name,
                            SubCategory = p.SubCategory?.Name,
                            TotalStock = p.ProductSizes?.Sum(s => s.StockCount) ?? 0,
                            SizeRowCount = p.ProductSizes?.Count ?? 0,
                            HasImage = p.ImageItems != null && p.ImageItems.Any()
                        })
                        .ToList();

                    var totalStock = products.Sum(x => x.TotalStock);
                    var totalSizeRows = products.Sum(x => x.SizeRowCount);
                    var noImageCount = products.Count(x => !x.HasImage);

                    detail = new DeviceDetailVm
                    {
                        Id = dev.Id,
                        Name = dev.Name,
                        ProductCount = products.Count,
                        TotalStock = totalStock,
                        TotalSizeRows = totalSizeRows,
                        NoImageProducts = noImageCount,
                        AvgPrice = products.Count == 0 ? 0 : products.Average(x => x.Price),
                        Products = products
                    };
                }
            }

            // Genel özet
            var totalDevices = devices.Count;
            var devicesWithNoProduct = devices.Count(x => x.ProductCount == 0);
            var totalLinkedProducts = devices.Sum(x => x.ProductCount);

            var vm = new DeviceIndexVm
            {
                Devices = devices,
                TotalDevices = totalDevices,
                DevicesWithNoProduct = devicesWithNoProduct,
                TotalLinkedProducts = totalLinkedProducts,
                SelectedId = selectedId,
                Detail = detail
            };

            ViewBag.Edit = edit;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(ProductDevice model)
        {
            var name = (model.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ERR"] = "Cihaz adı zorunlu.";
                return RedirectToAction(nameof(Index), new { editId = model.Id > 0 ? model.Id : (int?)null });
            }

            // Aynı isim kontrol (case-insensitive)
            var exists = await _db.ProductDevices.AnyAsync(x => x.Id != model.Id && x.Name.ToLower() == name.ToLower());
            if (exists)
            {
                TempData["ERR"] = "Bu cihaz adı zaten var.";
                return RedirectToAction(nameof(Index), new { editId = model.Id > 0 ? model.Id : (int?)null });
            }

            if (model.Id == 0)
            {
                _db.ProductDevices.Add(new ProductDevice { Name = name });
                TempData["OK"] = "Cihaz eklendi.";
            }
            else
            {
                var item = await _db.ProductDevices.FindAsync(model.Id);
                if (item == null)
                {
                    TempData["ERR"] = "Kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                item.Name = name;
                TempData["OK"] = "Cihaz güncellendi.";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.ProductDevices
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
            {
                TempData["ERR"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (item.Products.Any())
            {
                TempData["ERR"] = "Bu cihaz bazı ürünlere bağlı. Önce ürünlerden kaldır.";
                return RedirectToAction(nameof(Index), new { viewId = id });
            }

            _db.ProductDevices.Remove(item);
            await _db.SaveChangesAsync();

            TempData["OK"] = "Cihaz silindi.";
            return RedirectToAction(nameof(Index));
        }

        // ------- VMs (tek dosya kolaylığı için burada) -------
        public class DeviceIndexVm
        {
            public List<DeviceRowVm> Devices { get; set; } = new();
            public int TotalDevices { get; set; }
            public int DevicesWithNoProduct { get; set; }
            public int TotalLinkedProducts { get; set; }
            public int? SelectedId { get; set; }
            public DeviceDetailVm? Detail { get; set; }
        }

        public class DeviceRowVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int ProductCount { get; set; }
        }

        public class DeviceDetailVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int ProductCount { get; set; }
            public int TotalStock { get; set; }
            public int TotalSizeRows { get; set; }
            public int NoImageProducts { get; set; }
            public decimal AvgPrice { get; set; }
            public List<DeviceProductVm> Products { get; set; } = new();
        }

        public class DeviceProductVm
        {
            public int Id { get; set; }
            public string Title { get; set; } = "";
            public string? ModelCode { get; set; }
            public string? Barcode { get; set; }
            public decimal Price { get; set; }
            public string? Category { get; set; }
            public string? SubCategory { get; set; }
            public int TotalStock { get; set; }
            public int SizeRowCount { get; set; }
            public bool HasImage { get; set; }
        }
    }
}