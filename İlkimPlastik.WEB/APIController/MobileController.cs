using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ilkimPlastik.WEB.APIController
{
    // ✅ URL: /api/mobile/...
    [Route("api/[controller]")]
    [ApiController]
    public class MobileController : ControllerBase
    {
        private readonly EfCoreContext _context;
        public MobileController(EfCoreContext context) => _context = context;

        // =====================================================
        // LOGIN (Admin paneldeki SHA256 ile UYUMLU)
        // POST: /api/mobile/login
        // body: { email, password, notificationToken? }
        // =====================================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (model == null)
                return BadRequest("Geçersiz istek.");

            var email = (model.Email ?? "").Trim().ToLowerInvariant();
            var password = (model.Password ?? "").Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest("E-posta ve şifre zorunlu.");

            // ✅ Admin paneldeki gibi: input şifreyi SHA256 hashle
            var inputHash = Sha256(password);

            // ✅ admin + email + hashed password
            var user = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.IsAdmin &&
                    x.Email.ToLower() == email &&
                    x.Password.ToLower() == inputHash);

            if (user == null)
                return BadRequest("E-posta veya şifre hatalı.");

            // ✅ notification token kaydet
            var token = (model.NotificationToken ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                user.NotificationToken = token;
                await _context.SaveChangesAsync();
            }

            // ✅ Entity dönmüyoruz (cycle riski yok)
            return Ok(new
            {
                id = user.Id,
                name = user.Name,
                surname = user.Surname,
                email = user.Email,
                phone = user.Phone,
                isAdmin = user.IsAdmin
            });
        }

        // =====================================================
        // USER INFO (opsiyonel ama mobilde çok işe yarar)
        // POST: /api/mobile/user/info?notificationToken=xxx
        // =====================================================
        [HttpPost("user/info")]
        public async Task<IActionResult> UserInfo([FromQuery] string? notificationToken)
        {
            // Mobilde auth yok; en azından token ile admin bulalım
            var token = (notificationToken ?? "").Trim();
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("notificationToken zorunlu.");

            var user = await _context.Users.FirstOrDefaultAsync(x => x.IsAdmin && x.NotificationToken == token);
            if (user == null)
                return BadRequest("Kullanıcı bulunamadı.");

            return Ok(new
            {
                id = user.Id,
                name = user.Name,
                surname = user.Surname,
                email = user.Email,
                phone = user.Phone,
                isAdmin = user.IsAdmin
            });
        }

        // =====================================================
        // ORDERS LIST (CYCLE FIXED + DTO)
        // GET: /api/mobile/orders
        // =====================================================
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders()
        {
            var orders = await _context.Orders
                .Where(x=> x.IsPay)
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => new OrderDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    Name = x.Name,
                    Surname = x.Surname,
                    Phone = x.Phone,
                    City = x.City,
                    District = x.District,
                    Details = x.Details,
                    PostCode = x.PostCode,
                    CreatedAt = x.CreatedAt,
                    IsPay = x.IsPay,
                    Status = x.Status,
                    TotalPrice = x.TotalPrice,
                    Products = x.OrderProducts.Select(p => new OrderProductDto
                    {
                        Id = p.Id,
                        ProductId = p.ProductId,
                        Title = p.Title,
                        Description = p.Description,
                        Keywords = p.Keywords,
                        Price = p.Price,
                        CategoryId = p.CategoryId,
                        CategoryName = p.CategoryName,
                        ImageName = p.ImageName,
                        Count = p.Count
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // =====================================================
        // ORDER DETAIL (CYCLE FIXED + DTO)
        // GET: /api/mobile/orders/{id}
        // =====================================================
        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            if (id <= 0)
                return BadRequest("Geçersiz sipariş id.");

            var order = await _context.Orders
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new OrderDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    Name = x.Name,
                    Surname = x.Surname,
                    Phone = x.Phone,
                    City = x.City,
                    District = x.District,
                    Details = x.Details,
                    PostCode = x.PostCode,
                    CreatedAt = x.CreatedAt,
                    IsPay = x.IsPay,
                    Status = x.Status,
                    TotalPrice = x.TotalPrice,
                    Products = x.OrderProducts.Select(p => new OrderProductDto
                    {
                        Id = p.Id,
                        ProductId = p.ProductId,
                        Title = p.Title,
                        Description = p.Description,
                        Keywords = p.Keywords,
                        Price = p.Price,
                        CategoryId = p.CategoryId,
                        CategoryName = p.CategoryName,
                        ImageName = p.ImageName,
                        Count = p.Count
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (order == null)
                return BadRequest("Sipariş bulunamadı.");

            return Ok(order);
        }

        // =====================================================
        // ORDER STATUS CHANGE
        // POST: /api/mobile/change/order/status
        // body: { adminUserId, orderId, status }
        // status: Pending/Paid/Shipped/Cancelled
        // =====================================================
        [HttpPost("change/order/status")]
        public async Task<IActionResult> ChangeOrderStatus([FromBody] ChangeStatusModel model)
        {
            if (model == null)
                return BadRequest("Geçersiz istek.");

            if (model.AdminUserId <= 0 || model.OrderId <= 0)
                return BadRequest("AdminUserId ve OrderId zorunlu.");

            var admin = await _context.Users.FirstOrDefaultAsync(x => x.Id == model.AdminUserId);
            if (admin == null || !admin.IsAdmin)
                return BadRequest("Yetkiniz yok.");

            var status = (model.Status ?? "").Trim();
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Pending", "Paid", "Shipped", "Cancelled" };

            if (string.IsNullOrWhiteSpace(status) || !allowed.Contains(status))
                return BadRequest("Geçersiz durum. (Pending/Paid/Shipped/Cancelled)");

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == model.OrderId);
            if (order == null)
                return BadRequest("Sipariş bulunamadı.");

            // ✅ isPay otomatik
            order.Status = status;
            order.IsPay = status.Equals("Paid", StringComparison.OrdinalIgnoreCase);

            await _context.SaveChangesAsync();
            return Ok("OK");
        }

        // =====================================================
        // CARGO UPDATE (SENİN ENTITY'DE ALAN YOK)
        // POST: /api/mobile/orders/cargo
        // body: { orderId, cargoCompany?, cargoCode? }
        // NOT: Order entity’de kargo alanları yoksa sadece Shipped yapıyoruz.
        // =====================================================
        [HttpPost("orders/cargo")]
        public async Task<IActionResult> UpdateCargo([FromBody] CargoUpdateModel model)
        {
            if (model == null)
                return BadRequest("Geçersiz istek.");

            if (model.OrderId <= 0)
                return BadRequest("OrderId zorunlu.");

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == model.OrderId);
            if (order == null)
                return BadRequest("Sipariş bulunamadı.");

            order.Status = "Shipped";
            await _context.SaveChangesAsync();

            return Ok("OK");
        }

        // =====================================================
        // CANCEL ORDER (DB UPDATE)
        // POST: /api/mobile/orders/cancel
        // body: { adminUserId, orderId }
        // =====================================================
        [HttpPost("orders/cancel")]
        public async Task<IActionResult> CancelPayment([FromBody] CancelModel model)
        {
            if (model == null)
                return BadRequest("Geçersiz istek.");

            if (model.AdminUserId <= 0 || model.OrderId <= 0)
                return BadRequest("AdminUserId ve OrderId zorunlu.");

            var admin = await _context.Users.FirstOrDefaultAsync(x => x.Id == model.AdminUserId);
            if (admin == null || !admin.IsAdmin)
                return BadRequest("Yetkiniz yok.");

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == model.OrderId);
            if (order == null)
                return BadRequest("Sipariş bulunamadı.");

            order.IsPay = false;
            order.Status = "Cancelled";

            await _context.SaveChangesAsync();
            return Ok("OK");
        }

        // =====================================================
        // CONTACT MESSAGES
        // GET: /api/mobile/francheses
        // DELETE: /api/mobile/francheses/{id}
        // =====================================================
        [HttpGet("francheses")]
        public async Task<IActionResult> GetMessages()
        {
            var list = await _context.ContactMessages
                .AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    email = x.Email,
                    subject = x.Subject,
                    message = x.Message,
                    isRead = x.IsRead,
                    createdAt = x.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpDelete("francheses/{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            if (id <= 0)
                return BadRequest("Geçersiz id.");

            var item = await _context.ContactMessages.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null)
                return BadRequest("Mesaj bulunamadı.");

            _context.ContactMessages.Remove(item);
            await _context.SaveChangesAsync();

            return Ok("OK");
        }

        // =====================================================
        // HELPERS
        // =====================================================
        private static string Sha256(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }

    // =====================================================
    // REQUEST MODELS
    // =====================================================
    public class LoginModel
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? NotificationToken { get; set; }
    }

    public class ChangeStatusModel
    {
        public int AdminUserId { get; set; }
        public int OrderId { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class CargoUpdateModel
    {
        public int OrderId { get; set; }
        public string? CargoCompany { get; set; }
        public string? CargoCode { get; set; }
    }

    public class CancelModel
    {
        public int AdminUserId { get; set; }
        public int OrderId { get; set; }
    }

    // =====================================================
    // RESPONSE DTOs (cycle fix)
    // =====================================================
    public class OrderDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        public string Name { get; set; } = "";
        public string Surname { get; set; } = "";
        public string Phone { get; set; } = "";
        public string City { get; set; } = "";
        public string District { get; set; } = "";
        public string? Details { get; set; }
        public string? PostCode { get; set; }

        public DateTime CreatedAt { get; set; }
        public bool IsPay { get; set; }
        public string Status { get; set; } = "Pending";

        public decimal TotalPrice { get; set; }
        public List<OrderProductDto> Products { get; set; } = new();
    }

    public class OrderProductDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }

        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? Keywords { get; set; }

        public decimal Price { get; set; }

        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string? ImageName { get; set; }

        public int Count { get; set; }
    }
}
