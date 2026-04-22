using MailKit.Security;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace ilkimPlastik.WEB.Tools
{
    public class MailSender : IMailSender
    {
        // SMTP bilgilerini config veya appsettings.json'dan almak daha güvenli olur
        private readonly string _smtpServer = "webmail.tornadotoptan.com";
        private readonly int _smtpPort = 465;
        private readonly string _fromEmail = "satis@tornadotoptan.com";
        private readonly string _password = "EDKutests39Mv";
        public async Task SendMailAsync(string toEmail, string subject, string htmlBody, string name)
        {


            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(name, _fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            try
            {
                using var client = new SmtpClient();

                // ConnectAsync ile TLS kullan
                await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.SslOnConnect);

                // Authenticate
                await client.AuthenticateAsync(_fromEmail, _password);

                // Mail gönder
                await client.SendAsync(message);

                // Disconnect
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // Hata loglaması
                Console.WriteLine($"Mail gönderilirken hata oluştu: {ex.Message}");
                throw;
            }
        }
    }
}
