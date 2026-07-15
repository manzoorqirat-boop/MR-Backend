using System.Net;
using System.Net.Mail;

namespace SiteReportApp.Services
{
    // Plain SMTP sender configured entirely by environment variables so the
    // same build works on Railway/anywhere:
    //   SMTP_HOST, SMTP_PORT (default 587), SMTP_USER, SMTP_PASS,
    //   SMTP_FROM (default = SMTP_USER), SMTP_SSL ("true" default)
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMTP_HOST"));

        public async Task SendAsync(IEnumerable<string> to, string subject, string body)
        {
            var recipients = to.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList();
            if (recipients.Count == 0) return;

            if (!IsConfigured)
                throw new InvalidOperationException("SMTP is not configured. Set SMTP_HOST / SMTP_PORT / SMTP_USER / SMTP_PASS env vars.");

            var host = Environment.GetEnvironmentVariable("SMTP_HOST")!;
            var port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var p) ? p : 587;
            var user = Environment.GetEnvironmentVariable("SMTP_USER") ?? "";
            var pass = Environment.GetEnvironmentVariable("SMTP_PASS") ?? "";
            var from = Environment.GetEnvironmentVariable("SMTP_FROM") ?? user;
            var ssl = !string.Equals(Environment.GetEnvironmentVariable("SMTP_SSL"), "false", StringComparison.OrdinalIgnoreCase);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = ssl,
                Credentials = string.IsNullOrEmpty(user) ? null : new NetworkCredential(user, pass)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(from, "QMS Site Reporting"),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            foreach (var r in recipients) message.To.Add(r);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent to {Count} recipient(s): {Subject}", recipients.Count, subject);
        }
    }
}
