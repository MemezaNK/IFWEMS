using System.Net;
using System.Net.Mail;
using IFWEMS.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IFWEMS.Infrastructure.Notifications;

/// <summary>
/// Pluggable SMTP-based email sender. Connection details are configuration-driven so the
/// mail provider can be swapped without code changes. FR-005.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        var smtpSection = _configuration.GetSection("Smtp");
        var host = smtpSection["Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogInformation("SMTP not configured; skipping email send to {ToEmail} (subject: {Subject}).", toEmail, subject);
            return;
        }

        using var client = new SmtpClient(host, int.TryParse(smtpSection["Port"], out var port) ? port : 587)
        {
            EnableSsl = bool.TryParse(smtpSection["EnableSsl"], out var ssl) && ssl,
            Credentials = new NetworkCredential(smtpSection["Username"], smtpSection["Password"])
        };

        using var message = new MailMessage(smtpSection["FromAddress"] ?? "noreply@ifwems.local", toEmail, subject, bodyHtml)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
