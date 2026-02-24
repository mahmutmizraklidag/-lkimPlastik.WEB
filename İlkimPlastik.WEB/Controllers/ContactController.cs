using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ilkimPlastik.WEB.Controllers
{
    public class ContactController : Controller
    {
        private readonly EfCoreContext _db;
        private readonly ILogger<ContactController> _logger;

        public ContactController(EfCoreContext db, ILogger<ContactController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // Tek kayıt mantığı: SiteSettings tek satır
        private async Task<SiteSettings> GetSettingsAsync()
        {
            var s = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            return s ?? new SiteSettings();
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new ContactPageVm
            {
                Settings = await GetSettingsAsync(),
                Form = new ContactMessage()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index([Bind(Prefix = "Form")] ContactMessage model)
        {
            var settings = await GetSettingsAsync();

            // Bind edilmeyen alanlar
            ModelState.Remove(nameof(ContactMessage.Id));
            ModelState.Remove(nameof(ContactMessage.CreatedAt));

            // Trim
            model.Name = (model.Name ?? "").Trim();
            model.Email = (model.Email ?? "").Trim();
            model.Subject = (model.Subject ?? "").Trim();
            model.Message = (model.Message ?? "").Trim();

            // Server-side doğrulama
            Validate(model);

            if (!ModelState.IsValid)
            {
                ViewBag.ShowFormError = true;
                return View(new ContactPageVm { Settings = settings, Form = model });
            }

            try
            {
                model.CreatedAt = DateTime.UtcNow;
                _db.ContactMessages.Add(model);
                await _db.SaveChangesAsync();

                TempData["FormSuccess"] = "Mesajınız başarıyla iletildi. En kısa sürede sizinle iletişime geçeceğiz.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İletişim formu kayıt hatası. Name={Name}, Email={Email}, Subject={Subject}",
                    model.Name, model.Email, model.Subject);

                TempData["FormError"] = "Mesajınız gönderilirken bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
                return View(new ContactPageVm { Settings = settings, Form = model });
            }
        }

        private void Validate(ContactMessage m)
        {
            // Ad Soyad
            if (string.IsNullOrWhiteSpace(m.Name))
                ModelState.AddModelError("Form.Name", "Ad Soyad zorunludur.");
            else if (m.Name.Length < 2)
                ModelState.AddModelError("Form.Name", "Ad Soyad en az 2 karakter olmalıdır.");
            else if (m.Name.Length > 80)
                ModelState.AddModelError("Form.Name", "Ad Soyad en fazla 80 karakter olabilir.");

            // E-posta
            if (string.IsNullOrWhiteSpace(m.Email))
            {
                ModelState.AddModelError("Form.Email", "E-posta zorunludur.");
            }
            else
            {
                if (m.Email.Length > 120)
                    ModelState.AddModelError("Form.Email", "E-posta en fazla 120 karakter olabilir.");

                var emailPattern = @"^[^\s@]+@[^\s@]+\.[^\s@]+$";
                if (!Regex.IsMatch(m.Email, emailPattern))
                    ModelState.AddModelError("Form.Email", "Geçerli bir e-posta adresi giriniz.");
            }

            // Konu
            if (string.IsNullOrWhiteSpace(m.Subject))
                ModelState.AddModelError("Form.Subject", "Konu zorunludur.");
            else if (m.Subject.Length < 3)
                ModelState.AddModelError("Form.Subject", "Konu en az 3 karakter olmalıdır.");
            else if (m.Subject.Length > 120)
                ModelState.AddModelError("Form.Subject", "Konu en fazla 120 karakter olabilir.");

            // Mesaj
            if (string.IsNullOrWhiteSpace(m.Message))
                ModelState.AddModelError("Form.Message", "Mesaj zorunludur.");
            else if (m.Message.Length < 10)
                ModelState.AddModelError("Form.Message", "Mesaj en az 10 karakter olmalıdır.");
            else if (m.Message.Length > 2000)
                ModelState.AddModelError("Form.Message", "Mesaj en fazla 2000 karakter olabilir.");
        }

        public class ContactPageVm
        {
            public SiteSettings Settings { get; set; } = new SiteSettings();
            public ContactMessage Form { get; set; } = new ContactMessage();
        }
    }
}