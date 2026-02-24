using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB.Areas.Admin.Controllers
{
    public class OrderProductController : AdminBaseController
    {
        private readonly EfCoreContext _db;
        public OrderProductController(EfCoreContext db) => _db = db;

        public async Task<IActionResult> Index(int orderId)
        {
            var order = await _db.Orders.AsNoTracking()
                .Include(x => x.OrderProducts)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null) return NotFound();

            ViewBag.Products = await _db.Products.AsNoTracking().OrderBy(x => x.Title).ToListAsync();
            return View(order);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int orderId, int productId, int count)
        {
            if (count <= 0) count = 1;

            var order = await _db.Orders
                .Include(x => x.OrderProducts)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null) { TempData["ERR"] = "Sipariş bulunamadı."; return RedirectToAction(nameof(Index), new { orderId }); }

            var product = await _db.Products
                .Include(x => x.Category)
                .Include(x => x.ImageItems)
                .FirstOrDefaultAsync(x => x.Id == productId);

            if (product == null) { TempData["ERR"] = "Ürün bulunamadı."; return RedirectToAction(nameof(Index), new { orderId }); }

            var op = new OrderProduct
            {
                OrderId = orderId,
                ProductId = product.Id,
                Title = product.Title,
                Description = product.Description,
                Keywords = product.Keywords,
                Price = product.Price,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name ?? "",
                ImageName = product.ImageItems.FirstOrDefault()?.Filename,
                Count = count
            };

            _db.OrderProducts.Add(op);

            // TotalPrice güncelle (istersen daha gelişmiş hesap yap)
            order.TotalPrice += (product.Price * count);
            await _db.SaveChangesAsync();

            TempData["OK"] = "Ürün eklendi.";
            return RedirectToAction(nameof(Index), new { orderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id, int orderId)
        {
            var item = await _db.OrderProducts.FindAsync(id);
            if (item == null) { TempData["ERR"] = "Kayıt bulunamadı."; return RedirectToAction(nameof(Index), new { orderId }); }

            var order = await _db.Orders.FindAsync(orderId);
            if (order != null)
                order.TotalPrice -= (item.Price * item.Count);

            _db.OrderProducts.Remove(item);
            await _db.SaveChangesAsync();

            TempData["OK"] = "Ürün silindi.";
            return RedirectToAction(nameof(Index), new { orderId });
        }
    }
}
