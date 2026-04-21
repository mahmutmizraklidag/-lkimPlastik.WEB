using ilkimPlastik.WEB.Models;
using ilkimPlastik.WEB;
using ilkimPlastik.WEB.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Xml.Linq;

namespace ilkimPlastik.WEB.Controllers
{
    public class CheckoutController : Controller
    {
        // =====================================================
        // VAKIFBANK AYARLARI
        // =====================================================
        private bool UseVakifbankTest { get; set; } = false;

        // UseVakifbankTest 'true' ise test bilgilerini, 'false' ise canlı (prod) bilgilerini döner.
        private string VakifbankMerchantId => UseVakifbankTest ? "000100000013506" : "000000053765459";
        private string VakifbankMerchantPassword => UseVakifbankTest ? "123456" : "a2MZc9w5";
        private string VakifbankTerminalNo => UseVakifbankTest ? "VP000579" : "V3500192";

        private string VakifbankEnrollmentUrl => UseVakifbankTest
            ? "https://inbound.apigatewaytest.vakifbank.com.tr:8443/threeDGateway/Enrollment"
            : "https://inbound.apigateway.vakifbank.com.tr:8443/threeDGateway/Enrollment";

        private string VakifbankVposUrl => UseVakifbankTest
            ? "https://apiportalprep.vakifbank.com.tr:8443/virtualPos/Vposreq"
            : "https://apigw.vakifbank.com.tr:8443/virtualPos/Vposreq";

        private const string VakifbankCurrencyCode = "949"; // TRY
        private const string VakifbankTransactionType = "Sale";
        private const string VakifbankTransactionDeviceSource = "0"; // E-Commerce
        private const string CART_KEY = "CART_V1";
        private const string VB3D_CACHE_PREFIX = "VB3D_";

        private readonly EfCoreContext _db;
        private readonly IDistributedCache _cache;

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public CheckoutController(EfCoreContext db, IDistributedCache cache)
        {
            _db = db;
            _cache = cache;
        }

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
        // VAKIFBANK 3D ENROLLMENT
        // =======================
        [HttpPost("/checkout/pay")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(PaymentModel paymentModel)
        {
            if (string.IsNullOrWhiteSpace(VakifbankMerchantId) ||
                string.IsNullOrWhiteSpace(VakifbankMerchantPassword) ||
                string.IsNullOrWhiteSpace(VakifbankTerminalNo))
            {
                TempData["ERR"] = "Ödeme altyapısı ayarları eksik. (MerchantId / MerchantPassword / TerminalNo)";
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
                var (oldUnit, newUnit, pct) = CalcOffer(p.Price, p.OfferRate);
                var lineTotal = R2(newUnit * qty);
                subTotal += lineTotal;

                orderProducts.Add(new OrderProduct
                {
                    ProductId = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    Keywords = p.Keywords,
                    Price = newUnit,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category?.Name ?? "",
                    ImageName = img,
                    Count = qty
                });

                TrySetProp(orderProducts.Last(), "OldPrice", oldUnit);
                TrySetProp(orderProducts.Last(), "OfferRate", pct);
            }

            if (orderProducts.Count == 0)
            {
                TempData["ERR"] = "Sipariş oluşturulamadı. Sepet içeriğini kontrol edin.";
                return Redirect("/cart");
            }

            var shippingCost = 0m;
            var paidPrice = R2(subTotal + shippingCost);

            var cleanCardNumber = OnlyDigits(paymentModel.CardNumber);
            var cleanCvv = OnlyDigits(paymentModel.CVV);
            var expDigits = OnlyDigits(paymentModel.ExpirationDate);

            if (cleanCardNumber.Length < 13 || expDigits.Length < 4 || cleanCvv.Length < 3)
            {
                TempData["ERR"] = "Kart bilgileri geçersiz görünüyor.";
                return Redirect("/checkout");
            }

            var verifyEnrollmentRequestId = Guid.NewGuid().ToString("N");
            var enrollmentExpiry = BuildEnrollmentExpiry(paymentModel.ExpirationDate); // YYAA
            var vposExpiry = BuildVposExpiry(paymentModel.ExpirationDate);             // YYYYMM
            var callbackUrl = BuildAbsoluteUrl("/checkout/callback");
            var sessionInfo = verifyEnrollmentRequestId;
            var brandName = DetectBrandName(cleanCardNumber);
            var installment = 0;

            var enrollmentForm = new Dictionary<string, string>
            {
                ["MerchantId"] = VakifbankMerchantId,
                ["MerchantPassword"] = VakifbankMerchantPassword,
                ["VerifyEnrollmentRequestId"] = verifyEnrollmentRequestId,
                ["Pan"] = cleanCardNumber,
                ["ExpiryDate"] = enrollmentExpiry,
                ["PurchaseAmount"] = ToVakifbankAmount(paidPrice),
                ["Currency"] = VakifbankCurrencyCode,
                ["BrandName"] = brandName,
                ["SessionInfo"] = sessionInfo,
                ["SuccessUrl"] = callbackUrl,
                ["FailureUrl"] = callbackUrl
            };

            if (installment > 1)
                enrollmentForm["InstallmentCount"] = installment.ToString();

            var enrollmentRespStr = await PostFormAsync(VakifbankEnrollmentUrl, enrollmentForm);
            var enrollmentResp = ParseEnrollmentResponse(enrollmentRespStr);

            if (enrollmentResp == null)
            {
                TempData["ERR"] = "VakıfBank enrollment cevabı okunamadı.";
                return Redirect("/checkout");
            }

            if (!string.Equals(enrollmentResp.Status, "Y", StringComparison.OrdinalIgnoreCase))
            {
                var failMessage = !string.IsNullOrWhiteSpace(enrollmentResp.ErrorMessage)
                    ? enrollmentResp.ErrorMessage
                    : !string.IsNullOrWhiteSpace(enrollmentResp.Status)
                        ? $"3D doğrulama başlatılamadı. Durum: {enrollmentResp.Status}"
                        : "3D doğrulama başlatılamadı.";

                TempData["ERR"] = failMessage;
                return Redirect("/checkout");
            }

            var subBefore = orderProducts.Sum(op =>
            {
                var old = GetPropDecimal(op, "OldPrice", op.Price);
                return R2(old * op.Count);
            });

            var subAfter = R2(orderProducts.Sum(op => op.Price * op.Count));
            var discTotal = R2(subBefore - subAfter);
            if (discTotal < 0m) discTotal = 0m;

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

                TotalPrice = paidPrice,
                OrderProducts = orderProducts,

                PaymentConversationId = verifyEnrollmentRequestId,
                PaymentRaw = enrollmentRespStr,
                MpiTransactionId = verifyEnrollmentRequestId,
                DiscountTotal = discTotal,
                SubTotalBeforeDiscount = subBefore
            };

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            var cardSession = new VakifbankCardSessionModel
            {
                VerifyEnrollmentRequestId = verifyEnrollmentRequestId,
                CardNumber = cleanCardNumber,
                Cvv = cleanCvv,
                EnrollmentExpiry = enrollmentExpiry,
                VposExpiry = vposExpiry,
                CardHolderName = paymentModel.CardHolderName?.Trim() ?? "",
                Amount = paidPrice,
                InstallmentCount = installment > 1 ? installment : null,
                OrderId = order.Id
            };

            await SaveVakifbank3DStateAsync(cardSession);

            var html = BuildThreeDAutoPostHtml(
                enrollmentResp.ACSUrl,
                enrollmentResp.PaReq,
                enrollmentResp.TermUrl,
                enrollmentResp.MD);

            return Content(html, "text/html; charset=utf-8");
        }


        [HttpPost("/checkout/callback")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Callback(VakifbankCallbackModel cb)
        {
            if (cb == null || string.IsNullOrWhiteSpace(cb.VerifyEnrollmentRequestId))
                return Redirect("/checkout/failed");

            var order = await FindOrderByConversationIdAsync(cb.VerifyEnrollmentRequestId);
            var sessionData = await GetVakifbank3DStateAsync(cb.VerifyEnrollmentRequestId);

            if (order == null && sessionData?.OrderId > 0)
            {
                order = await _db.Orders
                    .Include(x => x.OrderProducts)
                    .FirstOrDefaultAsync(x => x.Id == sessionData.OrderId);
            }

            if (order == null)
                return Redirect("/checkout/failed");

            if (sessionData == null)
            {
                order.IsPay = false;
                order.Status = "Failed";
                order.PaymentRaw = JsonSerializer.Serialize(cb, JsonOpts);

                _db.Update(order);
                await _db.SaveChangesAsync();
                return Redirect("/checkout/failed");
            }

            var callbackRaw = JsonSerializer.Serialize(cb, JsonOpts);

            // Merchant doğrulaması
            if (!string.IsNullOrWhiteSpace(cb.MerchantId) &&
                !string.Equals(cb.MerchantId.Trim(), VakifbankMerchantId, StringComparison.Ordinal))
            {
                order.IsPay = false;
                order.Status = "Failed";
                order.PaymentRaw = callbackRaw;

                _db.Update(order);
                await _db.SaveChangesAsync();

                await ClearVakifbank3DStateAsync(cb.VerifyEnrollmentRequestId);

                TempData["PaymentErrorMessage"] = "Callback merchant bilgisi eşleşmedi.";
                TempData["PaymentErrorCode"] = "MERCHANT_MISMATCH";
                TempData["OrderId"] = order.Id.ToString();

                return Redirect("/checkout/failed");
            }

            // Hash doğrulaması (banka hash gönderirse mutlaka kontrol edilir)
            if (!string.IsNullOrWhiteSpace(cb.Hash))
            {
                var expectedHash = ComputeCallbackHash(cb, VakifbankMerchantPassword);

                Console.WriteLine("=== CALLBACK HASH BANK ===");
                Console.WriteLine(cb.Hash);

                Console.WriteLine("=== CALLBACK HASH EXPECTED ===");
                Console.WriteLine(expectedHash);

                Console.WriteLine("=== CALLBACK HASH INPUTS ===");
                Console.WriteLine($"VerifyEnrollmentRequestId: [{cb.VerifyEnrollmentRequestId}]");
                Console.WriteLine($"MerchantId: [{cb.MerchantId}]");
                Console.WriteLine($"PurchCurrency: [{cb.PurchCurrency}]");
                Console.WriteLine($"PurchAmount: [{cb.PurchAmount}]");
                Console.WriteLine($"ECI: [{cb.ECI}]");
                Console.WriteLine($"CAVV: [{cb.CAVV}]");
                Console.WriteLine($"MdStatus: [{cb.MdStatus}]");
                Console.WriteLine($"Status: [{cb.Status}]");
            }

            var status = (cb.Status ?? "").Trim().ToUpperInvariant();

            if (status != "Y" && status != "A")
            {
                order.IsPay = false;
                order.Status = "Failed";
                order.PaymentRaw = callbackRaw;

                _db.Update(order);
                await _db.SaveChangesAsync();

                await ClearVakifbank3DStateAsync(cb.VerifyEnrollmentRequestId);

                TempData["PaymentErrorMessage"] = !string.IsNullOrWhiteSpace(cb.ErrorMessage)
                    ? cb.ErrorMessage
                    : "3D Secure işlemi banka tarafından onaylanmadı.";
                TempData["PaymentErrorCode"] = cb.ErrorCode;
                TempData["OrderId"] = order.Id.ToString();

                return Redirect("/checkout/failed");
            }

            var brandName = DetectBrandName(sessionData.CardNumber); // 100: Visa, 200: MC, 300: Troy
            var eci = NormalizeEciByBrand(cb.ECI, status, brandName);
            var transactionId = Guid.NewGuid().ToString("N");
            var amount = sessionData.Amount > 0 ? sessionData.Amount : order.TotalPrice;
            var clientIp = GetClientIp();

            string xml;

            if (status == "Y")
            {
                // Full Secure
                xml = BuildVposSaleRequestXml_FullSecure(new VakifbankSaleRequestModel
                {
                    MerchantId = VakifbankMerchantId,
                    Password = VakifbankMerchantPassword,
                    TerminalNo = VakifbankTerminalNo,
                    TransactionType = VakifbankTransactionType,
                    TransactionId = transactionId,
                    CurrencyAmount = ToVakifbankAmount(amount),
                    CurrencyCode = VakifbankCurrencyCode,
                    Pan = sessionData.CardNumber,
                    Expiry = sessionData.VposExpiry,
                    Cvv = sessionData.Cvv,
                    CardHoldersName = sessionData.CardHolderName,
                    ECI = eci,
                    CAVV = cb.CAVV,
                    MpiTransactionId = cb.VerifyEnrollmentRequestId,
                    OrderId = order.Id.ToString(),
                    OrderDescription = sessionData.OrderNote,
                    ClientIp = clientIp,
                    TransactionDeviceSource = VakifbankTransactionDeviceSource,
                    NumberOfInstallments = sessionData.InstallmentCount
                });
            }
            else
            {
                // Half Secure
                xml = BuildVposSaleRequestXml_HalfSecure(new VakifbankSaleRequestModel
                {
                    MerchantId = VakifbankMerchantId,
                    Password = VakifbankMerchantPassword,
                    TerminalNo = VakifbankTerminalNo,
                    TransactionType = VakifbankTransactionType,
                    TransactionId = transactionId,
                    CurrencyAmount = ToVakifbankAmount(amount),
                    CurrencyCode = VakifbankCurrencyCode,
                    Pan = sessionData.CardNumber,
                    Expiry = sessionData.VposExpiry,
                    Cvv = sessionData.Cvv,
                    CardHoldersName = sessionData.CardHolderName,
                    ECI = eci,
                    CAVV = cb.CAVV,
                    MpiTransactionId = cb.VerifyEnrollmentRequestId,
                    OrderId = order.Id.ToString(),
                    OrderDescription = sessionData.OrderNote,
                    ClientIp = clientIp,
                    TransactionDeviceSource = VakifbankTransactionDeviceSource,
                    NumberOfInstallments = sessionData.InstallmentCount
                });
            }

            Console.WriteLine("=== CALLBACK RAW ===");
            Console.WriteLine(callbackRaw);
            Console.WriteLine("=== VPOS XML ===");
            Console.WriteLine(xml);

            using var httpClient = new HttpClient();

            var content = new StringContent(xml, System.Text.Encoding.UTF8, "application/xml");
            var response = await httpClient.PostAsync(VakifbankVposUrl, content);
            var vposRespStr = await response.Content.ReadAsStringAsync();

            Console.WriteLine("=== VPOS RESPONSE ===");
            Console.WriteLine(vposRespStr);

            var vposResp = ParseVposResponse(vposRespStr);
            var ok = vposResp != null &&
                     string.Equals(vposResp.ResultCode, "0000", StringComparison.OrdinalIgnoreCase);

            order.IsPay = ok;
            order.Status = ok ? "Paid" : "Failed";
            order.PaymentRaw = vposRespStr;
            order.PaymentId = vposResp?.TransactionId;
            order.AuthCode = vposResp?.AuthCode;
            order.Rrn = vposResp?.Rrn;
            order.MpiTransactionId = cb.VerifyEnrollmentRequestId;

            _db.Update(order);
            await _db.SaveChangesAsync();

            await ClearVakifbank3DStateAsync(cb.VerifyEnrollmentRequestId);

            if (!ok)
            {
                TempData["PaymentErrorMessage"] = !string.IsNullOrWhiteSpace(vposResp?.ResultDetail)
                    ? vposResp.ResultDetail
                    : "İşlem banka tarafından onaylanmadı.";
                TempData["PaymentErrorCode"] = vposResp?.ResultCode;
                TempData["OrderId"] = order.Id.ToString();

                return Redirect("/checkout/failed");
            }

            ClearCart();
            return Redirect("/checkout/success");
        }


        private string NormalizeEciByBrand(string? eciFromCallback, string status, string brandName)
        {
            var callbackEci = (eciFromCallback ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(callbackEci))
            {
                if (callbackEci.Length == 1)
                    callbackEci = "0" + callbackEci;

                return callbackEci;
            }

            status = (status ?? "").Trim().ToUpperInvariant();

            // brandName: 100 Visa, 200 MasterCard, 300 Troy
            if (status == "Y")
            {
                if (brandName == "100") return "05"; // Visa
                if (brandName == "200") return "02"; // MasterCard
                if (brandName == "300") return "02"; // Troy
            }

            if (status == "A")
            {
                if (brandName == "100") return "06"; // Visa
                if (brandName == "200") return "01"; // MasterCard
                if (brandName == "300") return "01"; // Troy
            }

            return "";
        }

        private string ComputeCallbackHash(VakifbankCallbackModel cb, string merchantPassword)
        {
            var verifyEnrollmentRequestId = (cb.VerifyEnrollmentRequestId ?? "").Trim();
            var merchantId = (cb.MerchantId ?? "").Trim();
            var currencyCode = (cb.PurchCurrency ?? "").Trim();

            var amount = NormalizeAmountForHash(cb.PurchAmount);
            var eci = NormalizeEciForHash(cb.ECI);
            var cavv = (cb.CAVV ?? "").Trim();
            var mdStatus = (cb.MdStatus ?? "").Trim();
            var paresStatus = (cb.Status ?? "").Trim().ToUpperInvariant();

            var raw = verifyEnrollmentRequestId +
                      merchantId +
                      currencyCode +
                      amount +
                      eci +
                      cavv +
                      mdStatus +
                      paresStatus +
                      merchantPassword;

            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.GetEncoding("ISO-8859-9").GetBytes(raw);
            var hashBytes = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }

        private string NormalizeAmountForHash(string? amount)
        {
            var value = (amount ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "";

            // Callback zaten kuruş dahil noktasız gelmiş olabilir. Örn: 1.09 => 109
            if (value.All(char.IsDigit))
                return value;

            value = value.Replace(",", ".");

            if (decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var dec))
            {
                dec = decimal.Round(dec, 2, MidpointRounding.AwayFromZero);

                // 1.09 => "109"
                return dec
                    .ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                    .Replace(".", "");
            }

            return new string(value.Where(char.IsDigit).ToArray());
        }
        private string NormalizeEciForHash(string? eci)
        {
            var value = (eci ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "";

            if (value.Length == 1)
                value = "0" + value;

            return value;
        }

        private string BuildVposSaleRequestXml_FullSecure(VakifbankSaleRequestModel m)
        {
            var root = new XElement("VposRequest",
                new XElement("MerchantId", m.MerchantId),
                new XElement("Password", m.Password),
                new XElement("TerminalNo", m.TerminalNo),
                new XElement("Pan", m.Pan),
                new XElement("Expiry", m.Expiry),
                new XElement("CurrencyAmount", m.CurrencyAmount),
                new XElement("CurrencyCode", m.CurrencyCode),
                new XElement("TransactionType", m.TransactionType)
            );

            if (!string.IsNullOrWhiteSpace(m.TransactionId))
                root.Add(new XElement("TransactionId", m.TransactionId));

            if (m.NumberOfInstallments.HasValue && m.NumberOfInstallments.Value > 1)
                root.Add(new XElement("NumberOfInstallments", m.NumberOfInstallments.Value.ToString("00")));

            if (!string.IsNullOrWhiteSpace(m.CardHoldersName))
                root.Add(new XElement("CardHoldersName", m.CardHoldersName));

            root.Add(new XElement("Cvv", m.Cvv));

            root.Add(new XElement("ECI", m.ECI ?? ""));

            if (!string.IsNullOrWhiteSpace(m.CAVV))
                root.Add(new XElement("CAVV", m.CAVV));

            root.Add(new XElement("MpiTransactionId", m.MpiTransactionId));

            if (!string.IsNullOrWhiteSpace(m.OrderId))
                root.Add(new XElement("OrderId", m.OrderId));

            if (!string.IsNullOrWhiteSpace(m.OrderDescription))
                root.Add(new XElement("OrderDescription", m.OrderDescription));

            root.Add(new XElement("ClientIp", m.ClientIp));

            // İstersen bunu da ekleyebilirsin
            // var customItems = new XElement("CustomItems",
            //     new XElement("Item",
            //         new XAttribute("name", "Açıklama"),
            //         new XAttribute("value", "Sipariş Ödemesi")));
            // root.Add(customItems);

            root.Add(new XElement("TransactionDeviceSource", m.TransactionDeviceSource));

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            return doc.ToString(SaveOptions.DisableFormatting);
        }


        private string BuildVposSaleRequestXml_HalfSecure(VakifbankSaleRequestModel m)
        {
            var root = new XElement("VposRequest",
                new XElement("MerchantId", m.MerchantId),
                new XElement("Password", m.Password),
                new XElement("TerminalNo", m.TerminalNo),
                new XElement("Pan", m.Pan),
                new XElement("Expiry", m.Expiry),
                new XElement("CurrencyAmount", m.CurrencyAmount),
                new XElement("CurrencyCode", m.CurrencyCode),
                new XElement("TransactionType", m.TransactionType)
            );

            if (!string.IsNullOrWhiteSpace(m.TransactionId))
                root.Add(new XElement("TransactionId", m.TransactionId));

            if (m.NumberOfInstallments.HasValue && m.NumberOfInstallments.Value > 1)
                root.Add(new XElement("NumberOfInstallments", m.NumberOfInstallments.Value.ToString("00")));

            if (!string.IsNullOrWhiteSpace(m.CardHoldersName))
                root.Add(new XElement("CardHoldersName", m.CardHoldersName));

            root.Add(new XElement("Cvv", m.Cvv));
            root.Add(new XElement("ECI", m.ECI ?? ""));

            if (!string.IsNullOrWhiteSpace(m.CAVV))
                root.Add(new XElement("CAVV", m.CAVV));

            root.Add(new XElement("MpiTransactionId", m.MpiTransactionId));

            if (!string.IsNullOrWhiteSpace(m.OrderId))
                root.Add(new XElement("OrderId", m.OrderId));

            if (!string.IsNullOrWhiteSpace(m.OrderDescription))
                root.Add(new XElement("OrderDescription", m.OrderDescription));

            root.Add(new XElement("ClientIp", m.ClientIp));
            root.Add(new XElement("TransactionDeviceSource", m.TransactionDeviceSource));

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            return doc.ToString(SaveOptions.DisableFormatting);
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

            public int OfferRate { get; set; }
            public decimal OldUnitPrice { get; set; }
            public decimal UnitPrice { get; set; }

            public int Quantity { get; set; }

            public decimal OldLineTotal { get; set; }
            public decimal LineTotal { get; set; }
        }


        public class VakifbankCallbackModel
        {
            public string MerchantId { get; set; } = "";
            public string VerifyEnrollmentRequestId { get; set; } = "";
            public string ExpiryDate { get; set; } = "";
            public string PurchAmount { get; set; } = "";
            public string PurchCurrency { get; set; } = "";
            public string Xid { get; set; } = "";
            public string SessionInfo { get; set; } = "";
            public string Status { get; set; } = "";
            public string? CAVV { get; set; }
            public string? ECI { get; set; }
            public string? InstallmentCount { get; set; }
            public string? MdStatus { get; set; }
            public string? Hash { get; set; }
            public string? ErrorMessage { get; set; }
            public string? ErrorCode { get; set; }
        }

        public class VakifbankEnrollmentResponse
        {
            public string Status { get; set; } = "";
            public string PaReq { get; set; } = "";
            public string ACSUrl { get; set; } = "";
            public string TermUrl { get; set; } = "";
            public string MD { get; set; } = "";
            public string VerifyEnrollmentRequestId { get; set; } = "";
            public string MessageErrorCode { get; set; } = "";
            public string ErrorCode { get; set; } = "";
            public string ErrorMessage { get; set; } = "";
        }

        public class VakifbankVposResponse
        {
            public string ResultCode { get; set; } = "";
            public string ResultDetail { get; set; } = "";
            public string TransactionId { get; set; } = "";
            public string AuthCode { get; set; } = "";
            public string Rrn { get; set; } = "";
            public string HostDate { get; set; } = "";
            public string BatchNo { get; set; } = "";
        }

        public class VakifbankCardSessionModel
        {
            public string VerifyEnrollmentRequestId { get; set; } = "";
            public int OrderId { get; set; }
            public string CardNumber { get; set; } = "";
            public string Cvv { get; set; } = "";
            public string EnrollmentExpiry { get; set; } = "";
            public string VposExpiry { get; set; } = "";
            public string CardHolderName { get; set; } = "";
            public decimal Amount { get; set; }
            public int? InstallmentCount { get; set; }
            public string? OrderNote { get; set; }
        }

        public class VakifbankSaleRequestModel
        {
            public string MerchantId { get; set; } = "";
            public string Password { get; set; } = "";
            public string TerminalNo { get; set; } = "";
            public string TransactionType { get; set; } = "Sale";
            public string TransactionId { get; set; } = "";
            public string CurrencyAmount { get; set; } = "";
            public string CurrencyCode { get; set; } = "949";
            public string Pan { get; set; } = "";
            public string Expiry { get; set; } = "";
            public string Cvv { get; set; } = "";
            public string? CardHoldersName { get; set; }
            public string? ECI { get; set; }
            public string? CAVV { get; set; }
            public string MpiTransactionId { get; set; } = "";
            public string? OrderId { get; set; }
            public string? OrderDescription { get; set; }
            public string ClientIp { get; set; } = "127.0.0.1";
            public string TransactionDeviceSource { get; set; } = "0";
            public int? NumberOfInstallments { get; set; }
        }

        // =======================
        // Helpers
        // =======================

        private async Task<string> PostFormAsync(string url, Dictionary<string, string> form)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

            var handler = new HttpClientHandler
            {
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(90);

            using var content = new FormUrlEncodedContent(form);
            var encoded = await content.ReadAsStringAsync();

            Console.WriteLine("=== POST URL ===");
            Console.WriteLine(url);
            Console.WriteLine("=== ENCODED FORM DATA ===");
            Console.WriteLine(encoded);

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };

            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("*/*");

            using var response = await client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            Console.WriteLine("=== HTTP STATUS ===");
            Console.WriteLine((int)response.StatusCode);
            Console.WriteLine(response.StatusCode);

            return raw;
        }


        private string BuildAbsoluteUrl(string relativePath)
        {
            var req = Request;

            var forwardedProto = req?.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var scheme = !string.IsNullOrWhiteSpace(forwardedProto)
                ? forwardedProto
                : (req?.Scheme ?? "https");

            var forwardedHost = req?.Headers["X-Forwarded-Host"].FirstOrDefault();
            var host = !string.IsNullOrWhiteSpace(forwardedHost)
                ? forwardedHost
                : (req?.Host.Value ?? "localhost");

            return $"{scheme}://{host}{relativePath}";
        }

        private string BuildThreeDAutoPostHtml(string acsUrl, string paReq, string termUrl, string md)
        {
            return $@"<!doctype html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>3D Secure Yönlendiriliyor</title>
</head>
<body>
    <form id=""vakifbank3dForm"" name=""vakifbank3dForm"" action=""{WebUtility.HtmlEncode(acsUrl)}"" method=""POST"">
        <noscript>
            <div style=""max-width:560px;margin:40px auto;font-family:Arial,sans-serif;padding:20px;border:1px solid #ddd;border-radius:12px;"">
                <h2>3D Secure doğrulaması</h2>
                <p>Devam etmek için aşağıdaki butona tıklayın.</p>
                <button type=""submit"">Devam Et</button>
            </div>
        </noscript>
        <input type=""hidden"" name=""PaReq"" value=""{WebUtility.HtmlEncode(paReq)}"" />
        <input type=""hidden"" name=""TermUrl"" value=""{WebUtility.HtmlEncode(termUrl)}"" />
        <input type=""hidden"" name=""MD"" value=""{WebUtility.HtmlEncode(md)}"" />
    </form>
    <script>
        document.getElementById('vakifbank3dForm').submit();
    </script>
</body>
</html>";
        }

        private VakifbankEnrollmentResponse? ParseEnrollmentResponse(string xml)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(xml)) return null;
                var doc = XDocument.Parse(xml);
                return new VakifbankEnrollmentResponse
                {
                    Status = GetDescendantValue(doc, "Status"),
                    PaReq = GetDescendantValue(doc, "PaReq"),
                    ACSUrl = GetDescendantValue(doc, "ACSUrl"),
                    TermUrl = GetDescendantValue(doc, "TermUrl"),
                    MD = GetDescendantValue(doc, "MD"),
                    VerifyEnrollmentRequestId = GetDescendantValue(doc, "VerifyEnrollmentRequestId"),
                    MessageErrorCode = GetDescendantValue(doc, "MessageErrorCode"),
                    ErrorCode = GetDescendantValue(doc, "ErrorCode"),
                    ErrorMessage = GetDescendantValue(doc, "ErrorMessage")
                };
            }
            catch
            {
                return null;
            }
        }

        private VakifbankVposResponse? ParseVposResponse(string xml)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(xml)) return null;
                var doc = XDocument.Parse(xml);
                return new VakifbankVposResponse
                {
                    ResultCode = GetDescendantValue(doc, "ResultCode"),
                    ResultDetail = GetDescendantValue(doc, "ResultDetail"),
                    TransactionId = GetDescendantValue(doc, "TransactionId"),
                    AuthCode = GetDescendantValue(doc, "AuthCode"),
                    Rrn = GetDescendantValue(doc, "Rrn"),
                    HostDate = GetDescendantValue(doc, "HostDate"),
                    BatchNo = GetDescendantValue(doc, "BatchNo")
                };
            }
            catch
            {
                return null;
            }
        }

        private string BuildVposSaleRequestXml(VakifbankSaleRequestModel m)
        {
            var root = new XElement("VposRequest",
                new XElement("MerchantId", m.MerchantId),
                new XElement("Password", m.Password),
                new XElement("TerminalNo", m.TerminalNo),
                new XElement("Pan", m.Pan),
                new XElement("Expiry", m.Expiry),
                new XElement("CurrencyAmount", m.CurrencyAmount),
                new XElement("CurrencyCode", m.CurrencyCode),
                new XElement("TransactionType", m.TransactionType),
                new XElement("TransactionId", m.TransactionId),
                new XElement("Cvv", m.Cvv),
                new XElement("ECI", m.ECI ?? ""),
                new XElement("MpiTransactionId", m.MpiTransactionId),
                new XElement("ClientIp", m.ClientIp),
                new XElement("TransactionDeviceSource", m.TransactionDeviceSource)
            );

            if (!string.IsNullOrWhiteSpace(m.CardHoldersName))
                root.Add(new XElement("CardHoldersName", m.CardHoldersName));

            if (!string.IsNullOrWhiteSpace(m.CAVV))
                root.Add(new XElement("CAVV", m.CAVV));

            if (!string.IsNullOrWhiteSpace(m.OrderId))
                root.Add(new XElement("OrderId", m.OrderId));

            if (!string.IsNullOrWhiteSpace(m.OrderDescription))
                root.Add(new XElement("OrderDescription", m.OrderDescription));

            if (m.NumberOfInstallments.HasValue && m.NumberOfInstallments.Value > 1)
                root.Add(new XElement("NumberOfInstallments", m.NumberOfInstallments.Value.ToString("00")));

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            return doc.ToString(SaveOptions.DisableFormatting);
        }

        private string GetDescendantValue(XDocument doc, string name)
        {
            return doc.Descendants()
                .FirstOrDefault(x => string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
                ?.Value?.Trim() ?? "";
        }

        private async Task SaveVakifbank3DStateAsync(VakifbankCardSessionModel model)
        {
            var json = JsonSerializer.Serialize(model, JsonOpts);

            await _cache.SetStringAsync(
                VB3D_CACHE_PREFIX + model.VerifyEnrollmentRequestId,
                json,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(20)
                });
        }

        private async Task<VakifbankCardSessionModel?> GetVakifbank3DStateAsync(string verifyEnrollmentRequestId)
        {
            var json = await _cache.GetStringAsync(VB3D_CACHE_PREFIX + verifyEnrollmentRequestId);
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return JsonSerializer.Deserialize<VakifbankCardSessionModel>(json, JsonOpts);
            }
            catch
            {
                return null;
            }
        }

        private async Task ClearVakifbank3DStateAsync(string verifyEnrollmentRequestId)
        {
            await _cache.RemoveAsync(VB3D_CACHE_PREFIX + verifyEnrollmentRequestId);
        }

        private string DetectBrandName(string pan)
        {
            if (string.IsNullOrWhiteSpace(pan)) return "100";

            var card = OnlyDigits(pan);
            if (card.StartsWith("4")) return "100";       // VISA
            if (IsMastercard(card)) return "200";         // MASTERCARD
            if (card.StartsWith("9792")) return "300";    // TROY

            return "100";
        }

        private bool IsMastercard(string pan)
        {
            if (pan.Length < 2) return false;

            if (int.TryParse(pan.Substring(0, 2), out var first2) && first2 >= 51 && first2 <= 55)
                return true;

            if (pan.Length >= 4 && int.TryParse(pan.Substring(0, 4), out var first4) && first4 >= 2221 && first4 <= 2720)
                return true;

            return false;
        }

        private string NormalizeEci(string? eciFromCallback, string status)
        {
            if (!string.IsNullOrWhiteSpace(eciFromCallback))
                return eciFromCallback.Trim();

            return status == "A" ? "06" : "05";
        }

        private string ToVakifbankAmount(decimal amount)
        {
            return amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        private string BuildEnrollmentExpiry(string exp)
        {
            var digits = OnlyDigits(exp);
            if (digits.Length < 4) return "3001";

            var mm = digits.Substring(0, 2);
            var yy = digits.Substring(2, 2);

            if (mm == "00") mm = "01";
            return yy + mm;
        }

        private string BuildVposExpiry(string exp)
        {
            var digits = OnlyDigits(exp);
            if (digits.Length < 4) return "203001";

            var mm = digits.Substring(0, 2);
            var yy = digits.Substring(2, 2);

            if (mm == "00") mm = "01";
            return "20" + yy + mm;
        }

        private string OnlyDigits(string? value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }


        private string GetClientIp()
        {
            var candidateHeaders = new[]
            {
                "CF-Connecting-IP",
                "X-Real-IP",
                "X-Forwarded-For"
            };

            foreach (var header in candidateHeaders)
            {
                var raw = Request.Headers[header].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var first = raw.Split(',').FirstOrDefault()?.Trim();
                if (System.Net.IPAddress.TryParse(first, out var forwardedIp))
                {
                    if (forwardedIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                        forwardedIp = forwardedIp.MapToIPv4();

                    return forwardedIp.ToString();
                }
            }

            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp != null)
            {
                if (remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    remoteIp = remoteIp.MapToIPv4();

                var ip = remoteIp.ToString();

                if (ip == "0.0.0.1" || ip == "::1")
                    return "127.0.0.1";

                return ip;
            }

            return "127.0.0.1";
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

                if (decimal.TryParse(v.ToString(), out var x))
                    return x;

                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private async Task<Order?> FindOrderByConversationIdAsync(string conversationId)
        {
            return await _db.Orders
                .Include(x => x.OrderProducts)
                .FirstOrDefaultAsync(x => x.PaymentConversationId == conversationId);
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

            try
            {
                return JsonSerializer.Deserialize<CartDto>(json) ?? new CartDto();
            }
            catch
            {
                return new CartDto();
            }
        }

        private void ClearCart()
        {
            // Sadece anahtarı silmek yerine içeriği boşaltmayı deneyin
            HttpContext.Session.Remove(CART_KEY);

            // Eğer çerez (Cookie) de kullanıyorsanız onu da burada silmelisiniz
            if (Request.Cookies.ContainsKey(CART_KEY))
            {
                Response.Cookies.Delete(CART_KEY);
            }
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
}