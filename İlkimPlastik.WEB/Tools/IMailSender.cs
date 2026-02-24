namespace ilkimPlastik.WEB.Tools
{
    public interface IMailSender
    {
        Task SendMailAsync(string toEmail, string subject, string htmlBody, string name);
    }
}
