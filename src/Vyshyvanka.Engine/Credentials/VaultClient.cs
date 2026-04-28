using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Engine.Credentials;

/// <summary>
/// HTTP client for the Vault / OpenBao KV v2 secrets engine.
/// </summary>
public sealed class VaultClient : IVaultClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _mountPath;
    private readonly string _pathPrefix;
    private readonly ILogger<VaultClient> _logger;

    public VaultClient(CredentialStorageSettings settings, ILogger<VaultClient> logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _logger = logger;
        _mountPath = settings.MountPath.Trim('/');
        _pathPrefix = settings.PathPrefix.Trim('/');

        var handler = new HttpClientHandler();
        if (settings.SkipTlsVerify)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(settings.Url!.TrimEnd('/') + "/")
        };

        var token = settings.Token
                    ?? Environment.GetEnvironmentVariable("VAULT_TOKEN");

        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Add("X-Vault-Token", token);
        }
    }

    public async Task WriteSecretAsync(
        string path,
        Dictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/{_mountPath}/data/{_pathPrefix}/{path}";
        var payload = new VaultWriteRequest { Data = data };

        var response = await _http.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogDebug("Wrote secret to Vault at {Path}", path);
    }

    public async Task<Dictionary<string, string>?> ReadSecretAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var url = $"v1/{_mountPath}/data/{_pathPrefix}/{path}";

        var response = await _http.GetAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<VaultReadResponse>(cancellationToken);
        return result?.Data?.Data;
    }

    public async Task DeleteSecretAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        // Delete metadata and all versions (permanent delete)
        var url = $"v1/{_mountPath}/metadata/{_pathPrefix}/{path}";

        var response = await _http.DeleteAsync(url, cancellationToken);

        if (response.StatusCode is not HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }

        _logger.LogDebug("Deleted secret from Vault at {Path}", path);
    }

    public void Dispose() => _http.Dispose();

    // KV v2 request/response models

    private record VaultWriteRequest
    {
        [JsonPropertyName("data")]
        public Dictionary<string, string> Data { get; init; } = new();
    }

    private record VaultReadResponse
    {
        [JsonPropertyName("data")]
        public VaultDataWrapper? Data { get; init; }
    }

    private record VaultDataWrapper
    {
        [JsonPropertyName("data")]
        public Dictionary<string, string>? Data { get; init; }

        [JsonPropertyName("metadata")]
        public JsonElement? Metadata { get; init; }
    }
}
