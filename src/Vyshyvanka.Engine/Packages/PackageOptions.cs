namespace Vyshyvanka.Engine.Packages;

/// <summary>
/// Configuration options for the NuGet package management subsystem.
/// </summary>
public class PackageOptions
{
    /// <summary>Directory for caching downloaded and extracted packages.</summary>
    public string CacheDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vyshyvanka", "packages");

    /// <summary>Path to the installed-packages manifest file.</summary>
    public string ManifestPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vyshyvanka", "packages", "manifest.json");

    /// <summary>Whether to require signed packages.</summary>
    public bool RequireSignedPackages { get; set; }

    /// <summary>Package IDs that are blocked from installation.</summary>
    public List<string> BlockedPackages { get; set; } = [];

    /// <summary>If non-empty, only these package IDs may be installed.</summary>
    public List<string> AllowedPackages { get; set; } = [];

    /// <summary>Whether to require confirmation before installing from untrusted sources.</summary>
    public bool RequireUntrustedSourceConfirmation { get; set; }

    /// <summary>
    /// Callback invoked when a package is about to be installed from an untrusted source.
    /// Returns true to proceed, false to abort.
    /// </summary>
    public Func<string, string, Task<bool>>? UntrustedSourceConfirmationCallback { get; set; }
}
