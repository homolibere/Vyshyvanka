using System.Net.Http.Json;
using Vyshyvanka.Contracts.Auth;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for audit log operations.
/// </summary>
public class AuditLogApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Lists audit log entries with optional filtering.</summary>
    public async Task<AuditLogListResponse> GetLogsAsync(
        string? eventType = null,
        bool? success = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/audit-logs?skip={skip}&take={take}";

        if (!string.IsNullOrEmpty(eventType))
        {
            url += $"&eventType={Uri.EscapeDataString(eventType)}";
        }

        if (success.HasValue)
        {
            url += $"&success={success.Value.ToString().ToLowerInvariant()}";
        }

        return await Http.GetFromJsonAsync<AuditLogListResponse>(url, JsonOptions, cancellationToken)
               ?? new AuditLogListResponse();
    }
}
