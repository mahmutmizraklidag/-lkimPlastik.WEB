using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly EfCoreContext _db;
        public AuthController(EfCoreContext db) => _db = db;

        [HttpGet]
        public IActionResult Login()
        {
            // Zaten girişliyse direkt yönlendir
            if (HttpContext.Session.GetInt32("ADMIN_ID") != null)
                return RedirectToAction("Index", "Home", new { area = "Admin" });

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            email = (email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ERR"] = "E-posta ve şifre zorunlu.";
                return RedirectToAction(nameof(Login));
            }

            // Admin user bul
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IsAdmin && x.Email.ToLower() == email);

            if (user == null)
            {
                TempData["ERR"] = "Admin kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Login));
            }

            // SHA256 hash karşılaştırma
            var inputHash = Sha256(password);
            if (!string.Equals(user.Password, inputHash, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ERR"] = "Şifre hatalı.";
                return RedirectToAction(nameof(Login));
            }

            HttpContext.Session.SetInt32("ADMIN_ID", user.Id);
            HttpContext.Session.SetString("ADMIN_NAME", $"{user.Name} {user.Surname}");

            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        private static string Sha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}