using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class PackageCard
{
    /// <summary>The NuGet package identifier.</summary>
    [Parameter, EditorRequired]
    public string PackageId { get; set; } = string.Empty;

    /// <summary>The display title of the package.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>The package version to display.</summary>
    [Parameter, EditorRequired]
    public string Version { get; set; } = string.Empty;

    /// <summary>Package description.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Package authors.</summary>
    [Parameter]
    public string? Authors { get; set; }

    /// <summary>URL to the package icon.</summary>
    [Parameter]
    public string? IconUrl { get; set; }

    /// <summary>Total download count.</summary>
    [Parameter]
    public long DownloadCount { get; set; }

    /// <summary>Number of node types provided by this package.</summary>
    [Parameter]
    public int NodeCount { get; set; }

    /// <summary>Whether this package is installed.</summary>
    [Parameter]
    public bool IsInstalled { get; set; }

    /// <summary>The installed version, if the package is installed.</summary>
    [Parameter]
    public string? InstalledVersion { get; set; }

    /// <summary>Whether an update is available.</summary>
    [Parameter]
    public bool HasUpdate { get; set; }

    /// <summary>The latest available version, if an update exists.</summary>
    [Parameter]
    public string? LatestVersion { get; set; }

    /// <summary>Whether an operation is in progress.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Whether this specific package is being installed.</summary>
    [Parameter]
    public bool IsInstalling { get; set; }

    /// <summary>Whether this specific package is being updated.</summary>
    [Parameter]
    public bool IsUpdating { get; set; }

    /// <summary>Whether this specific package is being uninstalled.</summary>
    [Parameter]
    public bool IsUninstalling { get; set; }

    /// <summary>Whether any operation is in progress for this package.</summary>
    private bool IsOperationInProgress => IsInstalling || IsUpdating || IsUninstalling;

    /// <summary>Callback when the card is clicked to show details.</summary>
    [Parameter]
    public EventCallback OnClick { get; set; }

    /// <summary>Callback when the Install button is clicked.</summary>
    [Parameter]
    public EventCallback OnInstall { get; set; }

    /// <summary>Callback when the Update button is clicked.</summary>
    [Parameter]
    public EventCallback OnUpdate { get; set; }

    /// <summary>Callback when the Uninstall button is clicked.</summary>
    [Parameter]
    public EventCallback OnUninstall { get; set; }

    private async Task HandleClick()
    {
        await OnClick.InvokeAsync();
    }

    private async Task HandleInstall()
    {
        await OnInstall.InvokeAsync();
    }

    private async Task HandleUpdate()
    {
        await OnUpdate.InvokeAsync();
    }

    private async Task HandleUninstall()
    {
        await OnUninstall.InvokeAsync();
    }

    private static string FormatDownloads(long count)
    {
        return count switch
        {
            >= 1_000_000_000 => (count / 1_000_000_000.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "B",
            >= 1_000_000 => (count / 1_000_000.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "M",
            >= 1_000 => (count / 1_000.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "K",
            _ => count.ToString()
        };
    }
}
 /// 