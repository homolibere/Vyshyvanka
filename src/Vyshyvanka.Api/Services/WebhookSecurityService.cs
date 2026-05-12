using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Api.Services;

/// <summary>
/// Validates webhook request security: HMAC signature verification and IP allowlisting.
/// </summary>
public static class WebhookSecurityService
{
    /// <summary>Header name for the HMAC-SHA256 signature.</summary>
    public const string SignatureHeader = "X-Webhook-Signature";

    /// <summary>
    /// Validates the HMAC-SHA256 signature of the request body against the configured secret.
    /// </summary>
    /// <param name="body">The raw request body bytes.</param>
    /// <param name="signatureHeader">The value of the X-Webhook-Signature header (format: sha256=hex).</param>
    /// <param name="secret">The webhook secret configured on the trigger node.</param>
    /// <returns>True if the signature is valid.</returns>
    public static bool ValidateSignature(byte[] body, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        // Expected format: sha256=<hex-encoded-hmac>
        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHex = signatureHeader["sha256=".Length..];

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var expectedHash = HMACSHA256.HashData(secretBytes, body);
        var expectedHex = Convert.ToHexStringLower(expectedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedHex.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedHex));
    }

    /// <summary>
    /// Checks whether the client IP is in the allowed list.
    /// </summary>
    /// <param name="clientIp">The client's IP address string.</param>
    /// <param name="allowedIps">List of allowed IP addresses.</param>
    /// <returns>True if the IP is allowed.</returns>
    public static bool IsIpAllowed(string? clientIp, IReadOnlyList<string> allowedIps)
    {
        if (string.IsNullOrWhiteSpace(clientIp))
        {
            return false;
        }

        return allowedIps.Any(allowed =>
            allowed.Equals(clientIp, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts the webhook secret from the trigger node configuration.
    /// </summary>
    public static string? GetWebhookSecret(WorkflowNode triggerNode)
    {
        if (triggerNode.Configuration.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (triggerNode.Configuration.TryGetProperty("secret", out var secretProp) &&
            secretProp.ValueKind == JsonValueKind.String)
        {
            var secret = secretProp.GetString();
            return string.IsNullOrWhiteSpace(secret) ? null : secret;
        }

        return null;
    }

    /// <summary>
    /// Extracts the allowed IPs list from the trigger node configuration.
    /// </summary>
    public static IReadOnlyList<string>? GetAllowedIps(WorkflowNode triggerNode)
    {
        if (triggerNode.Configuration.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        if (triggerNode.Configuration.TryGetProperty("allowedIps", out var ipsProp) &&
            ipsProp.ValueKind == JsonValueKind.Array)
        {
            var ips = new List<string>();
            foreach (var ip in ipsProp.EnumerateArray())
            {
                var value = ip.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ips.Add(value);
                }
            }

            return ips.Count > 0 ? ips : null;
        }

        return null;
    }
}
