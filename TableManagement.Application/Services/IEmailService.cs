namespace TableManagement.Application.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailConfirmationAsync(string email, string userName, string confirmationLink);
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    }
}
