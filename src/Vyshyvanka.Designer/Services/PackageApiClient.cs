using System.Net.Http.Json;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// API client for package management: install, update, uninstall, search, and source management.
/// </summary>
public class PackageApiClient(HttpClient httpClient) : ApiClientBase(httpClient)
{
    /// <summary>Gets all installed packages.</summary>
    public async Task<List<InstalledPackageModel>> GetInstalledPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/packages", cancellationToken);

        if (!response.IsSuccessStatusCode || !IsJsonResponse(response))
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<List<InstalledPackageApiResponse>>(cancellationToken);
        return result?.Select(r => r.ToModel()).ToList() ?? [];
    }

    /// <summary>Searches for packages across configured sources.</summary>
    public async Task<PackageSearchResultModel> SearchPackagesAsync(
        string query,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/packages/search?query={Uri.EscapeDataString(query)}&skip={skip}&take={take}&includePrerelease={includePrerelease}";
        var response = await Http.GetFromJsonAsync<PackageSearchApiResponse>(url, cancellationToken);

        return response?.ToModel() ?? new PackageSearchResultModel();
    }

    /// <summary>Gets detailed information about a specific package.</summary>
    public async Task<PackageDetailsModel?> GetPackageDetailsAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(version)
            ? $"api/packages/{Uri.EscapeDataString(packageId)}"
            : $"api/packages/{Uri.EscapeDataString(packageId)}?version={Uri.EscapeDataString(version)}";

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageDetailsApiResponse>(cancellationToken);
        return apiResponse?.ToModel();
    }

    /// <summary>Installs a package and its dependencies.</summary>
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

        var response = await Http.PostAsJsonAsync(
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

    /// <summary>Updates an installed package to a newer version.</summary>
    public async Task<PackageUpdateResultModel> UpdatePackageAsync(
        string packageId,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdatePackageApiRequest
        {
            TargetVersion = targetVersion
        };

        var response = await Http.PostAsJsonAsync(
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

    /// <summary>Uninstalls a package.</summary>
    public async Task<PackageUninstallResultModel> UninstallPackageAsync(
        string packageId,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/packages/{Uri.EscapeDataString(packageId)}?force={force}";
        var response = await Http.DeleteAsync(url, cancellationToken);

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

    /// <summary>Checks for available updates to installed packages.</summary>
    public async Task<List<PackageUpdateInfoModel>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.GetFromJsonAsync<List<PackageUpdateInfoApiResponse>>(
            "api/packages/updates",
            cancellationToken);

        return response?.Select(r => r.ToModel()).ToList() ?? [];
    }

    /// <summary>Uploads and installs a .nupkg file from the local machine.</summary>
    public async Task<PackageInstallResultModel> UploadPackageAsync(
        string fileName,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(streamContent, "file", fileName);

        var response = await Http.PostAsync("api/packages/upload", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return new PackageInstallResultModel
            {
                Success = false,
                Errors = error?.GetErrors() ?? [$"Upload failed with status {response.StatusCode}"]
            };
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageInstallApiResponse>(cancellationToken);
        return apiResponse?.ToModel() ?? new PackageInstallResultModel
            { Success = false, Errors = ["Invalid response"] };
    }

    /// <summary>Gets all configured package sources.</summary>
    public async Task<List<PackageSourceModel>> GetPackageSourcesAsync(CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/packages/sources", cancellationToken);

        if (!response.IsSuccessStatusCode || !IsJsonResponse(response))
        {
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<List<PackageSourceApiResponse>>(cancellationToken);
        return result?.Select(r => r.ToModel()).ToList() ?? [];
    }

    /// <summary>Adds a new package source.</summary>
    public async Task<PackageSourceModel?> AddPackageSourceAsync(
        PackageSourceModel source,
        CancellationToken cancellationToken = default)
    {
        var request = PackageSourceApiRequest.FromModel(source);
        var response = await Http.PostAsJsonAsync("api/packages/sources", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var apiResponse = await response.Content.ReadFromJsonAsync<PackageSourceApiResponse>(cancellationToken);
        return apiResponse?.ToModel();
    }

    /// <summary>Updates an existing package source.</summary>
    public async Task<bool> UpdatePackageSourceAsync(
        string name,
        PackageSourceModel source,
        CancellationToken cancellationToken = default)
    {
        var request = PackageSourceApiRequest.FromModel(source);
        var response = await Http.PutAsJsonAsync(
            $"api/packages/sources/{Uri.EscapeDataString(name)}",
            request,
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>Removes a package source.</summary>
    public async Task<bool> RemovePackageSourceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync(
            $"api/packages/sources/{Uri.EscapeDataString(name)}",
            cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>Tests connectivity to a package source.</summary>
    public async Task<SourceTestResultModel> TestPackageSourceAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync(
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
