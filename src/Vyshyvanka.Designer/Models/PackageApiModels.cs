namespace Vyshyvanka.Designer.Models;

/// <summary>
/// API response for installed package.
/// </summary>
internal record InstalledPackageApiResponse
{
    public string PackageId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public string InstallPath { get; init; } = string.Empty;
    public DateTime InstalledAt { get; init; }
    public IReadOnlyList<string> NodeTypes { get; init; } = [];
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public bool IsLoaded { get; init; }

    public InstalledPackageModel ToModel() => new()
    {
        PackageId = PackageId,
        Version = Version,
        SourceName = SourceName,
        InstalledAt = InstalledAt,
        NodeTypes = NodeTypes,
        IsLoaded = IsLoaded,
        HasUpdate = false,
        LatestVersion = null
    };
}

/// <summary>
/// API response for package search results.
/// </summary>
internal record PackageSearchApiResponse
{
    public IReadOnlyList<PackageSearchItemApiResponse> Packages { get; init; } = [];
    public int TotalCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public PackageSearchResultModel ToModel() => new()
    {
        Packages = Packages.Select(p => p.ToModel()).ToList(),
        TotalCount = TotalCount,
        Errors = Errors
    };
}

/// <summary>
/// API response for a package search item.
/// </summary>
internal record PackageSearchItemApiResponse
{
    public string PackageId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Authors { get; init; }
    public long DownloadCount { get; init; }
    public string? IconUrl { get; init; }
    public string? ProjectUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool IsInstalled { get; init; }
    public string? InstalledVersion { get; init; }

    public PackageSearchItemModel ToModel() => new()
    {
        PackageId = PackageId,
        Title = Title,
        LatestVersion = LatestVersion,
        Description = Description,
        Authors = Authors,
        DownloadCount = DownloadCount,
        IconUrl = IconUrl,
        Tags = Tags,
        IsInstalled = IsInstalled,
        InstalledVersion = InstalledVersion
    };
}

/// <summary>
/// API response for package details.
/// </summary>
internal record PackageDetailsApiResponse
{
    public string PackageId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Authors { get; init; }
    public string? License { get; init; }
    public string? ProjectUrl { get; init; }
    public string? IconUrl { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> Dependencies { get; init; } = [];
    public IReadOnlyList<string> AllVersions { get; init; } = [];
    public bool IsInstalled { get; init; }
    public string? InstalledVersion { get; init; }

    public PackageDetailsModel ToModel() => new()
    {
        PackageId = PackageId,
        Version = Version,
        Title = Title,
        Description = Description,
        Authors = Authors,
        License = License,
        ProjectUrl = ProjectUrl,
        IconUrl = IconUrl,
        Tags = Tags,
        Dependencies = Dependencies,
        AllVersions = AllVersions,
        NodeTypes = [],
        IsInstalled = IsInstalled,
        InstalledVersion = InstalledVersion
    };
}

/// <summary>
/// API response for package installation result.
/// </summary>
internal record PackageInstallApiResponse
{
    public bool Success { get; init; }
    public InstalledPackageApiResponse? Package { get; init; }
    public IReadOnlyList<InstalledPackageApiResponse> InstalledDependencies { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public PackageInstallResultModel ToModel() => new()
    {
        Success = Success,
        Package = Package?.ToModel(),
        Errors = Errors,
        Warnings = Warnings
    };
}

/// <summary>
/// API response for package update result.
/// </summary>
internal record PackageUpdateApiResponse
{
    public bool Success { get; init; }
    public InstalledPackageApiResponse? Package { get; init; }
    public string? PreviousVersion { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public PackageUpdateResultModel ToModel() => new()
    {
        Success = Success,
        Package = Package?.ToModel(),
        PreviousVersion = PreviousVersion,
        Errors = Errors
    };
}

/// <summary>
/// API response for package uninstall result.
/// </summary>
internal record PackageUninstallApiResponse
{
    public bool Success { get; init; }
    public string? PackageId { get; init; }
    public IReadOnlyList<string> RemovedDependencies { get; init; } = [];
    public IReadOnlyList<string> AffectedWorkflows { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];

    public PackageUninstallResultModel ToModel() => new()
    {
        Success = Success,
        AffectedWorkflows = AffectedWorkflows,
        Errors = Errors
    };
}

/// <summary>
/// API response for package update info.
/// </summary>
internal record PackageUpdateInfoApiResponse
{
    public string PackageId { get; init; } = string.Empty;
    public string CurrentVersion { get; init; } = string.Empty;
    public string LatestVersion { get; init; } = string.Empty;
    public string? ReleaseNotes { get; init; }

    public PackageUpdateInfoModel ToModel() => new()
    {
        PackageId = PackageId,
        CurrentVersion = CurrentVersion,
        LatestVersion = LatestVersion,
        ReleaseNotes = ReleaseNotes
    };
}

/// <summary>
/// API request for installing a package.
/// </summary>
internal record InstallPackageApiRequest
{
    public string? Version { get; init; }
    public bool Prerelease { get; init; }
}

/// <summary>
/// API request for updating a package.
/// </summary>
internal record UpdatePackageApiRequest
{
    public string? TargetVersion { get; init; }
}

/// <summary>
/// API error response.
/// </summary>
internal record ApiErrorResponse
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Dictionary<string, string[]>? Details { get; init; }

    public IReadOnlyList<string> GetErrors()
    {
        if (Details?.TryGetValue("errors", out var errors) == true)
        {
            return errors;
        }

        return [Message];
    }

    public IReadOnlyList<string> GetAffectedWorkflows()
    {
        if (Details?.TryGetValue("affectedWorkflows", out var workflows) == true)
        {
            return workflows;
        }

        return [];
    }
}

// Package Source API Models

/// <summary>
/// API response for package source.
/// </summary>
internal record PackageSourceApiResponse
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsTrusted { get; init; }
    public bool HasCredentials { get; init; }
    public int Priority { get; init; }

    public PackageSourceModel ToModel() => new()
    {
        Name = Name,
        Url = Url,
        IsEnabled = IsEnabled,
        IsTrusted = IsTrusted,
        HasCredentials = HasCredentials,
        Priority = Priority
    };
}

/// <summary>
/// API request for adding or updating a package source.
/// </summary>
internal record PackageSourceApiRequest
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public bool IsTrusted { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? ApiKey { get; init; }
    public int Priority { get; init; }

    public static PackageSourceApiRequest FromModel(PackageSourceModel model) => new()
    {
        Name = model.Name,
        Url = model.Url,
        IsEnabled = model.IsEnabled,
        IsTrusted = model.IsTrusted,
        Username = model.Username,
        Password = model.Password,
        ApiKey = model.ApiKey,
        Priority = model.Priority
    };
}

/// <summary>
/// API response for source connectivity test.
/// </summary>
internal record SourceTestApiResponse
{
    public bool Success { get; init; }
    public string SourceName { get; init; } = string.Empty;
    public long ResponseTimeMs { get; init; }
    public string? ErrorMessage { get; init; }

    public SourceTestResultModel ToModel() => new()
    {
        Success = Success,
        SourceName = SourceName,
        ResponseTimeMs = ResponseTimeMs,
        ErrorMessage = ErrorMessage
    };
}
