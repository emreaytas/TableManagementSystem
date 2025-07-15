using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace TableManagement.Application.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly SmtpClient _smtpClient;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpClient = new SmtpClient();
            ConfigureSmtpClient();
        }

        private void ConfigureSmtpClient()
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            _smtpClient.Host = emailSettings["SmtpHost"];
            _smtpClient.Port = int.Parse(emailSettings["SmtpPort"]);
            _smtpClient.EnableSsl = bool.Parse(emailSettings["EnableSsl"]);
            _smtpClient.UseDefaultCredentials = false;
            _smtpClient.Credentials = new NetworkCredential(
                emailSettings["SmtpUserName"],
                emailSettings["SmtpPassword"]
            );
        }

        public async Task<bool> SendEmailConfirmationAsync(string email, string userName, string confirmationLink)
        {
            var subject = "Email Adresinizi Doğrulayın - Tablo Yönetim Sistemi";
            var body = GetEmailConfirmationTemplate(userName, confirmationLink);

            return await SendEmailAsync(email, subject, body, true);
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
        {
            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");
                var fromEmail = emailSettings["FromEmail"];
                var fromName = emailSettings["FromName"];

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(to);

                await _smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Email gönderme hatası: {ex.Message}");
                return false;
            }
        }

        private string GetEmailConfirmationTemplate(string userName, string confirmationLink)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Email Doğrulama</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
        }}
        .container {{
            background-color: #f9f9f9;
            padding: 30px;
            border-radius: 10px;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            color: #1976D2;
            margin-bottom: 30px;
        }}
        .content {{
            background-color: white;
            padding: 25px;
            border-radius: 8px;
            margin-bottom: 20px;
        }}
        .button {{
            display: inline-block;
            background-color: #1976D2;
            color: white;
            padding: 12px 30px;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            margin: 20px 0;
        }}
        .button:hover {{
            background-color: #1565C0;
        }}
        .footer {{
            text-align: center;
            color: #666;
            font-size: 12px;
            margin-top: 20px;
        }}
        .warning {{
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
            padding: 15px;
            border-radius: 5px;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔐 Email Doğrulama</h1>
            <h2>Tablo Yönetim Sistemi</h2>
        </div>
        
        <div class='content'>
            <h3>Merhaba {userName}!</h3>
            
            <p>Tablo Yönetim Sistemi'ne kayıt olduğunuz için teşekkür ederiz.</p>
            
            <p>Hesabınızı aktifleştirmek için aşağıdaki butona tıklayarak email adresinizi doğrulayın:</p>
            
            <div style='text-align: center;'>
                <a href='{confirmationLink}' class='button'>
                    ✅ Email Adresimi Doğrula
                </a>
            </div>
            
            <div class='warning'>
                <strong>⚠️ Önemli:</strong>
                <ul>
                    <li>Bu link 24 saat geçerlidir</li>
                    <li>Email doğrulaması yapmazsanız hesabınız aktif olmayacaktır</li>
                    <li>Bu email'i siz talep etmediyseniz lütfen dikkate almayın</li>
                </ul>
            </div>
            
            <p>Eğer butona tıklayamıyorsanız, aşağıdaki linki tarayıcınıza kopyalayabilirsiniz:</p>
            <p style='word-break: break-all; color: #1976D2;'>{confirmationLink}</p>
        </div>
        
        <div class='footer'>
            <p>Bu email otomatik olarak gönderilmiştir. Lütfen yanıtlamayın.</p>
            <p>&copy; 2025 Tablo Yönetim Sistemi. Tüm hakları saklıdır.</p>
        </div>
    </div>
</body>
</html>";
        }

        public void Dispose()
        {
            _smtpClient?.Dispose();
        }
    }
}
