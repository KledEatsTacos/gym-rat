using Microsoft.AspNetCore.Identity.UI.Services;

namespace gym_rat.Services
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // For now, we just log it or do nothing.
            // In a real app, you would connect to SendGrid or SMTP here.
            return Task.CompletedTask;
        }
    }
}
