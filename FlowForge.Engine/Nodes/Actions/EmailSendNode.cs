using System.Net;
using System.Net.Mail;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Nodes.Actions;

/// <summary>
/// An action node that sends emails via SMTP.
/// Supports HTML and plain text content with attachments.
/// </summary>
[NodeDefinition(
    Name = "Send Email",
    Description = "Send emails via SMTP with HTML or plain text content",
    Icon = "mail")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Result")]
[ConfigurationProperty("to", "string", Description = "Recipient email address(es), comma-separated", IsRequired = true)]
[ConfigurationProperty("subject", "string", Description = "Email subject", IsRequired = true)]
[ConfigurationProperty("body", "string", Description = "Email body content", IsRequired = true)]
[ConfigurationProperty("from", "string", Description = "Sender email address")]
[ConfigurationProperty("cc", "string", Description = "CC recipients, comma-separated")]
[ConfigurationProperty("bcc", "string", Description = "BCC recipients, comma-separated")]
[ConfigurationProperty("isHtml", "boolean", Description = "Whether body is HTML content")]
[ConfigurationProperty("smtpHost", "string", Description = "SMTP server hostname")]
[ConfigurationProperty("smtpPort", "number", Description = "SMTP server port")]
[ConfigurationProperty("enableSsl", "boolean", Description = "Enable SSL/TLS")]
[ConfigurationProperty("replyTo", "string", Description = "Reply-to email address")]
public class EmailSendNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();
    private readonly Func<SmtpClient>? _smtpClientFactory;

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "email-send";

    /// <summary>
    /// Creates a new EmailSendNode with default SmtpClient.
    /// </summary>
    public EmailSendNode() : this(null)
    {
    }

    /// <summary>
    /// Creates a new EmailSendNode with a custom SmtpClient factory (for testing).
    /// </summary>
    internal EmailSendNode(Func<SmtpClient>? smtpClientFactory)
    {
        _smtpClientFactory = smtpClientFactory;
    }

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var to = GetRequiredConfigValue<string>(input, "to");
            var subject = GetRequiredConfigValue<string>(input, "subject");
            var body = GetRequiredConfigValue<string>(input, "body");
            var from = GetConfigValue<string>(input, "from");
            var cc = GetConfigValue<string>(input, "cc");
            var bcc = GetConfigValue<string>(input, "bcc");
            var isHtml = GetConfigValue<bool?>(input, "isHtml") ?? false;
            var smtpHost = GetConfigValue<string>(input, "smtpHost");
            var smtpPort = GetConfigValue<int?>(input, "smtpPort");
            var enableSsl = GetConfigValue<bool?>(input, "enableSsl") ?? true;
            var replyTo = GetConfigValue<string>(input, "replyTo");

            // Get SMTP credentials if provided
            SmtpCredentials? credentials = null;
            if (input.CredentialId.HasValue)
            {
                credentials = await GetSmtpCredentialsAsync(input.CredentialId.Value, context);
            }

            // Use credentials for SMTP settings if not provided in config
            var host = smtpHost ?? credentials?.Host ?? throw new InvalidOperationException("SMTP host is required");
            var port = smtpPort ?? credentials?.Port ?? 587;
            var senderEmail = from ?? credentials?.Username ??
                throw new InvalidOperationException("Sender email is required");

            // Create mail message
            using var message = new MailMessage
            {
                From = new MailAddress(senderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            // Add recipients
            foreach (var recipient in ParseEmailAddresses(to))
            {
                message.To.Add(recipient);
            }

            // Add CC recipients
            if (!string.IsNullOrWhiteSpace(cc))
            {
                foreach (var recipient in ParseEmailAddresses(cc))
                {
                    message.CC.Add(recipient);
                }
            }

            // Add BCC recipients
            if (!string.IsNullOrWhiteSpace(bcc))
            {
                foreach (var recipient in ParseEmailAddresses(bcc))
                {
                    message.Bcc.Add(recipient);
                }
            }

            // Add reply-to
            if (!string.IsNullOrWhiteSpace(replyTo))
            {
                message.ReplyToList.Add(new MailAddress(replyTo));
            }

            // Create and configure SMTP client
            using var client = _smtpClientFactory?.Invoke() ?? new SmtpClient(host, port);
            client.EnableSsl = enableSsl;

            // Set credentials if available
            if (credentials is not null && !string.IsNullOrWhiteSpace(credentials.Username))
            {
                client.Credentials = new NetworkCredential(credentials.Username, credentials.Password);
            }

            // Send email
            await client.SendMailAsync(message, context.CancellationToken);

            var result = new Dictionary<string, object?>
            {
                ["success"] = true,
                ["to"] = to,
                ["subject"] = subject,
                ["messageId"] = message.Headers["Message-ID"],
                ["sentAt"] = DateTime.UtcNow.ToString("O")
            };

            return SuccessOutput(result);
        }
        catch (SmtpException ex)
        {
            return FailureOutput($"SMTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return FailureOutput($"Email send error: {ex.Message}");
        }
    }

    private static IEnumerable<MailAddress> ParseEmailAddresses(string addresses)
    {
        if (string.IsNullOrWhiteSpace(addresses))
            yield break;

        foreach (var address in addresses.Split(',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(address))
            {
                yield return new MailAddress(address);
            }
        }
    }

    private static async Task<SmtpCredentials?> GetSmtpCredentialsAsync(
        Guid credentialId,
        IExecutionContext context)
    {
        var credentials = await context.Credentials.GetCredentialAsync(credentialId, context.CancellationToken);

        if (credentials is null)
            return null;

        return new SmtpCredentials
        {
            Host = credentials.TryGetValue("host", out var host) ? host : null,
            Port = credentials.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port)
                ? port
                : null,
            Username = credentials.TryGetValue("username", out var username) ? username : null,
            Password = credentials.TryGetValue("password", out var password) ? password : null
        };
    }

    private sealed record SmtpCredentials
    {
        public string? Host { get; init; }
        public int? Port { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
    }
}
