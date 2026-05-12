using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class ProductController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        private readonly IWebHostEnvironment _env;

        private const int MaxOfferRate = 95;

        public ProductController(EfCoreContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // ------------------------------ INDEX ------------------------------
        public async Task<IActionResult> Index(int? editId = null)
        {
            var list = await _db.Products.AsNoTracking()
     .Include(x => x.Category)
     .Include(x => x.SubCategory)
     .Include(x => x.ProductSizes)
     .Include(x => x.ImageItems)
     .OrderByDescending(x => x.Id)
     .ToListAsync();

            Product? edit = null;
            if (editId.HasValue)
            {
                edit = await _db.Products
                    .Include(x => x.ImageItems)
                    .Include(x => x.ProductFeatures)
                    .Include(x => x.ProductSizes)
                    .Include(x => x.ProductDevices)
                    .FirstOrDefaultAsync(x => x.Id == editId.Value);
            }

            if (ViewBag.Form == null) ViewBag.Edit = edit;

            await FillLookupViewBags();
            return View(list);
        }

        // ------------------------------ LOOKUPS ------------------------------
        [HttpGet]
        public async Task<IActionResult> GetSubCategories(int categoryId)
        {
            var subs = await _db.SubCategories.AsNoTracking()
                .Where(x => x.CategoryId == categoryId)
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name })
                .ToListAsync();

            return Json(subs);
        }

        private async Task FillLookupViewBags()
        {
            ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
            ViewBag.SubCategories = await _db.SubCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
            ViewBag.Devices = await _db.ProductDevices.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        }

        // ------------------------------ QUICK ADD DEVICE ------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickAddDevice(string deviceName, int? backEditId)
        {
            deviceName = (deviceName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                TempData["ERR"] = "Cihaz adı zorunlu.";
                return RedirectToAction(nameof(Index), new { editId = backEditId });
            }

            var exists = await _db.ProductDevices.AnyAsync(x => x.Name.ToLower() == deviceName.ToLower());
            if (!exists)
            {
                _db.ProductDevices.Add(new ProductDevice { Name = deviceName });
                await _db.SaveChangesAsync();
                TempData["OK"] = "Yeni cihaz eklendi.";
            }
            else
            {
                TempData["OK"] = "Cihaz zaten var.";
            }

            return RedirectToAction(nameof(Index), new { editId = backEditId });
        }

        // ------------------------------ QUICK DISCOUNT ------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDiscount(int id, int OfferRate, int? backEditId)
        {
            OfferRate = Clamp(OfferRate, 0, MaxOfferRate);

            var item = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                TempData["ERR"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index), new { editId = backEditId });
            }

            item.OfferRate = OfferRate;
            await _db.SaveChangesAsync();

            TempData["OK"] = OfferRate > 0
                ? $"İndirim tanımlandı: %{OfferRate}"
                : "İndirim kaldırıldı.";

            return RedirectToAction(nameof(Index), new { editId = backEditId ?? id });
        }

        // ------------------------------ UPSERT ------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(
            Product model,
            int[]? deviceIds,
            int[]? deleteImageIds,
           List<IFormFile>? newImages,
string? newImageOrder,
string? existingImageOrder,
string[]? featureNames,
            string[]? featureDescriptions,
            string[]? sizeNames,
            int[]? sizeStocks)
        {



            model.OfferRate = Clamp(model.OfferRate, 0, MaxOfferRate);

            ValidateProductModel(model);

            if (ModelState.IsValid && model.SubCategoryId.HasValue)
            {
                var ok = await _db.SubCategories.AnyAsync(s => s.Id == model.SubCategoryId.Value && s.CategoryId == model.CategoryId);
                if (!ok) ModelState.AddModelError(nameof(model.SubCategoryId), "Alt kategori seçimi geçersiz.");
            }

            if (!ModelState.IsValid)
            {
                var list = await _db.Products.AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.SubCategory)
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

                await PrepareIndexForErrorReturn(model, deviceIds, featureNames, featureDescriptions, sizeNames, sizeStocks);
                await FillLookupViewBags();

                return View("Index", list);
            }

            // CREATE
            if (model.Id == 0)
            {
                if (await _db.Products.AnyAsync(p => p.Slug == model.Slug))
                {
                    TempData["ERR"] = "Bu slug zaten kullanılıyor..";
                    return RedirectToAction(nameof(Index));
                }
                var newItem = new Product
                {
                    Title = model.Title.Trim(),
                    Description = model.Description,
                    Keywords = model.Keywords,
                    Barcode = (model.Barcode ?? "").Trim(),
                    ModelCode = (model.ModelCode ?? "").Trim(),
                    AverageDeliveryTime = model.AverageDeliveryTime,
                    IsFeatured = model.IsFeatured,
                    MinimumOrderQuantity = model.MinimumOrderQuantity,
                    Price = model.Price,
                    CategoryId = model.CategoryId,
                    SubCategoryId = model.SubCategoryId,
                    OfferRate = Clamp(model.OfferRate, 0, MaxOfferRate),
                    Slug = string.IsNullOrWhiteSpace(model.Slug) ? null : model.Slug.Trim()
                };

                newItem.ProductDevices = await LoadDevices(deviceIds);
                newItem.ProductFeatures = BuildFeatures(featureNames, featureDescriptions);
                newItem.ProductSizes = BuildSizes(sizeNames, sizeStocks);

                _db.Products.Add(newItem);
                await _db.SaveChangesAsync();

                if (newImages != null && newImages.Count > 0)
                {
                    var orderedImages = ApplyImageOrder(newImages, newImageOrder);
                    var uploaded = await SaveImagesAndCreateImageItems(orderedImages);

                    foreach (var img in uploaded)
                        newItem.ImageItems.Add(img);

                    await _db.SaveChangesAsync();
                }

                TempData["OK"] = "Ürün eklendi.";
                return RedirectToAction(nameof(Index), new { editId = newItem.Id });
            }

            // UPDATE
            if (await _db.Products
    .AnyAsync(p => p.Slug == model.Slug && p.Id != model.Id))
            {
                
                TempData["ERR"] = "Bu slug zaten kullanılıyor..";
                return RedirectToAction(nameof(Index));
            }
            var item = await _db.Products
                .Include(x => x.ProductDevices)
                .Include(x => x.ImageItems)
                .Include(x => x.ProductFeatures)
                .Include(x => x.ProductSizes)
                .FirstOrDefaultAsync(x => x.Id == model.Id);

            if (item == null)
            {
                TempData["ERR"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            item.Title = model.Title.Trim();
            item.Description = model.Description;
            item.Keywords = model.Keywords;
            item.Barcode = (model.Barcode ?? "").Trim();
            item.ModelCode = (model.ModelCode ?? "").Trim();
            item.Price = model.Price;
            item.AverageDeliveryTime = model.AverageDeliveryTime;
            item.IsFeatured = model.IsFeatured;
            item.MinimumOrderQuantity = model.MinimumOrderQuantity;
            item.CategoryId = model.CategoryId;
            item.SubCategoryId = model.SubCategoryId;
            item.OfferRate = Clamp(model.OfferRate, 0, MaxOfferRate);
            item.Slug = model.Slug;

            item.ProductDevices.Clear();
            var devs = await LoadDevices(deviceIds);
            foreach (var d in devs) item.ProductDevices.Add(d);

            _db.ProductFeatures.RemoveRange(item.ProductFeatures);
            item.ProductFeatures = BuildFeatures(featureNames, featureDescriptions);

            _db.ProductSizes.RemoveRange(item.ProductSizes);
            item.ProductSizes = BuildSizes(sizeNames, sizeStocks);

            if (deleteImageIds != null && deleteImageIds.Length > 0)
                await RemoveImagesFromProduct(item, deleteImageIds);

            ApplyExistingImageOrder(item, existingImageOrder);

            if (newImages != null && newImages.Count > 0)
            {
                var nextOrder = item.ImageItems.Any()
                    ? item.ImageItems.Max(x => x.DisplayOrder) + 1
                    : 1;

                var orderedImages = ApplyImageOrder(newImages, newImageOrder);
                var uploaded = await SaveImagesAndCreateImageItems(orderedImages, nextOrder);

                foreach (var img in uploaded)
                    item.ImageItems.Add(img);
            }

            await _db.SaveChangesAsync();

            TempData["OK"] = "Ürün güncellendi.";
            return RedirectToAction(nameof(Index), new { editId = item.Id });
        }

        private void ValidateProductModel(Product model)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "Başlık zorunlu.");

            if (model.Price < 0)
                ModelState.AddModelError(nameof(model.Price), "Fiyat negatif olamaz.");

            if (model.CategoryId <= 0)
                ModelState.AddModelError(nameof(model.CategoryId), "Kategori seçilmeli.");

            if (string.IsNullOrWhiteSpace(model.Barcode))
                ModelState.AddModelError(nameof(model.Barcode), "Barcode zorunlu.");

            if (string.IsNullOrWhiteSpace(model.ModelCode))
                ModelState.AddModelError(nameof(model.ModelCode), "ModelCode zorunlu.");

            if (model.OfferRate < 0 || model.OfferRate > MaxOfferRate)
                ModelState.AddModelError(nameof(model.OfferRate), $"İndirim oranı 0–{MaxOfferRate} arasında olmalı.");
        }

        private async Task<List<ProductDevice>> LoadDevices(int[]? deviceIds)
        {
            if (deviceIds == null || deviceIds.Length == 0) return new List<ProductDevice>();
            return await _db.ProductDevices.Where(x => deviceIds.Contains(x.Id)).ToListAsync();
        }

        // ------------------------------ DELETE ------------------------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Products
                .Include(x => x.ProductFeatures)
                .Include(x => x.ProductSizes)
                .Include(x => x.ProductDevices)
                .Include(x => x.ImageItems)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
            {
                TempData["ERR"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            _db.ProductFeatures.RemoveRange(item.ProductFeatures);
            _db.ProductSizes.RemoveRange(item.ProductSizes);

            item.ProductDevices.Clear();
            item.ImageItems.Clear();

            _db.Products.Remove(item);
            await _db.SaveChangesAsync();

            TempData["OK"] = "Ürün silindi.";
            return RedirectToAction(nameof(Index));
        }

        // ================= Helpers =================
        private async Task PrepareIndexForErrorReturn(
            Product form,
            int[]? deviceIds,
            string[]? featureNames,
            string[]? featureDescriptions,
            string[]? sizeNames,
            int[]? sizeStocks)
        {
            ViewBag.Form = form;
            ViewBag.SelectedDeviceIds = (deviceIds ?? Array.Empty<int>()).ToHashSet();
            ViewBag.FeatureRows = BuildFeatureRows(featureNames, featureDescriptions);
            ViewBag.SizeRows = BuildSizeRows(sizeNames, sizeStocks);

            if (form.Id > 0)
            {
                var edit = await _db.Products
                    .Include(x => x.ImageItems)
                    .Include(x => x.ProductDevices)
                    .Include(x => x.ProductFeatures)
                    .Include(x => x.ProductSizes)
                    .FirstOrDefaultAsync(x => x.Id == form.Id);

                ViewBag.Edit = edit;
            }
        }

        private static List<(string name, string? desc)> BuildFeatureRows(string[]? names, string[]? descs)
        {
            var rows = new List<(string, string?)>();
            if (names == null || names.Length == 0) return rows;

            for (int i = 0; i < names.Length; i++)
            {
                var n = (names[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(n)) continue;

                var d = (descs != null && i < descs.Length) ? (descs[i] ?? "").Trim() : null;
                if (string.IsNullOrWhiteSpace(d)) d = null;

                rows.Add((n, d));
            }
            return rows;
        }

        private static List<(string name, int stock)> BuildSizeRows(string[]? names, int[]? stocks)
        {
            var rows = new List<(string, int)>();
            if (names == null || names.Length == 0) return rows;

            for (int i = 0; i < names.Length; i++)
            {
                var n = (names[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(n)) continue;

                var st = (stocks != null && i < stocks.Length) ? stocks[i] : 0;
                if (st < 0) st = 0;

                rows.Add((n, st));
            }
            return rows;
        }

        private static List<ProductFeature> BuildFeatures(string[]? names, string[]? descs)
        {
            var list = new List<ProductFeature>();
            if (names == null || names.Length == 0) return list;

            for (int i = 0; i < names.Length; i++)
            {
                var n = (names[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(n)) continue;

                var d = (descs != null && i < descs.Length) ? (descs[i] ?? "").Trim() : null;
                if (string.IsNullOrWhiteSpace(d)) d = null;

                list.Add(new ProductFeature { Name = n, Description = d });
            }
            return list;
        }

        private static List<ProductSize> BuildSizes(string[]? names, int[]? stocks)
        {
            var list = new List<ProductSize>();
            if (names == null || names.Length == 0) return list;

            for (int i = 0; i < names.Length; i++)
            {
                var n = (names[i] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(n)) continue;

                var st = (stocks != null && i < stocks.Length) ? stocks[i] : 0;
                if (st < 0) st = 0;

                list.Add(new ProductSize { Name = n, StockCount = st });
            }
            return list;
        }

        private async Task<List<ImageItem>> SaveImagesAndCreateImageItems(
    List<IFormFile> files,
    int startOrder = 1)
        {
            var result = new List<ImageItem>();
            var root = Path.Combine(_env.WebRootPath, "uploads", "products");

            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);

            int displayOrder = startOrder;

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                    continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };

                if (!allowed.Contains(ext))
                    continue;

                var safeName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(root, safeName);

                using (var stream = System.IO.File.Create(fullPath))
                {
                    await file.CopyToAsync(stream);
                }

                var img = new ImageItem
                {
                    Filename = safeName,
                    DisplayOrder = displayOrder
                };

                result.Add(img);
                displayOrder++;
            }

            return result;
        }
        private static List<IFormFile> ApplyImageOrder(List<IFormFile> files, string? newImageOrder)
        {
            if (files == null || files.Count == 0)
                return new List<IFormFile>();

            if (string.IsNullOrWhiteSpace(newImageOrder))
                return files;

            var orderedIndexes = newImageOrder
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var index) ? index : -1)
                .Where(x => x >= 0 && x < files.Count)
                .Distinct()
                .ToList();

            if (orderedIndexes.Count == 0)
                return files;

            var orderedFiles = orderedIndexes
                .Select(index => files[index])
                .ToList();

            var missingFiles = files
                .Where((file, index) => !orderedIndexes.Contains(index))
                .ToList();

            orderedFiles.AddRange(missingFiles);

            return orderedFiles;
        }
        private static void ApplyExistingImageOrder(Product product, string? existingImageOrder)
        {
            if (product.ImageItems == null || product.ImageItems.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(existingImageOrder))
                return;

            var ids = existingImageOrder
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(x => x > 0)
                .ToList();

            if (ids.Count == 0)
                return;

            int order = 1;

            foreach (var id in ids)
            {
                var img = product.ImageItems.FirstOrDefault(x => x.Id == id);

                if (img == null)
                    continue;

                img.DisplayOrder = order;
                order++;
            }
        }
        private async Task RemoveImagesFromProduct(Product product, int[] deleteImageIds)
        {
            var toRemove = product.ImageItems.Where(x => deleteImageIds.Contains(x.Id)).ToList();
            if (toRemove.Count == 0) return;

            foreach (var img in toRemove)
                product.ImageItems.Remove(img);

            await _db.SaveChangesAsync();
        }

        private static int Clamp(int value, int min, int max)
            => value < min ? min : (value > max ? max : value);
    }
}
