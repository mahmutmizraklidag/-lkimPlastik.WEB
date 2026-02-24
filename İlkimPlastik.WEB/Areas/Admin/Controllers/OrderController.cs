using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenHtmlToPdf;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class OrderController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        private readonly IRazorViewToStringRenderer _razor;

        public OrderController(EfCoreContext db, IRazorViewToStringRenderer razor)
        {
            _db = db;
            _razor = razor;
        }

        // Index, UpdateStatus, Delete aynı kalabilir (daha önceki typed vm’li sürüm)

        [HttpGet]
        public async Task<IActionResult> Pdf(int id)
        {
            var order = await _db.Orders.AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.OrderProducts)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (order == null) return NotFound();

            // Razor view -> HTML
            var html = await _razor.RenderViewToStringAsync(this.ControllerContext,
                "/Areas/Admin/Views/Order/Invoice.cshtml", order);

            // HTML -> PDF
            var pdf = OpenHtmlToPdf.Pdf
                .From(html)
                .WithGlobalSetting("orientation", "Portrait")
                .WithGlobalSetting("paperSize", "A4")
                .WithGlobalSetting("margin.top", "12mm")
                .WithGlobalSetting("margin.bottom", "12mm")
                .WithGlobalSetting("margin.left", "12mm")
                .WithGlobalSetting("margin.right", "12mm")
                .Content();

            var fileName = $"siparis_{order.Id}.pdf";
            return File(pdf, "application/pdf", fileName);
        }

        public async Task<IActionResult> Index(int? viewId = null, string? q = null, string? status = null)
        {
            q = (q ?? "").Trim();
            status = (status ?? "").Trim();

            var baseQuery = _db.Orders.Where(x=>x.IsPay).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(status))
                baseQuery = baseQuery.Where(x => x.Status == status);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.ToLower();
                baseQuery = baseQuery.Where(x =>
                    (x.Name + " " + x.Surname).ToLower().Contains(qq) ||
                    (x.Phone ?? "").ToLower().Contains(qq) ||
                    (x.City ?? "").ToLower().Contains(qq) ||
                    (x.District ?? "").ToLower().Contains(qq) ||
                    x.Id.ToString().Contains(qq)
                );
            }

            var list = await baseQuery
                .OrderByDescending(x => x.Id)
                .Take(500)
                .ToListAsync();

            Order? detail = null;
            if (viewId.HasValue)
            {
                detail = await _db.Orders.AsNoTracking()
                    .Include(x => x.User)
                    .Include(x => x.OrderProducts)
                    .FirstOrDefaultAsync(x => x.Id == viewId.Value);
            }

            // Özet metrikler
            var totalOrders = await _db.Orders.CountAsync();
            var totalRevenue = await _db.Orders.SumAsync(x => (decimal?)x.TotalPrice) ?? 0;

            var since7 = DateTime.UtcNow.Date.AddDays(-6);
            var last7Count = await _db.Orders.CountAsync(x => x.CreatedAt >= since7);
            var last7Revenue = await _db.Orders.Where(x => x.CreatedAt >= since7).SumAsync(x => (decimal?)x.TotalPrice) ?? 0;

            // ✅ Typed ByStatus
            var byStatus = await _db.Orders.AsNoTracking()
                .GroupBy(x => x.Status)
                .Select(g => new OrderStatusVm
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Total = g.Sum(x => x.TotalPrice)
                })
                .ToListAsync();

            ViewBag.Detail = detail;

            var vm = new OrderIndexVm
            {
                List = list,
                Query = q,
                Status = status,

                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue,
                Last7Count = last7Count,
                Last7Revenue = last7Revenue,

                ByStatus = byStatus
            };

            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
            {
                TempData["ERR"] = "Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            var allowed = new[] { "Pending", "Paid", "Shipped", "Cancelled" };
            if (!allowed.Contains(status))
            {
                TempData["ERR"] = "Geçersiz durum.";
                return RedirectToAction(nameof(Index), new { viewId = id });
            }

            order.Status = status;
            await _db.SaveChangesAsync();
            TempData["OK"] = "Durum güncellendi.";
            return RedirectToAction(nameof(Index), new { viewId = id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.Orders
                .Include(x => x.OrderProducts)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (order == null)
            {
                TempData["ERR"] = "Sipariş bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            _db.OrderProducts.RemoveRange(order.OrderProducts);
            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();

            TempData["OK"] = "Sipariş silindi.";
            return RedirectToAction(nameof(Index));
        }

        // -------- VMs --------
        public class OrderIndexVm
        {
            public List<Order> List { get; set; } = new();
            public string? Query { get; set; }
            public string? Status { get; set; }

            public int TotalOrders { get; set; }
            public decimal TotalRevenue { get; set; }
            public int Last7Count { get; set; }
            public decimal Last7Revenue { get; set; }

            public List<OrderStatusVm> ByStatus { get; set; } = new();
        }

        public class OrderStatusVm
        {
            public string Status { get; set; } = "Pending";
            public int Count { get; set; }
            public decimal Total { get; set; }
        }
    }
}