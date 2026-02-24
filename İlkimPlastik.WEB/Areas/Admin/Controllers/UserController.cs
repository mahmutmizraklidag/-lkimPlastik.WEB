using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class UserController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        public UserController(EfCoreContext db) => _db = db;

        public async Task<IActionResult> Index(int? editId = null, string? q = null, string? role = null)
        {
            q = (q ?? "").Trim();
            role = (role ?? "").Trim();

            var baseQuery = _db.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(role))
            {
                if (role == "admin") baseQuery = baseQuery.Where(x => x.IsAdmin);
                if (role == "user") baseQuery = baseQuery.Where(x => !x.IsAdmin);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.ToLower();
                baseQuery = baseQuery.Where(x =>
                    (x.Name + " " + x.Surname).ToLower().Contains(qq) ||
                    (x.Email ?? "").ToLower().Contains(qq) ||
                    (x.Phone ?? "").ToLower().Contains(qq) ||
                    x.Id.ToString().Contains(qq)
                );
            }

            var list = await baseQuery
                .OrderByDescending(x => x.Id)
                .Take(600)
                .ToListAsync();

            User? edit = null;
            if (editId.HasValue)
                edit = await _db.Users.FindAsync(editId.Value);

            // Analiz metrikleri (filtrelenmemiş genel tablo)
            var totalUsers = await _db.Users.CountAsync();
            var adminCount = await _db.Users.CountAsync(x => x.IsAdmin);
            var normalCount = totalUsers - adminCount;
            var missingPhone = await _db.Users.CountAsync(x => x.Phone == null || x.Phone == "");
            var missingEmail = await _db.Users.CountAsync(x => x.Email == null || x.Email == "");

            var vm = new UserIndexVm
            {
                List = list,
                Query = q,
                Role = role,
                TotalUsers = totalUsers,
                AdminCount = adminCount,
                NormalCount = normalCount,
                MissingPhone = missingPhone,
                MissingEmail = missingEmail
            };

            ViewBag.Edit = edit;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(User model, string? newPassword)
        {
            if (string.IsNullOrWhiteSpace(model.Name)) TempData["ERR"] = "Ad zorunlu.";
            else if (string.IsNullOrWhiteSpace(model.Surname)) TempData["ERR"] = "Soyad zorunlu.";
            else if (string.IsNullOrWhiteSpace(model.Email)) TempData["ERR"] = "E-posta zorunlu.";

            if (TempData["ERR"] != null)
                return RedirectToAction(nameof(Index), new { editId = model.Id > 0 ? model.Id : (int?)null });

            var email = model.Email.Trim().ToLowerInvariant();

            if (model.Id == 0)
            {
                var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == email);
                if (exists)
                {
                    TempData["ERR"] = "Bu e-posta zaten kayıtlı.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(newPassword))
                {
                    TempData["ERR"] = "Yeni kullanıcı için şifre zorunlu.";
                    return RedirectToAction(nameof(Index));
                }

                var newUser = new User
                {
                    Name = model.Name,
                    Surname = model.Surname,
                    Email = email,
                    Phone = model.Phone,
                    IsAdmin = model.IsAdmin,
                    Password = Sha256(newPassword)
                };

                _db.Users.Add(newUser);
                TempData["OK"] = "Kullanıcı eklendi.";
            }
            else
            {
                var item = await _db.Users.FindAsync(model.Id);
                if (item == null)
                {
                    TempData["ERR"] = "Kayıt bulunamadı.";
                    return RedirectToAction(nameof(Index));
                }

                // email değişecekse benzersizlik kontrol
                if (item.Email.ToLower() != email)
                {
                    var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == email && x.Id != model.Id);
                    if (exists)
                    {
                        TempData["ERR"] = "Bu e-posta zaten kayıtlı.";
                        return RedirectToAction(nameof(Index), new { editId = model.Id });
                    }
                }

                item.Name = model.Name;
                item.Surname = model.Surname;
                item.Email = email;
                item.Phone = model.Phone;
                item.IsAdmin = model.IsAdmin;

                if (!string.IsNullOrWhiteSpace(newPassword))
                    item.Password = Sha256(newPassword);

                TempData["OK"] = "Kullanıcı güncellendi.";
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Users
                .Include(x => x.Orders)
                .Include(x => x.Addresses)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (item == null)
            {
                TempData["ERR"] = "Kayıt bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            if (item.Orders.Any())
            {
                TempData["ERR"] = "Kullanıcının siparişleri var. Silmek yerine pasifleştirme önerilir.";
                return RedirectToAction(nameof(Index));
            }

            _db.Users.Remove(item);
            await _db.SaveChangesAsync();
            TempData["OK"] = "Kullanıcı silindi.";
            return RedirectToAction(nameof(Index));
        }

        private static string Sha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public class UserIndexVm
        {
            public List<User> List { get; set; } = new();
            public string? Query { get; set; }
            public string? Role { get; set; }

            public int TotalUsers { get; set; }
            public int AdminCount { get; set; }
            public int NormalCount { get; set; }
            public int MissingPhone { get; set; }
            public int MissingEmail { get; set; }
        }
    }
}