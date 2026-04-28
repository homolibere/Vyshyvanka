using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for NuGet package management operations.
/// </summary>
[ApiController]
[Route("api/packages")]
[Produces("application/json")]
public class PackageController : ControllerBase
{
    private readonly INuGetPackageManager _packageManager;
    private readonly ILogger<PackageController> _logger;

    public PackageController(
        INuGetPackageManager packageManager,
        ILogger<PackageController> logger)
    {
        _packageManager = packageManager ?? throw new ArgumentNullException(nameof(packageManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Searches for packages across configured sources.
    /// </summary>
    /// <param name="query">Search query string.</param>
    /// <param name="skip">Number of results to skip for pagination.</param>
    /// <param name="take">Maximum number of results to return.</param>
    /// <param name="includePrerelease">Whether to include prerelease packages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("search")]
    [Authorize(Policy = Policies.CanViewPackages)]
    [ProducesResponseType(typeof(PackageSearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PackageSearchResponse>> Search(
        [FromQuery] string query,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching packages: query={Query}, skip={Skip}, take={Take}", query, skip, take);

        var options = new PackageSearchOptions
        {
            Skip = skip,
            Take = take,
            IncludePrerelease = includePrerelease
        };

        var result = await _packageManager.SearchPackagesAsync(query, options, cancellationToken);
        return Ok(PackageSearchResponse.FromResult(result));
    }


    /// <summary>
    /// Gets all installed packages.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewPackages)]
    [ProducesResponseType(typeof(IReadOnlyList<InstalledPackageResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<InstalledPackageResponse>> GetInstalled()
    {
        _logger.LogDebug("Getting installed packages");

        var packages = _packageManager.GetInstalledPackages();
        var response = packages.Select(InstalledPackageResponse.FromPackage).ToList();
        return Ok(response);
    }

    /// <summary>
    /// Gets detailed information about a specific package.
    /// </summary>
    /// <param name="id">Package identifier.</param>
    /// <param name="version">Optional specific version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("{id}")]
    [Authorize(Policy = Policies.CanViewPackages)]
    [ProducesResponseType(typeof(PackageDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageDetailsResponse>> GetDetails(
        string id,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting package details: {PackageId}, version={Version}", id, version);

        NuGetVersion? nugetVersion = null;
        if (!string.IsNullOrWhiteSpace(version) && !NuGetVersion.TryParse(version, out nugetVersion))
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_VERSION",
                Message = $"Invalid version format: '{version}'"
            });
        }

        var details = await _packageManager.GetPackageDetailsAsync(id, nugetVersion, cancellationToken);
        if (details is null)
        {
            return NotFound(new ApiError
            {
                Code = "PACKAGE_NOT_FOUND",
                Message = $"Package '{id}' was not found"
            });
        }

        return Ok(PackageDetailsResponse.FromDetails(details));
    }


    /// <summary>
    /// Installs a package and its dependencies.
    /// </summary>
    /// <param name="id">Package identifier to install.</param>
    /// <param name="request">Installation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id}/install")]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(typeof(PackageInstallResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PackageInstallResponse>> Install(
        string id,
        [FromBody] InstallPackageRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Installing package: {PackageId}, version={Version}", id, request?.Version);

        NuGetVersion? nugetVersion = null;
        if (!string.IsNullOrWhiteSpace(request?.Version) && !NuGetVersion.TryParse(request.Version, out nugetVersion))
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_VERSION",
                Message = $"Invalid version format: '{request.Version}'"
            });
        }

        var result = await _packageManager.InstallPackageAsync(
            id,
            nugetVersion,
            request?.Prerelease ?? false,
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Package installation failed: {PackageId}, errors={Errors}", id, result.Errors);
            return BadRequest(new ApiError
            {
                Code = "INSTALLATION_FAILED",
                Message = "Package installation failed",
                Details = new Dictionary<string, string[]> { ["errors"] = result.Errors.ToArray() }
            });
        }

        _logger.LogInformation("Package installed successfully: {PackageId} v{Version}",
            result.Package?.PackageId, result.Package?.Version);
        return Ok(PackageInstallResponse.FromResult(result));
    }

    /// <summary>
    /// Updates an installed package to a newer version.
    /// </summary>
    /// <param name="id">Package identifier to update.</param>
    /// <param name="request">Update options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id}/update")]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(typeof(PackageUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageUpdateResponse>> Update(
        string id,
        [FromBody] UpdatePackageRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating package: {PackageId}, targetVersion={Version}", id, request?.TargetVersion);

        // Check if package is installed
        var installed = _packageManager.GetInstalledPackages()
            .FirstOrDefault(p => p.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (installed is null)
        {
            return NotFound(new ApiError
            {
                Code = "PACKAGE_NOT_INSTALLED",
                Message = $"Package '{id}' is not installed"
            });
        }

        NuGetVersion? targetVersion = null;
        if (!string.IsNullOrWhiteSpace(request?.TargetVersion) &&
            !NuGetVersion.TryParse(request.TargetVersion, out targetVersion))
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_VERSION",
                Message = $"Invalid version format: '{request.TargetVersion}'"
            });
        }

        var result = await _packageManager.UpdatePackageAsync(id, targetVersion, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Package update failed: {PackageId}, errors={Errors}", id, result.Errors);
            return BadRequest(new ApiError
            {
                Code = "UPDATE_FAILED",
                Message = "Package update failed",
                Details = new Dictionary<string, string[]> { ["errors"] = result.Errors.ToArray() }
            });
        }

        _logger.LogInformation("Package updated successfully: {PackageId} v{OldVersion} -> v{NewVersion}",
            id, result.PreviousVersion, result.Package?.Version);
        return Ok(PackageUpdateResponse.FromResult(result));
    }


    /// <summary>
    /// Uninstalls a package.
    /// </summary>
    /// <param name="id">Package identifier to uninstall.</param>
    /// <param name="force">Force uninstall even if workflows reference the package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(typeof(PackageUninstallResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageUninstallResponse>> Uninstall(
        string id,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Uninstalling package: {PackageId}, force={Force}", id, force);

        // Check if package is installed
        var installed = _packageManager.GetInstalledPackages()
            .FirstOrDefault(p => p.PackageId.Equals(id, StringComparison.OrdinalIgnoreCase));

        if (installed is null)
        {
            return NotFound(new ApiError
            {
                Code = "PACKAGE_NOT_INSTALLED",
                Message = $"Package '{id}' is not installed"
            });
        }

        var result = await _packageManager.UninstallPackageAsync(id, force, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Package uninstallation failed: {PackageId}, errors={Errors}", id, result.Errors);

            // If there are affected workflows and force wasn't used, return a specific error
            if (result.AffectedWorkflows.Count > 0 && !force)
            {
                return BadRequest(new ApiError
                {
                    Code = "PACKAGE_IN_USE",
                    Message = "Package is referenced by workflows. Use force=true to uninstall anyway.",
                    Details = new Dictionary<string, string[]>
                        { ["affectedWorkflows"] = result.AffectedWorkflows.ToArray() }
                });
            }

            return BadRequest(new ApiError
            {
                Code = "UNINSTALL_FAILED",
                Message = "Package uninstallation failed",
                Details = new Dictionary<string, string[]> { ["errors"] = result.Errors.ToArray() }
            });
        }

        _logger.LogInformation("Package uninstalled successfully: {PackageId}", id);
        return Ok(PackageUninstallResponse.FromResult(result));
    }

    /// <summary>
    /// Checks for available updates to installed packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("updates")]
    [Authorize(Policy = Policies.CanViewPackages)]
    [ProducesResponseType(typeof(IReadOnlyList<PackageUpdateInfoResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PackageUpdateInfoResponse>>> CheckForUpdates(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking for package updates");

        var updates = await _packageManager.CheckForUpdatesAsync(cancellationToken);
        var response = updates.Select(PackageUpdateInfoResponse.FromInfo).ToList();
        return Ok(response);
    }

    /// <summary>
    /// Uploads and installs a .nupkg file from the local machine.
    /// </summary>
    /// <param name="file">The .nupkg file to upload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("upload")]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(typeof(PackageInstallResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100 MB
    public async Task<ActionResult<PackageInstallResponse>> Upload(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ApiError
            {
                Code = "NO_FILE",
                Message = "No file was uploaded"
            });
        }

        if (!file.FileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_FILE_TYPE",
                Message = "Only .nupkg files are accepted"
            });
        }

        _logger.LogInformation("Uploading package file: {FileName} ({Size} bytes)", file.FileName, file.Length);

        await using var stream = file.OpenReadStream();
        var result = await _packageManager.InstallFromStreamAsync(stream, file.FileName, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Package upload failed: {FileName}, errors={Errors}", file.FileName, result.Errors);
            return BadRequest(new ApiError
            {
                Code = "UPLOAD_INSTALL_FAILED",
                Message = "Package installation failed",
                Details = new Dictionary<string, string[]> { ["errors"] = result.Errors.ToArray() }
            });
        }

        _logger.LogInformation("Package uploaded and installed: {PackageId} v{Version}",
            result.Package?.PackageId, result.Package?.Version);
        return Ok(PackageInstallResponse.FromResult(result));
    }
}
