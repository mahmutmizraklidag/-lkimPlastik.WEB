using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Controllers
{
    public class AboutController : Controller
    {
        private readonly EfCoreContext _db;

        public AboutController(EfCoreContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // ✅ Tek kayıt: ilk ayarı al, yoksa oluştur (identity hatası yok)
            var settings = await _db.SiteSettings.AsNoTracking().FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SiteSettings
                {
                    Title = "İlkim Plastik",
                    AboutText = null
                };
                _db.SiteSettings.Add(settings);
                await _db.SaveChangesAsync();
            }

            // View'e sadece metni gönderelim
            var aboutText = (settings.AboutText ?? "").Trim();
            return View(model: aboutText);
        }
    }
}