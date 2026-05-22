using System.Net.Http.Json;
using System.Text.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Base class for API clients providing shared HTTP infrastructure.
/// </summary>
public abstract class ApiClientBase(HttpClient httpClient)
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected HttpClient Http => httpClient;

    /// <summary>
    /// Ensures the response is successful, throwing ApiException with parsed error details if not.
    /// </summary>
    protected static async Task EnsureSuccessAsync(HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
            return;

        ApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
        }
        catch
        {
            // Failed to parse error response, will use status code message
        }

        if (error is not null && !string.IsNullOrEmpty(error.Message))
        {
            throw new ApiException(error, (int)response.StatusCode);
        }

        throw new ApiException(
            $"Request failed with status {(int)response.StatusCode}: {response.ReasonPhrase}",
            (int)response.StatusCode);
    }

    /// <summary>Checks if the response has a JSON content type.</summary>
    protected static bool IsJsonResponse(HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        return contentType is not null &&
               (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("text/json", StringComparison.OrdinalIgnoreCase));
    }
}
