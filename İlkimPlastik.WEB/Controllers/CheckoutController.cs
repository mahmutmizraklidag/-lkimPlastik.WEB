
using ilkimPlastik.WEB.Models;
using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ilkimPlastik.WEB.Controllers
{
    public class CheckoutController : Controller
    {
        private string IyziApiBaseUrl { get; set; } = "https://api.iyzipay.com";
        private string IyziApiKey { get; set; } = "75DuMmSFX3NG6z5dtBDRovSLCAN4yWs8";
        private string IyziSecretKey { get; set; } = "AhYFDh8MRSnmTPsdacJoWDLXJZ29Vkx2";
        private string IyziCallbackUrl { get; set; } = "http://localhost:5081/checkout/callback";

        private const string IyziLocale = "tr";
        private const string IyziCurrency = "TRY";
        private const int IyziInstallment = 1;
        private const string IyziPaymentChannel = "WEB";
        private const string IyziPaymentGroup = "PRODUCT";

        private readonly EfCoreContext _db;
        private const string CART_KEY = "CART_V1";

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public CheckoutController(EfCoreContext db) => _db = db;

        // ===== Offer helpers =====
        private static int ClampOffer(int pct)
        {
            if (pct < 0) pct = 0;
            if (pct > 95) pct = 95;
            return pct;
        }
        private static decimal R2(decimal v) => decimal.Round(v, 2);

        private static (decimal oldUnit, decimal newUnit, int pct) CalcOffer(decimal price, int pct)
        {
            var oldUnit = R2(price);
            pct = ClampOffer(pct);
            if (pct <= 0) return (oldUnit, oldUnit, 0);

            var newUnit = R2(price * (1m - (pct / 100m)));
            if (newUnit < 0m) newUnit = 0m;
            return (oldUnit, newUnit, pct);
        }

        // =======================
        // CHECKOUT PAGE
        // =======================
        [HttpGet("/checkout")]
        public async Task<IActionResult> Index()
        {
            var cart = GetCart();
            if (cart.Items.Count == 0)
            {
                TempData["ERR"] = "Ödeme sayfasına geçmeden önce sepetinizde ürün olmalıdır.";
                return Redirect("/cart");
            }

            var productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _db.Products.AsNoTracking()
                .Include(p => p.ImageItems)
                .Include(p => p.ProductSizes)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var lines = new List<CartLineVm>();
            foreach (var ci in cart.Items)
            {
                var p = products.FirstOrDefault(x => x.Id == ci.ProductId);
                if (p == null) continue;

                var size = p.ProductSizes.FirstOrDefault(s => s.Id == ci.SizeId);
                if (size == null) continue;

                var qty = ci.Quantity < 1 ? 1 : ci.Quantity;
                if (qty > size.StockCount) qty = size.StockCount;
                if (qty <= 0) continue;

                var (oldUnit, newUnit, pct) = CalcOffer(p.Price, p.OfferRate);
                var oldLine = R2(oldUnit * qty);
                var newLine = R2(newUnit * qty);

                lines.Add(new CartLineVm
                {
                    ProductId = p.Id,
                    SizeId = size.Id,
                    Title = p.Title,
                    SizeName = size.Name,
                    Barcode = p.Barcode,
                    ImageFile = p.ImageItems.FirstOrDefault()?.Filename,

                    OfferRate = pct,
                    OldUnitPrice = oldUnit,
                    UnitPrice = newUnit,

                    Quantity = qty,
                    OldLineTotal = oldLine,
                    LineTotal = newLine
                });
            }

            if (lines.Count == 0)
            {
                TempData["ERR"] = "Sepetinizdeki ürünler stok nedeniyle işleme alınamadı. Sepetinizi güncelleyiniz.";
                return Redirect("/cart");
            }

            var isAuth = User?.Identity?.IsAuthenticated == true;
            int? userId = isAuth ? GetUserId() : null;

            var addressBook = new List<AddressVm>();
            int? selectedAddressId = null;

            if (userId.HasValue)
            {
                var addresses = await _db.Addresses.AsNoTracking()
                    .Where(a => a.UserId == userId.Value)
                    .OrderByDescending(a => a.Id)
                    .ToListAsync();

                addressBook = addresses.Select(a => new AddressVm
                {
                    Id = a.Id,
                    Name = a.Name,
                    Surname = a.Surname,
                    Phone = a.Phone,
                    City = a.City,
                    District = a.District,
                    Details = a.Details,
                    PostCode = a.PostCode
                }).ToList();

                selectedAddressId = addressBook.FirstOrDefault()?.Id;
            }

            var subTotalBefore = R2(lines.Sum(x => x.OldLineTotal));
            var subTotal = R2(lines.Sum(x => x.LineTotal));
            var discountTotal = R2(subTotalBefore - subTotal);
            if (discountTotal < 0m) discountTotal = 0m;

            var shippingLabel = "Ücretsiz";
            var shippingCost = 0m;
            var grandTotal = R2(subTotal + shippingCost);

            var payment = new PaymentModel();
            payment.Email = isAuth ? await ResolveUserEmailAsync(userId) : "";

            if (selectedAddressId.HasValue)
            {
                var a = addressBook.First(x => x.Id == selectedAddressId.Value);
                payment.Name = a.Name;
                payment.Surname = a.Surname;
                payment.Phone = a.Phone;
                payment.City = a.City;
                payment.District = a.District;
                payment.Address = a.Details ?? "";
                payment.PostCode = a.PostCode ?? "";
            }

            var vm = new CheckoutIndexVm
            {
                IsAuth = isAuth,
                UserId = userId,
                AddressBook = addressBook,
                SelectedAddressId = selectedAddressId,

                CartItems = lines,
                SubTotalBeforeDiscount = subTotalBefore,
                DiscountTotal = discountTotal,

                SubTotal = subTotal,
                ShippingLabel = shippingLabel,
                ShippingCost = shippingCost,
                GrandTotal = grandTotal,

                Payment = payment
            };

            return View(vm);
        }

        // =======================
        // IYZICO 3DS INITIALIZE
        // =======================
        [HttpPost("/checkout/pay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(PaymentModel paymentModel)
        {
            if (string.IsNullOrWhiteSpace(IyziApiBaseUrl) ||
                string.IsNullOrWhiteSpace(IyziApiKey) ||
                string.IsNullOrWhiteSpace(IyziSecretKey) ||
                string.IsNullOrWhiteSpace(IyziCallbackUrl))
            {
                TempData["ERR"] = "Ödeme altyapısı ayarları eksik. (Iyzico ApiUrl/ApiKey/Secret/CallbackUrl)";
                return Redirect("/checkout");
            }

            var cart = GetCart();
            if (cart.Items.Count == 0)
            {
                TempData["ERR"] = "Sepetiniz boş. Ödeme işlemi başlatılamadı.";
                return Redirect("/cart");
            }

            if (string.IsNullOrWhiteSpace(paymentModel.Name) ||
                string.IsNullOrWhiteSpace(paymentModel.Surname) ||
                string.IsNullOrWhiteSpace(paymentModel.Phone) ||
                string.IsNullOrWhiteSpace(paymentModel.City) ||
                string.IsNullOrWhiteSpace(paymentModel.District) ||
                string.IsNullOrWhiteSpace(paymentModel.Address) ||
                string.IsNullOrWhiteSpace(paymentModel.CardHolderName) ||
                string.IsNullOrWhiteSpace(paymentModel.CardNumber) ||
                string.IsNullOrWhiteSpace(paymentModel.ExpirationDate) ||
                string.IsNullOrWhiteSpace(paymentModel.CVV))
            {
                TempData["ERR"] = "Lütfen zorunlu alanları eksiksiz doldurun.";
                return Redirect("/checkout");
            }

            var isAuth = User?.Identity?.IsAuthenticated == true;
            int? userId = isAuth ? GetUserId() : null;
            if (string.IsNullOrWhiteSpace(paymentModel.Email) && isAuth)
                paymentModel.Email = await ResolveUserEmailAsync(userId);

            var productIds = cart.Items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _db.Products
                .Include(p => p.ImageItems)
                .Include(p => p.Category)
                .Include(p => p.ProductSizes)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var orderProducts = new List<OrderProduct>();
            decimal subTotal = 0m;

            var basketItems = new List<BasketItemDto>();

            foreach (var ci in cart.Items)
            {
                var p = products.FirstOrDefault(x => x.Id == ci.ProductId);
                if (p == null) continue;

                var size = p.ProductSizes.FirstOrDefault(s => s.Id == ci.SizeId);
                if (size == null) continue;
                if (size.StockCount <= 0) continue;

                var qty = ci.Quantity < 1 ? 1 : ci.Quantity;
                if (qty > size.StockCount) qty = size.StockCount;
                if (qty <= 0) continue;

                var img = p.ImageItems.FirstOrDefault()?.Filename;

                // ✅ indirimli fiyat ile ödeme
                var (oldUnit, newUnit, pct) = CalcOffer(p.Price, p.OfferRate);
                var lineTotal = R2(newUnit * qty);
                subTotal += lineTotal;

                // Order snapshot: Price alanını indirimli birim fiyat olarak yazıyoruz (toplam ile tutarlı)
                orderProducts.Add(new OrderProduct
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    Keywords = p.Keywords,
                    Price = newUnit, // ✅ indirimli birim
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category?.Name ?? "",
                    ImageName = img,
                    Count = qty
                });

                // varsa indirimsiz fiyatı da logla (entity'de alan yoksa sessiz geçer)
                TrySetProp(orderProducts.Last(), "OldPrice", oldUnit);
                TrySetProp(orderProducts.Last(), "OfferRate", pct);

                basketItems.Add(new BasketItemDto
                {
                    Id = $"{p.Id}-{size.Id}",
                    Price = lineTotal, // ✅ indirimli satır tutarı
                    Name = $"{p.Title} ({size.Name}) x{qty}",
                    Category1 = p.Category?.Name ?? "Ürün",
                    ItemType = "PHYSICAL"
                });
            }

            if (orderProducts.Count == 0)
            {
                TempData["ERR"] = "Sipariş oluşturulamadı. Sepet içeriğini kontrol edin.";
                return Redirect("/cart");
            }

            var shippingCost = 0m;
            var paidPrice = R2(subTotal + shippingCost);

            var (expMonth, expYear) = ParseExpire(paymentModel.ExpirationDate);

            var conversationId = Guid.NewGuid().ToString("N");
            var rnd = Guid.NewGuid().ToString("N");
            var initPath = "/payment/3dsecure/initialize";
            var cardNumber = (paymentModel.CardNumber ?? "").Replace(" ", "");

            var initReq = new InitializeThreeDSPaymentRequestDto
            {
                Locale = IyziLocale,
                ConversationId = conversationId,

                Price = R2(subTotal),
                PaidPrice = paidPrice,
                Currency = IyziCurrency,
                Installment = IyziInstallment,
                PaymentChannel = IyziPaymentChannel,
                BasketId = conversationId,
                PaymentGroup = IyziPaymentGroup,
                CallbackUrl = IyziCallbackUrl,

                PaymentCard = new PaymentCardDto
                {
                    CardHolderName = paymentModel.CardHolderName,
                    CardNumber = cardNumber,
                    ExpireYear = expYear,
                    ExpireMonth = expMonth,
                    Cvc = (paymentModel.CVV ?? "").Trim()
                },

                Buyer = new BuyerDto
                {
                    Id = conversationId,
                    Name = paymentModel.Name.Trim(),
                    Surname = paymentModel.Surname.Trim(),
                    IdentityNumber = string.IsNullOrWhiteSpace(paymentModel.IdentificationNumber) ? "11111111111" : paymentModel.IdentificationNumber.Trim(),
                    Email = string.IsNullOrWhiteSpace(paymentModel.Email) ? "no-reply@example.com" : paymentModel.Email.Trim(),
                    GsmNumber = paymentModel.Phone.Trim(),
                    RegistrationAddress = paymentModel.Address.Trim(),
                    City = paymentModel.City.Trim(),
                    Country = "Turkey",
                    ZipCode = (paymentModel.PostCode ?? "").Trim(),
                    Ip = GetClientIp()
                },

                ShippingAddress = new AddressDto
                {
                    Address = paymentModel.Address.Trim(),
                    ZipCode = (paymentModel.PostCode ?? "").Trim(),
                    ContactName = $"{paymentModel.Name.Trim()} {paymentModel.Surname.Trim()}",
                    City = paymentModel.City.Trim(),
                    Country = "Turkey"
                },

                BillingAddress = new AddressDto
                {
                    Address = paymentModel.Address.Trim(),
                    ZipCode = (paymentModel.PostCode ?? "").Trim(),
                    ContactName = $"{paymentModel.Name.Trim()} {paymentModel.Surname.Trim()}",
                    City = paymentModel.City.Trim(),
                    Country = "Turkey"
                },

                BasketItems = basketItems
            };

            var json = JsonSerializer.Serialize(initReq, JsonOpts);

            var auth = BuildIyziAuthHeader(rnd, initPath, json);

            using var client = new HttpClient { BaseAddress = new Uri(IyziApiBaseUrl) };
            client.DefaultRequestHeaders.Add("Authorization", auth);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-iyzi-rnd", rnd);

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(initPath, content);
            var respStr = await resp.Content.ReadAsStringAsync();

            var initResp = DeserializeSafe<InitialPaymentResponseDto>(respStr);
            if (initResp == null || !string.Equals(initResp.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ERR"] = initResp?.ErrorMessage ?? "Ödeme başlatılamadı. Lütfen tekrar deneyin.";
                return Redirect("/checkout");
            }

            var order = new Order
            {
                UserId = userId ?? 0,

                Name = paymentModel.Name.Trim(),
                Surname = paymentModel.Surname.Trim(),
                Phone = paymentModel.Phone.Trim(),
                City = paymentModel.City.Trim(),
                District = paymentModel.District.Trim(),
                Details = paymentModel.Address.Trim(),
                PostCode = string.IsNullOrWhiteSpace(paymentModel.PostCode) ? null : paymentModel.PostCode.Trim(),

                CreatedAt = DateTime.UtcNow,
                IsPay = false,
                Status = "Pending",

                TotalPrice = paidPrice,       // ✅ indirimli toplam
                OrderProducts = orderProducts
            };

            TrySetProp(order, "PaymentConversationId", conversationId);
            TrySetProp(order, "PaymentId", initResp.PaymentId);
            TrySetProp(order, "PaymentRaw", respStr);

            // istersen siparişe de indirim totals set et (alan yoksa sessiz geçer)
            var subBefore = orderProducts.Sum(op => {
                var old = GetPropDecimal(op, "OldPrice", op.Price);
                return R2(old * op.Count);
            });
            var subAfter = R2(orderProducts.Sum(op => op.Price * op.Count));
            var discTotal = R2(subBefore - subAfter);
            if (discTotal < 0m) discTotal = 0m;
            TrySetProp(order, "DiscountTotal", discTotal);
            TrySetProp(order, "SubTotalBeforeDiscount", subBefore);

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            var htmlBytes = Convert.FromBase64String(initResp.ThreeDSHtmlContent ?? "");
            var html = Encoding.UTF8.GetString(htmlBytes);
            return Content(html, "text/html");
        }

        [HttpPost("/checkout/callback")]
        public async Task<IActionResult> Callback(IyzicoCallbackDto cb)
        {
            if (cb == null || string.IsNullOrWhiteSpace(cb.ConversationId))
                return Redirect("/checkout/failed");

            var order = await FindOrderByConversationIdAsync(cb.ConversationId);
            if (order == null)
                return Redirect("/checkout/failed");

            if (!string.Equals(cb.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                order.IsPay = false;
                order.Status = "Failed";
                TrySetProp(order, "PaymentRaw", JsonSerializer.Serialize(cb, JsonOpts));
                _db.Update(order);
                await _db.SaveChangesAsync();
                return Redirect("/checkout/failed");
            }

            if (string.IsNullOrWhiteSpace(IyziApiBaseUrl) ||
                string.IsNullOrWhiteSpace(IyziApiKey) ||
                string.IsNullOrWhiteSpace(IyziSecretKey))
            {
                order.IsPay = false;
                order.Status = "Failed";
                _db.Update(order);
                await _db.SaveChangesAsync();
                return Redirect("/checkout/failed");
            }

            var rnd = Guid.NewGuid().ToString("N");
            var authPath = "/payment/3dsecure/auth";

            var authReq = new AuthRequestDto
            {
                Locale = IyziLocale,
                ConversationId = cb.ConversationId,
                PaymentId = cb.PaymentId ?? "",
                ConversationData = JsonSerializer.Serialize(cb, JsonOpts)
            };

            var json = JsonSerializer.Serialize(authReq, JsonOpts);
            var authHeader = BuildIyziAuthHeader(rnd, authPath, json);

            using var client = new HttpClient { BaseAddress = new Uri(IyziApiBaseUrl) };
            client.DefaultRequestHeaders.Add("Authorization", authHeader);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-iyzi-rnd", rnd);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage? resp = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    resp = await client.PostAsync(authPath, content);
                    if (resp.IsSuccessStatusCode) break;
                    await Task.Delay(1200);
                }
                catch
                {
                    if (attempt == 3) throw;
                    await Task.Delay(1200);
                }
            }

            if (resp == null)
                return Redirect("/checkout/failed");

            var respStr = await resp.Content.ReadAsStringAsync();
            var authResp = DeserializeSafe<IyzicoAuthResponseDto>(respStr);

            var ok = authResp != null && string.Equals(authResp.Status, "success", StringComparison.OrdinalIgnoreCase);
            order.IsPay = ok;
            order.Status = ok ? "Paid" : "Failed";

            TrySetProp(order, "PaymentRaw", respStr);
            if (!string.IsNullOrWhiteSpace(cb.PaymentId))
                TrySetProp(order, "PaymentId", cb.PaymentId);

            _db.Update(order);
            await _db.SaveChangesAsync();

            if (!ok) return Redirect("/checkout/failed");

            ClearCart();
            return Redirect("/checkout/success");
        }

        [HttpGet("/checkout/success")]
        public IActionResult Success() => View();

        [HttpGet("/checkout/failed")]
        public IActionResult Failed() => View();

        // =======================
        // VM
        // =======================
        public class CheckoutIndexVm
        {
            public bool IsAuth { get; set; }
            public int? UserId { get; set; }
            public List<AddressVm> AddressBook { get; set; } = new();
            public int? SelectedAddressId { get; set; }

            public List<CartLineVm> CartItems { get; set; } = new();

            // ✅ indirim totals
            public decimal SubTotalBeforeDiscount { get; set; }
            public decimal DiscountTotal { get; set; }

            public decimal SubTotal { get; set; }
            public string ShippingLabel { get; set; } = "Ücretsiz";
            public decimal ShippingCost { get; set; }
            public decimal GrandTotal { get; set; }

            public PaymentModel Payment { get; set; } = new();
        }

        public class AddressVm
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

        public class CartLineVm
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public string Title { get; set; } = "";
            public string SizeName { get; set; } = "";
            public string? Barcode { get; set; }
            public string? ImageFile { get; set; }

            // ✅ indirim alanları
            public int OfferRate { get; set; }
            public decimal OldUnitPrice { get; set; }
            public decimal UnitPrice { get; set; }

            public int Quantity { get; set; }

            public decimal OldLineTotal { get; set; }
            public decimal LineTotal { get; set; }
        }

        // -------- IYZICO DTOs --------
        public class InitializeThreeDSPaymentRequestDto
        {
            public string Locale { get; set; } = "tr";
            public string ConversationId { get; set; } = "";
            public decimal Price { get; set; }
            public decimal PaidPrice { get; set; }
            public string Currency { get; set; } = "TRY";
            public int Installment { get; set; } = 1;
            public string PaymentChannel { get; set; } = "WEB";
            public string BasketId { get; set; } = "";
            public string PaymentGroup { get; set; } = "PRODUCT";
            public string CallbackUrl { get; set; } = "";

            public PaymentCardDto PaymentCard { get; set; } = new();
            public BuyerDto Buyer { get; set; } = new();
            public AddressDto ShippingAddress { get; set; } = new();
            public AddressDto BillingAddress { get; set; } = new();
            public List<BasketItemDto> BasketItems { get; set; } = new();
        }

        public class PaymentCardDto
        {
            public string CardHolderName { get; set; } = "";
            public string CardNumber { get; set; } = "";
            public string ExpireYear { get; set; } = "";
            public string ExpireMonth { get; set; } = "";
            public string Cvc { get; set; } = "";
        }

        public class BuyerDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string IdentityNumber { get; set; } = "11111111111";
            public string Email { get; set; } = "";
            public string GsmNumber { get; set; } = "";
            public string RegistrationAddress { get; set; } = "";
            public string City { get; set; } = "";
            public string Country { get; set; } = "Turkey";
            public string ZipCode { get; set; } = "";
            public string Ip { get; set; } = "";
        }

        public class AddressDto
        {
            public string Address { get; set; } = "";
            public string ZipCode { get; set; } = "";
            public string ContactName { get; set; } = "";
            public string City { get; set; } = "";
            public string Country { get; set; } = "Turkey";
        }

        public class BasketItemDto
        {
            public string Id { get; set; } = "";
            public decimal Price { get; set; }
            public string Name { get; set; } = "";
            public string Category1 { get; set; } = "";
            public string ItemType { get; set; } = "PHYSICAL";
        }

        public class InitialPaymentResponseDto
        {
            public string Status { get; set; } = "";
            public string? PaymentId { get; set; }
            public string? ConversationId { get; set; }
            public string? ThreeDSHtmlContent { get; set; }
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }

        public class IyzicoCallbackDto
        {
            public string Status { get; set; } = "";
            public string? Locale { get; set; }
            public long SystemTime { get; set; }
            public string ConversationId { get; set; } = "";
            public decimal Price { get; set; }
            public decimal PaidPrice { get; set; }
            public int Installment { get; set; }
            public string? PaymentId { get; set; }
            public int MdStatus { get; set; }
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }

        public class AuthRequestDto
        {
            public string Locale { get; set; } = "tr";
            public string PaymentId { get; set; } = "";
            public string ConversationId { get; set; } = "";
            public string ConversationData { get; set; } = "";
        }

        public class IyzicoAuthResponseDto
        {
            public string Status { get; set; } = "";
            public string? PaymentId { get; set; }
            public string? ConversationId { get; set; }
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }

        // =======================
        // Helpers
        // =======================
        private string BuildIyziAuthHeader(string randomKey, string path, string jsonBody)
        {
            var payload = randomKey + path + jsonBody;
            var keyBytes = Encoding.UTF8.GetBytes(IyziSecretKey);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);

            var hash = new HMACSHA256(keyBytes).ComputeHash(payloadBytes);
            var signature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

            var authString = $"apiKey:{IyziApiKey}&randomKey:{randomKey}&signature:{signature}";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));

            return "IYZWSv2 " + base64;
        }

        private (string month, string year) ParseExpire(string exp)
        {
            var digits = new string((exp ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length < 4) return ("01", "30");
            var mm = digits.Substring(0, 2);
            var yy = digits.Substring(2, 2);
            if (mm == "00") mm = "01";
            return (mm, yy);
        }

        private string GetClientIp()
        {
            try { return Request?.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1"; }
            catch { return "127.0.0.1"; }
        }

        private T? DeserializeSafe<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json, JsonOpts); }
            catch { return default; }
        }

        private void TrySetProp(object obj, string propName, object? value)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null || !p.CanWrite) return;
                p.SetValue(obj, value);
            }
            catch { }
        }

        private decimal GetPropDecimal(object obj, string propName, decimal fallback)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null) return fallback;
                var v = p.GetValue(obj);
                if (v == null) return fallback;
                if (v is decimal d) return d;
                if (decimal.TryParse(v.ToString(), out var x)) return x;
                return fallback;
            }
            catch { return fallback; }
        }

        private async Task<Order?> FindOrderByConversationIdAsync(string conversationId)
        {
            var recent = await _db.Orders
                .Include(x => x.OrderProducts)
                .OrderByDescending(x => x.Id)
                .Take(200)
                .ToListAsync();

            foreach (var o in recent)
            {
                var v = GetPropString(o, "PaymentConversationId");
                if (!string.IsNullOrWhiteSpace(v) && v == conversationId) return o;
            }
            return null;
        }

        private string? GetPropString(object obj, string propName)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null) return null;
                return p.GetValue(obj)?.ToString();
            }
            catch { return null; }
        }

        private int? GetUserId()
        {
            var s = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(s, out var id)) return id;

            var s2 = User?.FindFirstValue("sub");
            if (int.TryParse(s2, out var id2)) return id2;

            return null;
        }

        private async Task<string> ResolveUserEmailAsync(int? userId)
        {
            var email = User?.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrWhiteSpace(email)) return email.Trim();

            email = User?.FindFirstValue("email");
            if (!string.IsNullOrWhiteSpace(email)) return email.Trim();

            var name = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(name) && name.Contains("@")) return name.Trim();

            if (userId.HasValue)
            {
                var dbEmail = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId.Value)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(dbEmail)) return dbEmail.Trim();
            }

            return "";
        }

        // =======================
        // Cart session helpers
        // =======================
        private CartDto GetCart()
        {
            var json = HttpContext.Session.GetString(CART_KEY);
            if (string.IsNullOrWhiteSpace(json)) return new CartDto();
            try { return JsonSerializer.Deserialize<CartDto>(json) ?? new CartDto(); }
            catch { return new CartDto(); }
        }

        private void ClearCart()
        {
            HttpContext.Session.Remove(CART_KEY);
        }

        private class CartDto
        {
            public List<CartItemDto> Items { get; set; } = new();
        }

        private class CartItemDto
        {
            public int ProductId { get; set; }
            public int SizeId { get; set; }
            public int Quantity { get; set; }
        }
    }

    namespace ilkimPlastik.WEB.Models
    {
        public class PaymentModel
        {
            public string Name { get; set; } = "";
            public string Surname { get; set; } = "";
            public string? IdentificationNumber { get; set; } = "";
            public string Email { get; set; } = "";
            public string Phone { get; set; } = "";
            public string City { get; set; } = "";
            public string District { get; set; } = "";
            public string Address { get; set; } = "";
            public string? PostCode { get; set; } = "";
            public string? OrderNote { get; set; } = "";

            public string CardHolderName { get; set; } = "";
            public string CardNumber { get; set; } = "";
            public string ExpirationDate { get; set; } = "";
            public string CVV { get; set; } = "";
        }
    }
}