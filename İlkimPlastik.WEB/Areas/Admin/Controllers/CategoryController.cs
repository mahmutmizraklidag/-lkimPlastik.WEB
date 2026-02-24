using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class CategoryController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        public CategoryController(EfCoreContext db) => _db = db;

        public async Task<IActionResult> Index(int? editId = null, int? editSubId = null, int? subCategoryCategoryId = null)
        {
            var list = await _db.Categories.AsNoTracking()
                .Include(x => x.SubCategories)
                .OrderByDescending(x => x.Id)
                .ToListAsync();

            Category? edit = null;
            if (editId.HasValue)
                edit = await _db.Categories.FindAsync(editId.Value);

            SubCategory? editSub = null;
            if (editSubId.HasValue)
                editSub = await _db.SubCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == editSubId.Value);

            ViewBag.Edit = edit;
            ViewBag.EditSub = editSub;

            // Alt kategori panelinde “hangi kategoriye alt kategori ekleyeceğiz” seçimi için
            // Öncelik: query param -> editSub.CategoryId -> ilk kategori
            int selectedCategoryId =
                subCategoryCategoryId
                ?? editSub?.CategoryId
                ?? list.FirstOrDefault()?.Id
                ?? 0;

            ViewBag.SubCategoryCategoryId = selectedCategoryId;

            // Seçilen kategoriye ait alt kategoriler (liste)
            var subs = new List<SubCategory>();
            if (selectedCategoryId > 0)
            {
                subs = await _db.SubCategories.AsNoTracking()
                    .Where(x => x.CategoryId == selectedCategoryId)
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();
            }

            ViewBag.SubList = subs;
            return View(list);
        }

        // ------------- CATEGORY CRUD -------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Category model)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ERR"] = "Kategori adı zorunlu.";
                return RedirectToAction(nameof(Index), new { editId = model.Id > 0 ? model.Id : (int?)null });
            }

            if (model.Id == 0)
            {
                if (await _db.Categories.AnyAsync(p => p.Slug == model.Slug))
                {
                    TempData["ERR"] = "Bu slug zaten kullanılıyor..";
                    return RedirectToAction(nameof(Index));
                }
                _db.Categories.Add(model);
                TempData["OK"] = "Kategori eklendi.";
            }
            else
            {
                if (await _db.Categories
   .AnyAsync(p => p.Slug == model.Slug && p.Id != model.Id))
                {
                    TempData["ERR"] = "Bu slug zaten kullanılıyor..";
                    return RedirectToAction(nameof(Index));
                }
                var item = await _db.Categories.FindAsync(model.Id);
                if (item == null)
                {
                    TempData["ERR"] = "Kategori bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                item.Name = model.Name;
                item.Description = model.Description;
                item.Slug = model.Slug;
                TempData["OK"] = "Kategori güncellendi.";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Categories
                .Include(x => x.Products)
                .Include(x => x.SubCategories)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
            {
                TempData["ERR"] = "Kategori bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (item.Products.Any())
            {
                TempData["ERR"] = "Bu kategoriye bağlı ürünler var. Önce ürünleri taşı/sil.";
                return RedirectToAction(nameof(Index));
            }

            if (item.SubCategories.Any())
            {
                TempData["ERR"] = "Bu kategoriye bağlı alt kategoriler var. Önce alt kategorileri sil.";
                return RedirectToAction(nameof(Index));
            }

            _db.Categories.Remove(item);
            await _db.SaveChangesAsync();
            TempData["OK"] = "Kategori silindi.";
            return RedirectToAction(nameof(Index));
        }

        // ------------- SUBCATEGORY CRUD -------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpsertSub(SubCategory model)
        {
            if (model.CategoryId <= 0)
            {
                TempData["ERR"] = "Alt kategori için kategori seçmelisin.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["ERR"] = "Alt kategori adı zorunlu.";
                return RedirectToAction(nameof(Index), new { editSubId = model.Id > 0 ? model.Id : (int?)null, subCategoryCategoryId = model.CategoryId });
            }

            if (model.Id == 0)
            {
                if (await _db.SubCategories.AnyAsync(p => p.Slug == model.Slug))
                {
                    TempData["ERR"] = "Bu slug zaten kullanılıyor..";
                    return RedirectToAction(nameof(Index));
                }
                // Aynı kategoride aynı isim var mı?
                var exists = await _db.SubCategories.AnyAsync(x =>
                    x.CategoryId == model.CategoryId && x.Name.ToLower() == model.Name.ToLower());

                if (exists)
                {
                    TempData["ERR"] = "Bu kategoride aynı isimde alt kategori zaten var.";
                    return RedirectToAction(nameof(Index), new { subCategoryCategoryId = model.CategoryId });
                }

                _db.SubCategories.Add(model);
                await _db.SaveChangesAsync();
                TempData["OK"] = "Alt kategori eklendi.";
                return RedirectToAction(nameof(Index), new { subCategoryCategoryId = model.CategoryId });
            }
            else
            {
                if (await _db.SubCategories
  .AnyAsync(p => p.Slug == model.Slug && p.Id != model.Id))
                {
                    TempData["ERR"] = "Bu slug zaten kullanılıyor..";
                    return RedirectToAction(nameof(Index));
                }
                var item = await _db.SubCategories.FindAsync(model.Id);
                if (item == null)
                {
                    TempData["ERR"] = "Alt kategori bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // Kategori + isim çakışması
                var exists = await _db.SubCategories.AnyAsync(x =>
                    x.Id != model.Id &&
                    x.CategoryId == model.CategoryId &&
                    x.Name.ToLower() == model.Name.ToLower());

                if (exists)
                {
                    TempData["ERR"] = "Bu kategoride aynı isimde alt kategori zaten var.";
                    return RedirectToAction(nameof(Index), new { editSubId = model.Id, subCategoryCategoryId = model.CategoryId });
                }

                item.CategoryId = model.CategoryId;
                item.Name = model.Name;

                await _db.SaveChangesAsync();
                TempData["OK"] = "Alt kategori güncellendi.";
                return RedirectToAction(nameof(Index), new { subCategoryCategoryId = model.CategoryId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSub(int id, int categoryId)
        {
            var item = await _db.SubCategories
                .Include(x => x.Products)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
            {
                TempData["ERR"] = "Alt kategori bulunamadı.";
                return RedirectToAction(nameof(Index), new { subCategoryCategoryId = categoryId });
            }

            if (item.Products.Any())
            {
                TempData["ERR"] = "Bu alt kategoriye bağlı ürünler var. Önce ürünleri taşı/sil.";
                return RedirectToAction(nameof(Index), new { subCategoryCategoryId = categoryId });
            }

            _db.SubCategories.Remove(item);
            await _db.SaveChangesAsync();
            TempData["OK"] = "Alt kategori silindi.";
            return RedirectToAction(nameof(Index), new { subCategoryCategoryId = categoryId });
        }
    }
}
