using Vyshyvanka.Api.Authorization;
using Vyshyvanka.Api.Models;
using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// API controller for managing NuGet package sources.
/// </summary>
[ApiController]
[Route("api/packages/sources")]
[Produces("application/json")]
public class PackageSourceController : ControllerBase
{
    private readonly IPackageSourceService _sourceService;
    private readonly ILogger<PackageSourceController> _logger;

    public PackageSourceController(
        IPackageSourceService sourceService,
        ILogger<PackageSourceController> logger)
    {
        _sourceService = sourceService ?? throw new ArgumentNullException(nameof(sourceService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all configured package sources.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewPackages)]
    [ProducesResponseType(typeof(IReadOnlyList<PackageSourceResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PackageSourceResponse>> GetSources()
    {
        _logger.LogDebug("Getting package sources");

        var sources = _sourceService.GetSources();
        var response = sources.Select(PackageSourceResponse.FromSource).ToList();
        return Ok(response);
    }

    /// <summary>
    /// Adds a new package source.
    /// </summary>
    /// <param name="request">Source configuration.</param>
    [HttpPost]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(typeof(PackageSourceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PackageSourceResponse>> AddSource(
        [FromBody] PackageSourceRequest request)
    {
        _logger.LogInformation("Adding package source: {SourceName}, url={Url}", request.Name, request.Url);

        // Check if source already exists
        var existing = _sourceService.GetSources()
            .FirstOrDefault(s => s.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            return Conflict(new ApiError
            {
                Code = "SOURCE_EXISTS",
                Message = $"Package source '{request.Name}' already exists"
            });
        }

        try
        {
            var source = await _sourceService.AddSourceAsync(request.ToConfig());
            _logger.LogInformation("Package source added: {SourceName}", source.Name);

            return CreatedAtAction(
                nameof(GetSources),
                PackageSourceResponse.FromSource(source));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_SOURCE",
                Message = ex.Message
            });
        }
    }


    /// <summary>
    /// Updates an existing package source.
    /// </summary>
    /// <param name="name">Name of the source to update.</param>
    /// <param name="request">Updated source configuration.</param>
    [HttpPut("{name}")]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(typeof(PackageSourceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageSourceResponse>> UpdateSource(
        string name,
        [FromBody] PackageSourceRequest request)
    {
        _logger.LogInformation("Updating package source: {SourceName}", name);

        // Check if source exists
        var existing = _sourceService.GetSources()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return NotFound(new ApiError
            {
                Code = "SOURCE_NOT_FOUND",
                Message = $"Package source '{name}' was not found"
            });
        }

        try
        {
            await _sourceService.UpdateSourceAsync(name, request.ToConfig());

            // Get the updated source
            var updated = _sourceService.GetSources()
                .First(s => s.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase));

            _logger.LogInformation("Package source updated: {SourceName}", updated.Name);
            return Ok(PackageSourceResponse.FromSource(updated));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiError
            {
                Code = "INVALID_SOURCE",
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Removes a package source.
    /// </summary>
    /// <param name="name">Name of the source to remove.</param>
    [HttpDelete("{name}")]
    [Authorize(Policy = Policies.CanManagePackages)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSource(string name)
    {
        _logger.LogInformation("Removing package source: {SourceName}", name);

        // Check if source exists
        var existing = _sourceService.GetSources()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return NotFound(new ApiError
            {
                Code = "SOURCE_NOT_FOUND",
                Message = $"Package source '{name}' was not found"
            });
        }

        await _sourceService.RemoveSourceAsync(name);
        _logger.LogInformation("Package source removed: {SourceName}", name);
        return NoContent();
    }

    /// <summary>
    /// Tests connectivity to a package source.
    /// </summary>
    /// <param name="name">Name of the source to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{name}/test")]
    [Authorize(Policy = Policies.CanViewPackages)]
    [ProducesResponseType(typeof(SourceTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SourceTestResponse>> TestSource(
        string name,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Testing package source connectivity: {SourceName}", name);

        // Check if source exists
        var existing = _sourceService.GetSources()
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return NotFound(new ApiError
            {
                Code = "SOURCE_NOT_FOUND",
                Message = $"Package source '{name}' was not found"
            });
        }

        var result = await _sourceService.TestSourceAsync(name);
        return Ok(SourceTestResponse.FromResult(result));
    }
}
