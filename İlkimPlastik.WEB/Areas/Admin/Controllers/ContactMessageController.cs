using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    [Route("admin/contactmessage")]
    public class ContactMessageController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        public ContactMessageController(EfCoreContext db) => _db = db;

        // GET /admin/contactmessage?q=&status=all|unread|read&page=1&pageSize=20
        [HttpGet("")]
        public async Task<IActionResult> Index(string? q = null, string status = "all", int page = 1, int pageSize = 20)
        {
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            status = (status ?? "all").Trim().ToLowerInvariant();
            if (status is not ("all" or "unread" or "read")) status = "all";

            if (page < 1) page = 1;
            if (pageSize < 10) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var baseQuery = _db.ContactMessages.AsNoTracking();

            if (status == "unread") baseQuery = baseQuery.Where(x => !x.IsRead);
            else if (status == "read") baseQuery = baseQuery.Where(x => x.IsRead);

            if (q != null)
            {
                baseQuery = baseQuery.Where(x =>
                    x.Name.Contains(q) ||
                    x.Email.Contains(q) ||
                    x.Subject.Contains(q) ||
                    x.Message.Contains(q));
            }

            baseQuery = baseQuery.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);

            var total = await baseQuery.CountAsync();
            var items = await baseQuery.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var unreadCount = await _db.ContactMessages.AsNoTracking().CountAsync(x => !x.IsRead);
            var todayStart = DateTime.Today;
            var todayCount = await _db.ContactMessages.AsNoTracking().CountAsync(x => x.CreatedAt >= todayStart);

            var vm = new ContactMessagesIndexVm
            {
                Items = items,
                Q = q,
                Status = status,
                Page = page,
                PageSize = pageSize,
                Total = total,
                UnreadCount = unreadCount,
                TodayCount = todayCount
            };

            return View(vm);
        }

        // POST /admin/contactmessage/mark-read
        [HttpPost("mark-read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id, string? returnUrl = null)
        {
            var item = await _db.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                TempData["ERR"] = "Mesaj bulunamadı.";
                return RedirectSafe(returnUrl, "/admin/contactmessage");
            }

            if (!item.IsRead)
            {
                item.IsRead = true;
                await _db.SaveChangesAsync();
            }

            TempData["OK"] = "Mesaj okundu olarak işaretlendi.";
            return RedirectSafe(returnUrl, "/admin/contactmessage");
        }

        // POST /admin/contactmessage/mark-unread
        [HttpPost("mark-unread")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnread(int id, string? returnUrl = null)
        {
            var item = await _db.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                TempData["ERR"] = "Mesaj bulunamadı.";
                return RedirectSafe(returnUrl, "/admin/contactmessage");
            }

            if (item.IsRead)
            {
                item.IsRead = false;
                await _db.SaveChangesAsync();
            }

            TempData["OK"] = "Mesaj okunmadı olarak işaretlendi.";
            return RedirectSafe(returnUrl, "/admin/contactmessage");
        }

        // POST /admin/contactmessage/delete
        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? returnUrl = null)
        {
            var item = await _db.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
            {
                TempData["ERR"] = "Mesaj bulunamadı.";
                return RedirectSafe(returnUrl, "/admin/contactmessage");
            }

            _db.ContactMessages.Remove(item);
            await _db.SaveChangesAsync();

            TempData["OK"] = "Mesaj silindi.";
            return RedirectSafe(returnUrl, "/admin/contactmessage");
        }

        private IActionResult RedirectSafe(string? returnUrl, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return Redirect(fallback);
        }

        public class ContactMessagesIndexVm
        {
            public List<ContactMessage> Items { get; set; } = new();
            public string? Q { get; set; }
            public string Status { get; set; } = "all";
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 20;
            public int Total { get; set; }
            public int UnreadCount { get; set; }
            public int TodayCount { get; set; }
        }
    }
}
