using Microsoft.AspNetCore.Identity.UI.Services; // <--- This is the correct interface
using Microsoft.Extensions.Configuration;
using System; // For Console.WriteLine and Exception
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;

namespace Kutip.Services
{
    // Now implementing the correct IEmailSender from Microsoft.AspNetCore.Identity.UI.Services
    public class EmailSender : IEmailSender // <--- This line is crucial
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            // Get SMTP settings from appsettings.json
            var smtpHost = _configuration["SmtpSettings:Host"];
            var smtpPort = int.Parse(_configuration["SmtpSettings:Port"]);
            var smtpUsername = _configuration["SmtpSettings:Username"];
            var smtpPassword = _configuration["SmtpSettings:Password"];
            var smtpEnableSsl = bool.Parse(_configuration["SmtpSettings:EnableSsl"]);
            var fromEmail = _configuration["SmtpSettings:FromEmail"];
            var fromName = _configuration["SmtpSettings:FromName"];

            using (var client = new SmtpClient(smtpHost, smtpPort))
            {
                client.EnableSsl = smtpEnableSsl;
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                client.DeliveryMethod = SmtpDeliveryMethod.Network;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true,
                };
                mailMessage.To.Add(email);

                try
                {
                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine($"Email sent to {email} successfully.");
                }
                catch (SmtpException ex)
                {
                    Console.WriteLine($"SMTP Error sending email to {email}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    }
                    // Consider logging this error more robustly in a real application
                    // throw; // Re-throw if you want the error to propagate
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"General Error sending email to {email}: {ex.Message}");
                    // throw; // Re-throw if you want the error to propagate
                }
            }
        }
    }
}