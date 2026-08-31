using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConceptFactory.Utils
{
    // Sends the password-recovery code email through Gmail's SMTP relay.
    // Reads credentials from configuration ("EmailSettings" in
    // appsettings.json) — see that file for setup notes. Gmail requires a
    // 16-character "App Password" here, NOT the account's normal login
    // password (Google Account → Security → 2-Step Verification → App
    // Passwords).
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // Returns true if the email was handed off to Gmail successfully.
        // Never throws — a misconfigured/missing App Password shouldn't
        // crash the forgot-password flow; the caller falls back to
        // showing the code on-screen in Development so the rest of the
        // feature stays testable without real Gmail credentials wired up.
        public async Task<bool> SendPasswordResetCodeAsync(string toEmail, string recipientName, string code)
        {
            try
            {
                string senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
                string senderAppPassword = _configuration["EmailSettings:SenderAppPassword"] ?? "";
                string senderName = _configuration["EmailSettings:SenderName"] ?? "The Concept Factory";

                if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderAppPassword))
                {
                    _logger.LogWarning("EmailSettings is not configured — skipping password reset email to {Email}.", toEmail);
                    return false;
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = "Your Concept Factory password reset code",
                    IsBodyHtml = true,
                    Body = $@"
                        <div style='font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto'>
                            <h2 style='color:#5B5EF4;margin-bottom:4px;'>Password Reset</h2>
                            <p>Hi {WebUtility.HtmlEncode(recipientName)},</p>
                            <p>Use the code below to reset your Concept Factory account password. This code expires in 15 minutes.</p>
                            <div style='font-size:28px;font-weight:700;letter-spacing:6px;background:#f3f3ff;color:#5B5EF4;padding:14px 20px;border-radius:8px;text-align:center;margin:20px 0;'>{code}</div>
                            <p>If you didn't request this, you can safely ignore this email — your password will not change.</p>
                        </div>"
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl = true,
                    Credentials = new NetworkCredential(senderEmail, senderAppPassword)
                };

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}.", toEmail);
                return false;
            }
        }
    }
}
