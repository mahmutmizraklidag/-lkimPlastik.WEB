using ilkimPlastik.WEB.Entities;
using System.Globalization;
using System.Net;
using System.Text;

namespace ilkimPlastik.WEB.Utils
{
    public static class MailTemplates
    {
        private const string PrimaryColor = "#f0c400";
        private const string DarkColor = "#222222";
        private const string TextColor = "#555555";
        private const string LightBg = "#f7f7f7";
        private const string WhiteColor = "#ffffff";
        private const string BorderColor = "#e9e9e9";

        private static string Encode(string? text)
        {
            return WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(text) ? "-" : text);
        }

        private static string Money(decimal value)
        {
            return value.ToString("C", new CultureInfo("tr-TR"));
        }

        private static string NormalizeBaseUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "";

            return baseUrl.Trim().TrimEnd('/');
        }

        private static string BuildLogoUrl(SiteSettings? siteSettings, string? baseUrl)
        {
            if (siteSettings == null || string.IsNullOrWhiteSpace(siteSettings.LogoFileName))
                return "";

            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
                return "";

            return $"{normalizedBaseUrl}/uploads/site/{WebUtility.UrlEncode(siteSettings.LogoFileName)}";
        }

        private static string BuildProductImageUrl(OrderProduct product, string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(product.ImageName))
                return "";

            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            if (string.IsNullOrWhiteSpace(normalizedBaseUrl))
                return "";

            return $"{normalizedBaseUrl}/uploads/products/{WebUtility.UrlEncode(product.ImageName)}";
        }

        private static string BuildHeaderLogoHtml(SiteSettings? siteSettings, string? baseUrl)
        {
            var logoUrl = BuildLogoUrl(siteSettings, baseUrl);
            if (string.IsNullOrWhiteSpace(logoUrl))
            {
                return $@"
<div style='font-size:28px; font-weight:700; color:{PrimaryColor}; letter-spacing:0.5px;'>
    Tornado Toptan
</div>";
            }

            return $@"
<img src='{logoUrl}'
     alt='Tornado Toptan'
     style='max-height:56px; width:auto; display:block; margin:0 auto;' />";
        }

        private static string BuildContactInfoHtml(SiteSettings? siteSettings)
        {
            if (siteSettings == null)
                return "";

            var phone = Encode(siteSettings.Phone);
            var email = Encode(siteSettings.Email);
            var address = Encode(siteSettings.AddressText);

            var hasPhone = !string.IsNullOrWhiteSpace(siteSettings.Phone);
            var hasEmail = !string.IsNullOrWhiteSpace(siteSettings.Email);
            var hasAddress = !string.IsNullOrWhiteSpace(siteSettings.AddressText);

            if (!hasPhone && !hasEmail && !hasAddress)
                return "";

            var sb = new StringBuilder();

            sb.Append($@"
<table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
       style='border-collapse:collapse; margin-top:16px;'>");

            if (hasPhone)
            {
                sb.Append($@"
<tr>
    <td style='padding:4px 0; font-size:13px; color:#dddddd;'>
        <strong style='color:{WhiteColor};'>Telefon:</strong> {phone}
    </td>
</tr>");
            }

            if (hasEmail)
            {
                sb.Append($@"
<tr>
    <td style='padding:4px 0; font-size:13px; color:#dddddd;'>
        <strong style='color:{WhiteColor};'>E-Posta:</strong> {email}
    </td>
</tr>");
            }

            if (hasAddress)
            {
                sb.Append($@"
<tr>
    <td style='padding:4px 0; font-size:13px; color:#dddddd; line-height:1.6;'>
        <strong style='color:{WhiteColor};'>Adres:</strong> {address}
    </td>
</tr>");
            }

            sb.Append("</table>");
            return sb.ToString();
        }

        private static string BuildProductCardHtml(OrderProduct product, string? baseUrl)
        {
            var title = Encode(product.Title);
            var imageUrl = BuildProductImageUrl(product, baseUrl);
            var quantity = product.Count;
            var unitPrice = Money(product.Price);
            var totalPrice = Money(product.Price * product.Count);

            var imageHtml = string.IsNullOrWhiteSpace(imageUrl)
                ? $@"
<div style='width:96px; height:96px; border-radius:12px; background:{LightBg}; border:1px solid {BorderColor}; text-align:center; line-height:96px; color:#999999; font-size:12px;'>
    Görsel Yok
</div>"
                : $@"
<img src='{imageUrl}'
     alt='{title}'
     width='96'
     height='96'
     style='display:block; width:96px; height:96px; object-fit:cover; border-radius:12px; border:1px solid {BorderColor};' />";

            return $@"
<tr>
    <td style='padding:0 0 14px 0;'>
        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
               style='border-collapse:collapse; background:{WhiteColor}; border:1px solid {BorderColor}; border-radius:14px;'>
            <tr>
                <td style='width:120px; padding:18px; vertical-align:top;'>
                    {imageHtml}
                </td>
                <td style='padding:18px 18px 18px 0; vertical-align:top;'>
                    <div style='font-size:17px; line-height:1.5; font-weight:700; color:{DarkColor}; margin-bottom:10px;'>
                        {title}
                    </div>

                    <div style='font-size:14px; color:{TextColor}; line-height:1.9;'>
                        <div><strong>Adet:</strong> {quantity}</div>
                        <div><strong>Birim Fiyat:</strong> {unitPrice}</div>
                        <div><strong>Toplam:</strong> {totalPrice}</div>
                    </div>
                </td>
            </tr>
        </table>
    </td>
</tr>";
        }

        private static string BuildProductsHtml(IEnumerable<OrderProduct>? products, string? baseUrl)
        {
            if (products == null || !products.Any())
            {
                return $@"
<tr>
    <td style='padding:18px; background:{WhiteColor}; border:1px solid {BorderColor}; border-radius:12px; font-size:14px; color:{TextColor};'>
        Siparişe ait ürün bilgisi bulunamadı.
    </td>
</tr>";
            }

            var sb = new StringBuilder();

            foreach (var product in products)
            {
                sb.Append(BuildProductCardHtml(product, baseUrl));
            }

            return sb.ToString();
        }

        private static string BuildCustomerInfoCard(Order order)
        {
            var fullName = Encode($"{order.Name} {order.Surname}".Trim());
            var phone = Encode(order.Phone);
            var city = Encode(order.City);
            var district = Encode(order.District);
            var details = Encode(order.Details);
            var postCode = Encode(order.PostCode);
            var createdAt = order.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            var totalPrice = Money(order.TotalPrice);

            return $@"
<table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
       style='border-collapse:collapse; background:#fffdf1; border:1px solid #f2e5a2; border-radius:14px;'>
    <tr>
        <td style='padding:20px 22px;'>
            <div style='font-size:15px; color:{DarkColor}; line-height:1.9;'>
                <div><strong>Müşteri:</strong> {fullName}</div>
                <div><strong>Telefon:</strong> {phone}</div>
                <div><strong>Sipariş Tarihi:</strong> {createdAt}</div>
                <div><strong>Toplam Tutar:</strong> {totalPrice}</div>
                <div><strong>Teslimat Adresi:</strong> {details}, {district} / {city} {postCode}</div>
            </div>
        </td>
    </tr>
</table>";
        }

        private static string BuildCompanyInfoCard(Order order)
        {
            var fullName = Encode($"{order.Name} {order.Surname}".Trim());
            var phone = Encode(order.Phone);
            var city = Encode(order.City);
            var district = Encode(order.District);
            var details = Encode(order.Details);
            var postCode = Encode(order.PostCode);
            var createdAt = order.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            var totalPrice = Money(order.TotalPrice);
            var status = Encode(order.Status);
            var isPay = order.IsPay ? "Ödendi" : "Ödeme Bekliyor";

            return $@"
<table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
       style='border-collapse:collapse; background:{WhiteColor}; border:1px solid {BorderColor}; border-radius:14px;'>
    <tr>
        <td style='padding:20px 22px;'>
            <div style='font-size:15px; color:{DarkColor}; line-height:1.95;'>
                <div><strong>Sipariş No:</strong> #{order.Id}</div>
                <div><strong>Müşteri:</strong> {fullName}</div>
                <div><strong>Telefon:</strong> {phone}</div>
                <div><strong>Sipariş Tarihi:</strong> {createdAt}</div>
                <div><strong>Toplam Tutar:</strong> {totalPrice}</div>
                <div><strong>Ödeme Durumu:</strong> {isPay}</div>
                <div><strong>Sipariş Durumu:</strong> {status}</div>
                <div><strong>Adres:</strong> {details}, {district} / {city} {postCode}</div>
            </div>
        </td>
    </tr>
</table>";
        }

        private static string BuildCustomerTemplate(Order order, string? baseUrl, SiteSettings? siteSettings)
        {
            var customerName = Encode($"{order.Name} {order.Surname}".Trim());
            var logoHtml = BuildHeaderLogoHtml(siteSettings, baseUrl);
            var contactHtml = BuildContactInfoHtml(siteSettings);
            var productsHtml = BuildProductsHtml(order.OrderProducts, baseUrl);
            var orderInfoHtml = BuildCustomerInfoCard(order);

            return $@"
<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Siparişiniz Alındı</title>
</head>
<body style='margin:0; padding:0; background-color:#f3f3f3; font-family:Arial, Helvetica, sans-serif; color:{DarkColor};'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color:#f3f3f3; margin:0; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                       style='max-width:760px; background-color:{WhiteColor}; border-radius:18px; overflow:hidden; box-shadow:0 8px 30px rgba(0,0,0,0.08);'>

                    <tr>
                        <td style='background:{DarkColor}; padding:30px 32px; text-align:center;'>
                            {logoHtml}
                            <div style='margin-top:18px; font-size:28px; line-height:1.3; color:{PrimaryColor}; font-weight:700;'>
                                Siparişiniz Alındı
                            </div>
                            <div style='margin-top:10px; font-size:15px; line-height:1.7; color:{WhiteColor};'>
                                Siparişiniz alınmıştır, en kısa sürede hazırlanacaktır.
                            </div>
                            {contactHtml}
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:32px 32px 12px 32px;'>
                            <p style='margin:0 0 16px 0; font-size:16px; line-height:1.8; color:{DarkColor};'>
                                Merhaba <strong>{customerName}</strong>,
                            </p>

                            <p style='margin:0; font-size:15px; line-height:1.9; color:{TextColor};'>
                                Tornado Toptan üzerinden oluşturduğunuz sipariş başarıyla tarafımıza ulaşmıştır.
                                Sipariş detaylarınızı aşağıda inceleyebilirsiniz.
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:12px 32px;'>
                            <div style='font-size:19px; font-weight:700; color:{DarkColor}; margin-bottom:14px;'>
                                Sipariş Bilgileri
                            </div>
                            {orderInfoHtml}
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:12px 32px 30px 32px;'>
                            <div style='font-size:19px; font-weight:700; color:{DarkColor}; margin-bottom:16px;'>
                                Sipariş Verilen Ürünler
                            </div>

                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                {productsHtml}
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:0 32px 30px 32px;'>
                            <div style='background:{LightBg}; border:1px solid {BorderColor}; border-radius:12px; padding:16px 18px; font-size:14px; color:{TextColor}; line-height:1.8;'>
                                Siparişiniz ile ilgili herhangi bir sorunuz olduğunda bizimle iletişime geçebilirsiniz.
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:22px 32px; text-align:center; background:{DarkColor};'>
                            <p style='margin:0; font-size:12px; line-height:1.8; color:{WhiteColor};'>
                                © {DateTime.Now.Year} Tornado Toptan
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        private static string BuildAdminTemplate(Order order, string? baseUrl, SiteSettings? siteSettings)
        {
            var logoHtml = BuildHeaderLogoHtml(siteSettings, baseUrl);
            var contactHtml = BuildContactInfoHtml(siteSettings);
            var productsHtml = BuildProductsHtml(order.OrderProducts, baseUrl);
            var orderInfoHtml = BuildCompanyInfoCard(order);

            return $@"
<!DOCTYPE html>
<html lang='tr'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Yeni Sipariş Bildirimi</title>
</head>
<body style='margin:0; padding:0; background-color:#f3f3f3; font-family:Arial, Helvetica, sans-serif; color:{DarkColor};'>
    <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color:#f3f3f3; margin:0; padding:30px 0;'>
        <tr>
            <td align='center'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'
                       style='max-width:760px; background-color:{WhiteColor}; border-radius:18px; overflow:hidden; box-shadow:0 8px 30px rgba(0,0,0,0.08);'>

                    <tr>
                        <td style='background:{DarkColor}; padding:30px 32px; text-align:center;'>
                            {logoHtml}
                            <div style='margin-top:18px; font-size:28px; line-height:1.3; color:{PrimaryColor}; font-weight:700;'>
                                Yeni Sipariş Alındı
                            </div>
                            <div style='margin-top:10px; font-size:15px; line-height:1.7; color:{WhiteColor};'>
                                Sisteme yeni bir sipariş kaydı düştü.
                            </div>
                            {contactHtml}
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:32px 32px 12px 32px;'>
                            <p style='margin:0; font-size:15px; line-height:1.9; color:{TextColor};'>
                                Aşağıda yeni siparişe ait müşteri ve ürün detaylarını bulabilirsiniz.
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:12px 32px;'>
                            <div style='font-size:19px; font-weight:700; color:{DarkColor}; margin-bottom:14px;'>
                                Sipariş Özeti
                            </div>
                            {orderInfoHtml}
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:12px 32px 30px 32px;'>
                            <div style='font-size:19px; font-weight:700; color:{DarkColor}; margin-bottom:16px;'>
                                Sipariş Verilen Ürünler
                            </div>

                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0'>
                                {productsHtml}
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style='padding:22px 32px; text-align:center; background:{DarkColor};'>
                            <p style='margin:0; font-size:12px; line-height:1.8; color:{WhiteColor};'>
                                © {DateTime.Now.Year} Tornado Toptan - Sipariş Bildirimi
                            </p>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }

        public static string OrderCustomerTemplate(Order order, string baseUrl, SiteSettings? siteSettings)
        {
            return BuildCustomerTemplate(order, baseUrl, siteSettings);
        }

        public static string OrderAdminTemplate(Order order, string baseUrl, SiteSettings? siteSettings)
        {
            return BuildAdminTemplate(order, baseUrl, siteSettings);
        }
    }
}