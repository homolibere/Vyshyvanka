using System.Net.Http.Json;
using FlowForge.Designer.Models;

namespace FlowForge.Designer.Services;

/// <summary>
/// Package management methods for the FlowForge API client.
/// </summary>
public partial class FlowForgeApiClient
{
    /// <summary>
    /// Gets all installed packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of installed packages.</returns>
    public async Task<List<InstalledPackageModel>> GetInstalledPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/packages", cancellationToken);

        if (!response.IsSuccessStatusCode || !IsJsonResponse(response))
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<List<InstalledPackageApiResponse>>(cancellationToken);
        return result?.Select(r => r.ToModel()).ToList() ?? [];
    }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        return contentType is not null &&
               (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("text/json", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Searches for packages across configured sources.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="skip">Number of results to skip for pagination.</param>
    /// <param name="take">Maximum number of results to return.</param>
    /// <param name="includePrerelease">Whether to include prerelease packages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results with matching packages.</returns>
    public async Task<PackageSearchResultModel> SearchPackagesAsync(
        string query,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/packages/search?query={Uri.EscapeDataString(query)}&skip={skip}&take={take}&includePrerelease={includePrerelease}";
        var response = await _httpClient.GetFromJsonAsync<PackageSearchApiResponse>(url, cancellationToken);

        return response?.ToModel() ?? new PackageSearchResultModel();
    }

    /// <summary>
    /// Gets detailed information about a specific package.
    /// </summary>
    /// <param name="packageId">Package identifier.</param>
    /// <param name="version">Optional specific version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Package details or null if not found.</returns>
    public async Task<PackageDetailsModel?> GetPackageDetailsAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(version)
            ? $"api/packages/{Uri.EscapeDataString(packageId)}"
            : $"api/packages/{Uri.EscapeDataString(packageId)}?version={Uri.EscapeDataString(version)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageDetailsApiResponse>(cancellationToken);
        return apiResponse?.ToModel();
    }

    /// <summary>
    /// Installs a package and its dependencies.
    /// </summary>
    /// <param name="packageId">Package identifier to install.</param>
    /// <param name="version">Optional specific version to install.</param>
    /// <param name="prerelease">Whether to allow prerelease versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installation result.</returns>
    public async Task<PackageInstallResultModel> InstallPackageAsync(
        string packageId,
        string? version = null,
        bool prerelease = false,
        CancellationToken cancellationToken = default)
    {
        var request = new InstallPackageApiRequest
        {
            Version = version,
            Prerelease = prerelease
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"api/packages/{Uri.EscapeDataString(packageId)}/install",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return new PackageInstallResultModel
            {
                Success = false,
                Errors = error?.GetErrors() ?? [$"Installation failed with status {response.StatusCode}"]
            };
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageInstallApiResponse>(cancellationToken);
        return apiResponse?.ToModel() ?? new PackageInstallResultModel
            { Success = false, Errors = ["Invalid response"] };
    }

    /// <summary>
    /// Updates an installed package to a newer version.
    /// </summary>
    /// <param name="packageId">Package identifier to update.</param>
    /// <param name="targetVersion">Optional target version to update to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result.</returns>
    public async Task<PackageUpdateResultModel> UpdatePackageAsync(
        string packageId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePackageApiRequest
        {
            TargetVersion = targetVersion
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"api/packages/{Uri.EscapeDataString(packageId)}/update",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return new PackageUpdateResultModel
            {
                Success = false,
                Errors = error?.GetErrors() ?? [$"Update failed with status {response.StatusCode}"]
            };
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageUpdateApiResponse>(cancellationToken);
        return apiResponse?.ToModel() ?? new PackageUpdateResultModel
            { Success = false, Errors = ["Invalid response"] };
    }

    /// <summary>
    /// Uninstalls a package.
    /// </summary>
    /// <param name="packageId">Package identifier to uninstall.</param>
    /// <param name="force">Force uninstall even if workflows reference the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Uninstall result.</returns>
    public async Task<PackageUninstallResultModel> UninstallPackageAsync(
        string packageId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/packages/{Uri.EscapeDataString(packageId)}?force={force}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return new PackageUninstallResultModel
            {
                Success = false,
                AffectedWorkflows = error?.GetAffectedWorkflows() ?? [],
                Errors = error?.GetErrors() ?? [$"Uninstall failed with status {response.StatusCode}"]
            };
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageUninstallApiResponse>(cancellationToken);
        return apiResponse?.ToModel() ?? new PackageUninstallResultModel
            { Success = false, Errors = ["Invalid response"] };
    }

    /// <summary>
    /// Checks for available updates to installed packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available updates.</returns>
    public async Task<List<PackageUpdateInfoModel>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<PackageUpdateInfoApiResponse>>(
            "api/packages/updates",
            cancellationToken);

        return response?.Select(r => r.ToModel()).ToList() ?? [];
    }
}

// Package Source Management Methods

public partial class FlowForgeApiClient
{
    /// <summary>
    /// Gets all configured package sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of package sources.</returns>
    public async Task<List<PackageSourceModel>> GetPackageSourcesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/packages/sources", cancellationToken);

        if (!response.IsSuccessStatusCode || !IsJsonResponse(response))
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<List<PackageSourceApiResponse>>(cancellationToken);
        return result?.Select(r => r.ToModel()).ToList() ?? [];
    }

    /// <summary>
    /// Adds a new package source.
    /// </summary>
    /// <param name="source">Source configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created package source, or null if creation failed.</returns>
    public async Task<PackageSourceModel?> AddPackageSourceAsync(
        PackageSourceModel source,
        CancellationToken cancellationToken = default)
    {
        var request = PackageSourceApiRequest.FromModel(source);
        var response = await _httpClient.PostAsJsonAsync("api/packages/sources", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageSourceApiResponse>(cancellationToken);
        return apiResponse?.ToModel();
    }

    /// <summary>
    /// Updates an existing package source.
    /// </summary>
    /// <param name="name">Name of the source to update.</param>
    /// <param name="source">Updated source configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if update succeeded, false otherwise.</returns>
    public async Task<bool> UpdatePackageSourceAsync(
        string name,
        PackageSourceModel source,
        CancellationToken cancellationToken = default)
    {
        var request = PackageSourceApiRequest.FromModel(source);
        var response = await _httpClient.PutAsJsonAsync(
            $"api/packages/sources/{Uri.EscapeDataString(name)}",
            request,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Removes a package source.
    /// </summary>
    /// <param name="name">Name of the source to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if removal succeeded, false otherwise.</returns>
    public async Task<bool> RemovePackageSourceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"api/packages/sources/{Uri.EscapeDataString(name)}",
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Tests connectivity to a package source.
    /// </summary>
    /// <param name="name">Name of the source to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Test result with success status and response time.</returns>
    public async Task<SourceTestResultModel> TestPackageSourceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/packages/sources/{Uri.EscapeDataString(name)}/test",
            null,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new SourceTestResultModel
            {
                Success = false,
                SourceName = name,
                ResponseTimeMs = 0,
                ErrorMessage = $"Test failed with status {response.StatusCode}"
            };
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<SourceTestApiResponse>(cancellationToken);
        return apiResponse?.ToModel() ?? new SourceTestResultModel
        {
            Success = false,
            SourceName = name,
            ResponseTimeMs = 0,
            ErrorMessage = "Invalid response"
        };
    }
}
