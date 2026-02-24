using ilkimPlastik.WEB.Areas.Admin.Controllers;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Controllers
{
    public class SiteSettingsController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        private readonly IWebHostEnvironment _env;

        public SiteSettingsController(EfCoreContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var item = await _db.SiteSettings.FirstOrDefaultAsync();

            if (item == null)
            {
                item = new SiteSettings { Title = "İlkim Plastik" };
                _db.SiteSettings.Add(item);
                await _db.SaveChangesAsync();
            }

            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(
            SiteSettings model,
            IFormFile? LogoFile,
            IFormFile? FaviconFile,

            // YENİ PDF DOSYALARI
            IFormFile? KvkkPdfFile,
            IFormFile? PrivacyPolicyPdfFile,
            IFormFile? ConditionsPdfFile,
            IFormFile? ReturnPolicyPdfFile,
            IFormFile? DistanceSalesAgreementPdfFile,

            bool removeLogo = false,
            bool removeFavicon = false,

            // YENİ KALDIRMA CHECKBOXLARI
            bool removeKvkkPdf = false,
            bool removePrivacyPdf = false,
            bool removeConditionsPdf = false,
            bool removeReturnPdf = false,
            bool removeDistancePdf = false
        )
        {
            var item = await _db.SiteSettings.FirstOrDefaultAsync();
            if (item == null)
            {
                item = new SiteSettings();
                _db.SiteSettings.Add(item);
                await _db.SaveChangesAsync();
            }

            // Trim helper
            string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

            // Alanlar
            item.Phone = T(model.Phone);
            item.Email = T(model.Email);
            item.AddressText = T(model.AddressText);

            item.Title = T(model.Title);
            item.Description = T(model.Description);
            item.Keywords = T(model.Keywords);
            item.Author = T(model.Author);

            item.FacebookUrl = T(model.FacebookUrl);
            item.TwitterUrl = T(model.TwitterUrl);
            item.InstagramUrl = T(model.InstagramUrl);
            item.LinkedInUrl = T(model.LinkedInUrl);

            item.WorkingHours = T(model.WorkingHours);

            item.ApiUrl = T(model.ApiUrl);
            item.IyzicoApiKey = T(model.IyzicoApiKey);
            item.IyzicoSecretKey = T(model.IyzicoSecretKey);
            item.CallBackUrl = T(model.CallBackUrl);

            item.NotificationEmail = T(model.NotificationEmail);
            item.AboutText = T(model.AboutText);

            // =======================
            // LOGO kaldır
            // =======================
            if (removeLogo && !string.IsNullOrWhiteSpace(item.LogoFileName))
            {
                DeleteFileIfExists(item.LogoFileName);
                item.LogoFileName = null;
            }

            // =======================
            // LOGO upload
            // =======================
            if (LogoFile != null && LogoFile.Length > 0)
            {
                var ext = Path.GetExtension(LogoFile.FileName).ToLowerInvariant();
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
                if (!allowed.Contains(ext))
                {
                    TempData["ERR"] = "Logo dosya tipi geçersiz. (png/jpg/webp/svg)";
                    return RedirectToAction(nameof(Index));
                }

                const long maxBytes = 4 * 1024 * 1024;
                if (LogoFile.Length > maxBytes)
                {
                    TempData["ERR"] = "Logo dosyası çok büyük. (maks 4MB)";
                    return RedirectToAction(nameof(Index));
                }

                var fileName = await SaveUploadAsync(LogoFile, "site", "logo_", ext);

                if (!string.IsNullOrWhiteSpace(item.LogoFileName))
                    DeleteFileIfExists(item.LogoFileName);

                item.LogoFileName = fileName;
            }

            // =======================
            // FAVICON kaldır
            // =======================
            if (removeFavicon && !string.IsNullOrWhiteSpace(item.FaviconFileName))
            {
                DeleteFileIfExists(item.FaviconFileName);
                item.FaviconFileName = null;
            }

            // =======================
            // FAVICON upload
            // =======================
            if (FaviconFile != null && FaviconFile.Length > 0)
            {
                var ext = Path.GetExtension(FaviconFile.FileName).ToLowerInvariant();
                var allowed = new[] { ".png", ".jpg", ".jpeg", ".webp", ".ico" };
                if (!allowed.Contains(ext))
                {
                    TempData["ERR"] = "Favicon dosya tipi geçersiz. (png/jpg/webp/ico)";
                    return RedirectToAction(nameof(Index));
                }

                const long maxBytes = 2 * 1024 * 1024;
                if (FaviconFile.Length > maxBytes)
                {
                    TempData["ERR"] = "Favicon dosyası çok büyük. (maks 2MB)";
                    return RedirectToAction(nameof(Index));
                }

                var fileName = await SaveUploadAsync(FaviconFile, "site", "favicon_", ext);

                if (!string.IsNullOrWhiteSpace(item.FaviconFileName))
                    DeleteFileIfExists(item.FaviconFileName);

                item.FaviconFileName = fileName;
            }

            // =======================
            // PDF kaldırmalar
            // =======================
            if (removeKvkkPdf && !string.IsNullOrWhiteSpace(item.KvkkPdfName))
            {
                DeleteFileIfExists(item.KvkkPdfName);
                item.KvkkPdfName = null;
            }
            if (removePrivacyPdf && !string.IsNullOrWhiteSpace(item.PrivacyPolicyPdfName))
            {
                DeleteFileIfExists(item.PrivacyPolicyPdfName);
                item.PrivacyPolicyPdfName = null;
            }
            if (removeConditionsPdf && !string.IsNullOrWhiteSpace(item.ConditionsPdfName))
            {
                DeleteFileIfExists(item.ConditionsPdfName);
                item.ConditionsPdfName = null;
            }
            if (removeReturnPdf && !string.IsNullOrWhiteSpace(item.ReturnPolicyPdfName))
            {
                DeleteFileIfExists(item.ReturnPolicyPdfName);
                item.ReturnPolicyPdfName = null;
            }
            if (removeDistancePdf && !string.IsNullOrWhiteSpace(item.DistanceSalesAgreementPdfName))
            {
                DeleteFileIfExists(item.DistanceSalesAgreementPdfName);
                item.DistanceSalesAgreementPdfName = null;
            }

            // =======================
            // PDF upload helper (tek mantık)
            // =======================
            async Task<string?> UploadPdfIfAny(IFormFile? pdf, string prefix, long maxBytes)
            {
                if (pdf == null || pdf.Length <= 0) return null;

                var ext = Path.GetExtension(pdf.FileName).ToLowerInvariant();
                if (ext != ".pdf")
                {
                    TempData["ERR"] = "Sadece PDF dosyası yükleyebilirsiniz.";
                    return null;
                }

                if (pdf.Length > maxBytes)
                {
                    TempData["ERR"] = $"PDF dosyası çok büyük. (maks {maxBytes / (1024 * 1024)}MB)";
                    return null;
                }

                // Basit içerik tipi kontrolü (bazı tarayıcılar boş gönderebilir)
                if (!string.IsNullOrWhiteSpace(pdf.ContentType) &&
                    !pdf.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ERR"] = "PDF dosyası geçersiz görünüyor.";
                    return null;
                }

                return await SaveUploadAsync(pdf, "site", prefix, ext);
            }

            const long pdfMax = 10 * 1024 * 1024; // 10MB

            // =======================
            // KVKK upload
            // =======================
            if (KvkkPdfFile != null && KvkkPdfFile.Length > 0)
            {
                var newName = await UploadPdfIfAny(KvkkPdfFile, "kvkk_", pdfMax);
                if (!string.IsNullOrWhiteSpace(TempData["ERR"] as string))
                    return RedirectToAction(nameof(Index));

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    if (!string.IsNullOrWhiteSpace(item.KvkkPdfName))
                        DeleteFileIfExists(item.KvkkPdfName);

                    item.KvkkPdfName = newName;
                }
            }

            // Gizlilik upload
            if (PrivacyPolicyPdfFile != null && PrivacyPolicyPdfFile.Length > 0)
            {
                var newName = await UploadPdfIfAny(PrivacyPolicyPdfFile, "privacy_", pdfMax);
                if (!string.IsNullOrWhiteSpace(TempData["ERR"] as string))
                    return RedirectToAction(nameof(Index));

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    if (!string.IsNullOrWhiteSpace(item.PrivacyPolicyPdfName))
                        DeleteFileIfExists(item.PrivacyPolicyPdfName);

                    item.PrivacyPolicyPdfName = newName;
                }
            }

            // Şartlar upload
            if (ConditionsPdfFile != null && ConditionsPdfFile.Length > 0)
            {
                var newName = await UploadPdfIfAny(ConditionsPdfFile, "conditions_", pdfMax);
                if (!string.IsNullOrWhiteSpace(TempData["ERR"] as string))
                    return RedirectToAction(nameof(Index));

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    if (!string.IsNullOrWhiteSpace(item.ConditionsPdfName))
                        DeleteFileIfExists(item.ConditionsPdfName);

                    item.ConditionsPdfName = newName;
                }
            }

            // İade upload
            if (ReturnPolicyPdfFile != null && ReturnPolicyPdfFile.Length > 0)
            {
                var newName = await UploadPdfIfAny(ReturnPolicyPdfFile, "return_", pdfMax);
                if (!string.IsNullOrWhiteSpace(TempData["ERR"] as string))
                    return RedirectToAction(nameof(Index));

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    if (!string.IsNullOrWhiteSpace(item.ReturnPolicyPdfName))
                        DeleteFileIfExists(item.ReturnPolicyPdfName);

                    item.ReturnPolicyPdfName = newName;
                }
            }

            // Mesafeli satış upload
            if (DistanceSalesAgreementPdfFile != null && DistanceSalesAgreementPdfFile.Length > 0)
            {
                var newName = await UploadPdfIfAny(DistanceSalesAgreementPdfFile, "distance_", pdfMax);
                if (!string.IsNullOrWhiteSpace(TempData["ERR"] as string))
                    return RedirectToAction(nameof(Index));

                if (!string.IsNullOrWhiteSpace(newName))
                {
                    if (!string.IsNullOrWhiteSpace(item.DistanceSalesAgreementPdfName))
                        DeleteFileIfExists(item.DistanceSalesAgreementPdfName);

                    item.DistanceSalesAgreementPdfName = newName;
                }
            }

            await _db.SaveChangesAsync();
            TempData["OK"] = "Ayarlar kaydedildi.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveUploadAsync(IFormFile file, string folder, string prefix, string ext)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", folder);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var fileName = $"{prefix}{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(dir, fileName);

            using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            return fileName;
        }

        private void DeleteFileIfExists(string fileName)
        {
            var safe = (fileName ?? "").Trim().TrimStart('/');
            if (safe.Contains("..")) return;

            var path = Path.Combine(_env.WebRootPath, "uploads", "site", safe);
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
    }
}
