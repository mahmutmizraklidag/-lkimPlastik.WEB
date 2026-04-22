using ilkimPlastik.WEB.Areas.Admin.Models;
using ilkimPlastik.WEB.Entities;
using ilkimPlastik.WEB.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class SliderController : Controller
    {
        private readonly EfCoreContext _db;

        public SliderController(EfCoreContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? id)
        {
            var vm = new SliderPageViewModel
            {
                Sliders = await _db.Sliders
                    .OrderByDescending(x => x.Id)
                    .ToListAsync()
            };

            if (id.HasValue)
            {
                var entity = await _db.Sliders.FindAsync(id.Value);
                if (entity == null)
                {
                    TempData["ErrorMessage"] = "Kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                vm.Id = entity.Id;
                vm.Title = entity.Title;
                vm.Description = entity.Description;
                vm.SefUrl = entity.sefUrl;
                vm.ExistingImage = entity.Image;
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(SliderPageViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Sliders = await _db.Sliders
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

                return View("Index", model);
            }

            if (model.Id.HasValue)
            {
                var entity = await _db.Sliders.FindAsync(model.Id.Value);
                if (entity == null)
                {
                    TempData["ErrorMessage"] = "Güncellenecek kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                entity.Title = model.Title?.Trim();
                entity.Description = model.Description?.Trim();
                entity.sefUrl = string.IsNullOrWhiteSpace(model.SefUrl) ? null : model.SefUrl.Trim();

                if (model.ImageFile != null && model.ImageFile.Length > 0)
                {
                    if (!string.IsNullOrWhiteSpace(entity.Image))
                    {
                        FileHelper.DeleteFile(entity.Image);
                    }

                    entity.Image = await FileHelper.FileLoaderAsync(model.ImageFile);
                }

                _db.Sliders.Update(entity);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Slider başarıyla güncellendi.";
            }
            else
            {
                var entity = new Slider
                {
                    Title = model.Title?.Trim(),
                    Description = model.Description?.Trim(),
                    sefUrl = string.IsNullOrWhiteSpace(model.SefUrl) ? null : model.SefUrl.Trim(),
                    Image = model.ImageFile != null && model.ImageFile.Length > 0
                        ? await FileHelper.FileLoaderAsync(model.ImageFile)
                        : null
                };

                await _db.Sliders.AddAsync(entity);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Slider başarıyla eklendi.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.Sliders.FindAsync(id);
            if (entity == null)
            {
                TempData["ErrorMessage"] = "Silinecek kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.IsNullOrWhiteSpace(entity.Image))
            {
                FileHelper.DeleteFile(entity.Image);
            }

            _db.Sliders.Remove(entity);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Slider başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }
    }
}