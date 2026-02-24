using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ilkimPlastik.WEB.Controllers
{
    public class AccountController : Controller
    {
        private readonly EfCoreContext _db;
        public AccountController(EfCoreContext db) => _db = db;

        // ===================== AUTH =====================
        [HttpGet] public IActionResult Register(string? returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string name, string surname, string email, string password, string? phone,
            string addressName, string addressSurname, string addressPhone, string city, string district, string addressDetails, string? postCode,
            string? returnUrl = null)
        {
            name = (name ?? "").Trim();
            surname = (surname ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();
            phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

            addressName = (addressName ?? "").Trim();
            addressSurname = (addressSurname ?? "").Trim();
            addressPhone = (addressPhone ?? "").Trim();
            city = (city ?? "").Trim();
            district = (district ?? "").Trim();
            addressDetails = (addressDetails ?? "").Trim();
            postCode = string.IsNullOrWhiteSpace(postCode) ? null : postCode.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname) ||
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ERR"] = "Lütfen zorunlu alanları eksiksiz doldurun.";
                return RedirectToAction(nameof(Register), new { returnUrl });
            }
            if (!email.Contains("@") || email.Length < 6)
            {
                TempData["ERR"] = "E-posta formatı geçersiz.";
                return RedirectToAction(nameof(Register), new { returnUrl });
            }
            if (password.Length < 6)
            {
                TempData["ERR"] = "Şifre en az 6 karakter olmalıdır.";
                return RedirectToAction(nameof(Register), new { returnUrl });
            }
            if (string.IsNullOrWhiteSpace(addressName) || string.IsNullOrWhiteSpace(addressSurname) ||
                string.IsNullOrWhiteSpace(addressPhone) || string.IsNullOrWhiteSpace(city) ||
                string.IsNullOrWhiteSpace(district) || string.IsNullOrWhiteSpace(addressDetails))
            {
                TempData["ERR"] = "Adres alanlarını eksiksiz doldurun.";
                return RedirectToAction(nameof(Register), new { returnUrl });
            }

            var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == email);
            if (exists)
            {
                TempData["ERR"] = "Bu e-posta zaten kayıtlı.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var user = new User
                {
                    Name = name,
                    Surname = surname,
                    Email = email,
                    Phone = phone,
                    IsAdmin = false,
                    Password = Sha256(password)
                };

                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                var addr = new Address
                {
                    UserId = user.Id,
                    Name = addressName,
                    Surname = addressSurname,
                    Phone = addressPhone,
                    City = city,
                    District = district,
                    Details = addressDetails,
                    PostCode = postCode
                };

                _db.Addresses.Add(addr);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();

                await SignInUser(user);

                TempData["OK"] = "Hesabınız oluşturuldu.";
                return RedirectSafe(returnUrl, "/");
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["ERR"] = "Kayıt sırasında bir hata oluştu.";
                return RedirectToAction(nameof(Register), new { returnUrl });
            }
        }

        [HttpGet] public IActionResult Login(string? returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
        {
            email = (email ?? "").Trim().ToLowerInvariant();
            password = password ?? "";

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ERR"] = "E-posta ve şifre zorunlu.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var user = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email && !x.IsAdmin);

            if (user == null)
            {
                TempData["ERR"] = "Kullanıcı bulunamadı.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var inputHash = Sha256(password);
            if (!string.Equals(user.Password, inputHash, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ERR"] = "Şifre hatalı.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            await SignInUser(user);

            TempData["OK"] = "Giriş başarılı.";
            return RedirectSafe(returnUrl, "/");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["OK"] = "Çıkış yapıldı.";
            return RedirectToAction(nameof(Login));
        }

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, $"{user.Name} {user.Surname}"),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("IsAdmin", "false")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(365)
                });
        }

        private IActionResult RedirectSafe(string? returnUrl, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return Redirect(fallback);
        }

        private bool IsAjax()
            => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        // ===================== PROFILE =====================
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        [HttpGet("/account/profile")]
        public async Task<IActionResult> Profile()
        {
            var userId = GetUserId();
            if (userId <= 0) return RedirectToAction(nameof(Login));

            var user = await _db.Users
                .Include(u => u.Addresses)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return RedirectToAction(nameof(Login));

            var vm = new ProfileVm
            {
                UserId = user.Id,
                Name = user.Name,
                Surname = user.Surname,
                Email = user.Email,
                Phone = user.Phone,
                Addresses = user.Addresses.OrderByDescending(a => a.Id).ToList()
            };

            return View(vm);
        }

        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        [HttpPost("/account/profile/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileUpdateVm model)
        {
            var userId = GetUserId();
            if (userId <= 0) return RedirectToAction(nameof(Login));

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToAction(nameof(Login));

            model.Name = (model.Name ?? "").Trim();
            model.Surname = (model.Surname ?? "").Trim();
            model.Email = (model.Email ?? "").Trim().ToLowerInvariant();
            model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Surname) || string.IsNullOrWhiteSpace(model.Email))
                return FailOrRedirect("Ad, Soyad ve E-posta zorunludur.");

            if (!model.Email.Contains("@") || model.Email.Length < 6)
                return FailOrRedirect("E-posta formatı geçersiz.");

            var exists = await _db.Users.AnyAsync(x => x.Id != userId && x.Email.ToLower() == model.Email);
            if (exists)
                return FailOrRedirect("Bu e-posta başka bir hesapta kayıtlı.");

            user.Name = model.Name;
            user.Surname = model.Surname;
            user.Email = model.Email;
            user.Phone = model.Phone;

            await _db.SaveChangesAsync();

            // cookie claim refresh
            await SignInUser(user);

            if (IsAjax())
            {
                return Json(new
                {
                    ok = true,
                    message = "Profil güncellendi.",
                    data = new { name = user.Name, surname = user.Surname, email = user.Email, phone=user.Phone }
                });
            }

            TempData["OK"] = "Profil güncellendi.";
            return RedirectToAction(nameof(Profile));
        }

        // ===================== PASSWORD =====================
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        [HttpPost("/account/password/change")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
        {
            var userId = GetUserId();
            if (userId <= 0) return RedirectToAction(nameof(Login));

            vm.CurrentPassword = vm.CurrentPassword ?? "";
            vm.NewPassword = vm.NewPassword ?? "";
            vm.NewPassword2 = vm.NewPassword2 ?? "";

            if (string.IsNullOrWhiteSpace(vm.CurrentPassword) ||
                string.IsNullOrWhiteSpace(vm.NewPassword) ||
                string.IsNullOrWhiteSpace(vm.NewPassword2))
                return FailOrRedirect("Şifre alanları zorunludur.");

            if (vm.NewPassword.Length < 6)
                return FailOrRedirect("Yeni şifre en az 6 karakter olmalıdır.");

            if (vm.NewPassword != vm.NewPassword2)
                return FailOrRedirect("Yeni şifreler eşleşmiyor.");

            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (user == null) return RedirectToAction(nameof(Login));

            var curHash = Sha256(vm.CurrentPassword);
            if (!string.Equals(user.Password, curHash, StringComparison.OrdinalIgnoreCase))
                return FailOrRedirect("Mevcut şifre hatalı.");

            user.Password = Sha256(vm.NewPassword);
            await _db.SaveChangesAsync();

            if (IsAjax())
                return Json(new { ok = true, message = "Şifre güncellendi." });

            TempData["OK"] = "Şifre güncellendi.";
            return RedirectToAction(nameof(Profile));
        }

        // ===================== ADDRESSES =====================
        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        [HttpPost("/account/address/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAddress(AddressInputVm a)
        {
            var userId = GetUserId();
            if (userId <= 0) return RedirectToAction(nameof(Login));

            NormalizeAddress(a);

            if (!AddressValid(a, out var msg))
                return FailOrRedirect(msg);

            var addr = new Address
            {
                UserId = userId,
                Name = a.Name,
                Surname = a.Surname,
                Phone = a.Phone,
                City = a.City,
                District = a.District,
                Details = a.Details,
                PostCode = a.PostCode
            };

            _db.Addresses.Add(addr);
            await _db.SaveChangesAsync();

            if (IsAjax())
            {
                return Json(new
                {
                    ok = true,
                    message = "Adres eklendi.",
                    data = new
                    {
                        id = addr.Id,
                        name = addr.Name,
                        surname = addr.Surname,
                        phone = addr.Phone,
                        city = addr.City,
                        district = addr.District,
                        details = addr.Details ?? "",
                        postCode = addr.PostCode ?? ""
                    }
                });
            }

            TempData["OK"] = "Adres eklendi.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        [HttpPost("/account/address/update")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAddress(AddressInputVm a)
        {
            var userId = GetUserId();
            if (userId <= 0) return RedirectToAction(nameof(Login));

            var addr = await _db.Addresses.FirstOrDefaultAsync(x => x.Id == a.Id && x.UserId == userId);
            if (addr == null)
                return FailOrRedirect("Adres bulunamadı.");

            NormalizeAddress(a);

            if (!AddressValid(a, out var msg))
                return FailOrRedirect(msg);

            addr.Name = a.Name;
            addr.Surname = a.Surname;
            addr.Phone = a.Phone;
            addr.City = a.City;
            addr.District = a.District;
            addr.Details = a.Details;
            addr.PostCode = a.PostCode;

            await _db.SaveChangesAsync();

            if (IsAjax())
            {
                return Json(new
                {
                    ok = true,
                    message = "Adres güncellendi.",
                    data = new
                    {
                        id = addr.Id,
                        name = addr.Name,
                        surname = addr.Surname,
                        phone = addr.Phone,
                        city = addr.City,
                        district = addr.District,
                        details = addr.Details ?? "",
                        postCode = addr.PostCode ?? ""
                    }
                });
            }

            TempData["OK"] = "Adres güncellendi.";
            return RedirectToAction(nameof(Profile));
        }

        [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
        [HttpPost("/account/address/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var userId = GetUserId();
            if (userId <= 0) return RedirectToAction(nameof(Login));

            var addr = await _db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (addr == null)
                return FailOrRedirect("Adres bulunamadı.");

            _db.Addresses.Remove(addr);
            await _db.SaveChangesAsync();

            if (IsAjax())
                return Json(new { ok = true, message = "Adres silindi." });

            TempData["OK"] = "Adres silindi.";
            return RedirectToAction(nameof(Profile));
        }

        private IActionResult FailOrRedirect(string message)
        {
            if (IsAjax())
                return Json(new { ok = false, message });

            TempData["ERR"] = message;
            return RedirectToAction(nameof(Profile));
        }

        private void NormalizeAddress(AddressInputVm a)
        {
            a.Name = (a.Name ?? "").Trim();
            a.Surname = (a.Surname ?? "").Trim();
            a.Phone = (a.Phone ?? "").Trim();
            a.City = (a.City ?? "").Trim();
            a.District = (a.District ?? "").Trim();
            a.Details = string.IsNullOrWhiteSpace(a.Details) ? null : a.Details.Trim();
            a.PostCode = string.IsNullOrWhiteSpace(a.PostCode) ? null : a.PostCode.Trim();
        }

        private bool AddressValid(AddressInputVm a, out string msg)
        {
            if (string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.Surname) ||
                string.IsNullOrWhiteSpace(a.Phone) || string.IsNullOrWhiteSpace(a.City) ||
                string.IsNullOrWhiteSpace(a.District) || string.IsNullOrWhiteSpace(a.Details))
            {
                msg = "Adres alanlarını eksiksiz doldurun.";
                return false;
            }
            msg = "";
            return true;
        }

        private int GetUserId()
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(s, out var id) ? id : 0;
        }

        private static string Sha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // ===================== VMs =====================
        public class ProfileVm
        {
            public int UserId { get; set; }
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string Email { get; set; } = "";
            public string? Phone { get; set; }
            public List<Address> Addresses { get; set; } = new();
        }

        public class ProfileUpdateVm
        {
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string Email { get; set; } = "";
            public string Phone { get; set; } = "";
        }

        public class ChangePasswordVm
        {
            public string CurrentPassword { get; set; } = "";
            public string NewPassword { get; set; } = "";
            public string NewPassword2 { get; set; } = "";
        }

        public class AddressInputVm
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string Phone { get; set; } = "";
            public string City { get; set; } = "";
            public string District { get; set; } = "";
            public string? Details { get; set; }
            public string? PostCode { get; set; }
        }
    }
}
