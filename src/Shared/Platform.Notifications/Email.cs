using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Platform.Notifications;

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(IReadOnlyCollection<string> To, string Subject, string HtmlBody, IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailSendResult(bool Sent, string? Error)
{
    public static readonly EmailSendResult Success = new(true, null);
    public static EmailSendResult Failed(string error) => new(false, error);
    public static readonly EmailSendResult Disabled = new(false, "Email delivery is disabled");
}

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@localhost";
    public string FromName { get; set; } = "Platform";

    public static SmtpOptions FromConfiguration(IConfiguration configuration, string section = "Smtp")
    {
        var options = new SmtpOptions();
        configuration.GetSection(section).Bind(options);
        return options;
    }
}

/// <summary>
/// SMTP delivery via System.Net.Mail. When disabled (default in dev/test) the message is logged
/// and reported as not sent, so in-system notifications still work without a mail server.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email delivery disabled; would have sent '{Subject}' to {Count} recipient(s)", message.Subject, message.To.Count);
            return EmailSendResult.Disabled;
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            foreach (var to in message.To) mail.To.Add(to);
            foreach (var attachment in message.Attachments ?? Array.Empty<EmailAttachment>())
            {
                mail.Attachments.Add(new Attachment(new MemoryStream(attachment.Content), attachment.FileName, attachment.ContentType));
            }

            using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
            if (!string.IsNullOrEmpty(_options.Username))
            {
                client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }
            await client.SendMailAsync(mail, cancellationToken);
            return EmailSendResult.Success;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(ex, "Email '{Subject}' could not be delivered", message.Subject);
            return EmailSendResult.Failed(ex.Message);
        }
    }
}

/// <summary>Renders "{{Name}}" placeholders; values are HTML-encoded unless the body is plain text.</summary>
public static partial class TemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}")]
    private static partial Regex Placeholder();

    public static string Render(string template, IReadOnlyDictionary<string, string?> values, bool htmlEncode = true) =>
        Placeholder().Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            if (!values.TryGetValue(key, out var value) || value is null) return string.Empty;
            return htmlEncode ? WebUtility.HtmlEncode(value) : value;
        });
}
