/* =========================================================================================
   TEK PARÇA
   1) Controllers/MyOrdersController.cs
   2) Views/MyOrders/Index.cshtml

   ✅ Kullanıcı kendi siparişlerini listeler
   ✅ Detay modal (popup) içinde OrderProducts gösterilir
   ✅ "Sipariş Detayı" butonu: sayfa yenilemeden detay yükler (fetch)
   ✅ Kullanıcı deneyimi: teknik/sert mesaj yok, sade bildirim
   ✅ Güvenlik: sadece oturumdaki kullanıcı kendi siparişini görür
   ========================================================================================= */

using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ilkimPlastik.WEB.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public class MyOrdersController : Controller
    {
        private readonly EfCoreContext _db;
        public MyOrdersController(EfCoreContext db) => _db = db;

        // GET: /myorders
        [HttpGet("/myorders")]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId <= 0) return Redirect("/account/login");

            var orders = await _db.Orders.AsNoTracking()
                .Where(o => o.UserId == userId && o.IsPay)
                .OrderByDescending(o => o.Id)
                .Select(o => new OrderRowVm
                {
                    Id = o.Id,
                    CreatedAt = o.CreatedAt,
                    Status = o.Status,
                    IsPay = o.IsPay,
                    TotalPrice = o.TotalPrice,
                    City = o.City,
                    District = o.District,
                    Name = o.Name,
                    Surname = o.Surname
                })
                .Take(200)
                .ToListAsync();

            var vm = new IndexVm
            {
                Orders = orders
            };

            return View(vm);
        }

        // GET: /myorders/detail/123   (fetch ile modal doldurmak için)
        [HttpGet("/myorders/detail/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var userId = GetUserId();
            if (userId <= 0) return Unauthorized();

            var order = await _db.Orders.AsNoTracking()
                .Include(o => o.OrderProducts)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();

            var items = (order.OrderProducts ?? new List<OrderProduct>())
                .OrderBy(x => x.Id)
                .Select(x => new OrderItemVm
                {
                    ProductId = x.ProductId,
                    Title = x.Title,
                    Price = x.Price,
                    Count = x.Count,
                    ImageName = x.ImageName,
                    CategoryName = x.CategoryName
                })
                .ToList();

            var dto = new OrderDetailVm
            {
                Id = order.Id,
                CreatedAt = order.CreatedAt,
                Status = order.Status,
                IsPay = order.IsPay,
                TotalPrice = order.TotalPrice,
                Name = order.Name,
                Surname = order.Surname,
                Phone = order.Phone,
                City = order.City,
                District = order.District,
                PostCode = order.PostCode,
                Details = order.Details,
                Items = items
            };

            return Json(dto);
        }

        // helpers
        private int GetUserId()
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(s, out var id) ? id : 0;
        }

        // ===== VMs =====
        public class IndexVm
        {
            public List<OrderRowVm> Orders { get; set; } = new();
        }

        public class OrderRowVm
        {
            public int Id { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Status { get; set; } = "Pending";
            public bool IsPay { get; set; }
            public decimal TotalPrice { get; set; }
            public string City { get; set; } = "";
            public string District { get; set; } = "";
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
        }

        public class OrderDetailVm
        {
            public int Id { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Status { get; set; } = "Pending";
            public bool IsPay { get; set; }
            public decimal TotalPrice { get; set; }

            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string Phone { get; set; } = "";
            public string City { get; set; } = "";
            public string District { get; set; } = "";
            public string? PostCode { get; set; }
            public string? Details { get; set; }

            public List<OrderItemVm> Items { get; set; } = new();
        }

        public class OrderItemVm
        {
            public int ProductId { get; set; }
            public string Title { get; set; } = "";
            public decimal Price { get; set; }
            public int Count { get; set; }
            public string? ImageName { get; set; }
            public string CategoryName { get; set; } = "";
        }
    }
}

/* =========================================================================================
   Views/MyOrders/Index.cshtml
   ========================================================================================= */
