using Bunit;
using CsCheck;
using FlowForge.Designer.Components;
using FlowForge.Designer.Models;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for PackageCard component information completeness.
/// Feature: designer-plugin-management, Property 1 &amp; 2: Package Card Information Completeness
/// </summary>
public class PackageCardTests
{
    // Generator for alphanumeric strings safe for HTML display
    private static readonly Gen<string> AlphaNumGen = Gen.Char['a', 'z'].Array[3, 20]
        .Select(chars => new string(chars));

    // Generator for version strings (e.g., "1.2.3")
    private static readonly Gen<string> VersionGen = Gen.Select(
        Gen.Int[1, 99],
        Gen.Int[0, 99],
        Gen.Int[0, 99],
        (major, minor, patch) => $"{major}.{minor}.{patch}");

    /// <summary>
    /// Feature: designer-plugin-management, Property 1: Installed Package Card Information Completeness
    /// For any installed package displayed in the Plugin Manager, the Package_Card SHALL display 
    /// the package name, version, installation date, node count, and appropriate action buttons 
    /// (Update if available, Uninstall always).
    /// Validates: Requirements 2.2, 2.3, 2.4
    /// </summary>
    [Fact]
    public void InstalledPackageCard_DisplaysRequiredInformation()
    {
        var installedPackageGen = Gen.Select(
            AlphaNumGen, // PackageId
            VersionGen, // Version
            Gen.Int[0, 20], // NodeCount
            Gen.Bool, // HasUpdate
            VersionGen, // LatestVersion
            (packageId, version, nodeCount, hasUpdate, latestVersion) => new InstalledPackageModel
            {
                PackageId = packageId,
                Version = version,
                SourceName = "nuget.org",
                InstalledAt = DateTime.UtcNow,
                NodeTypes = Enumerable.Range(0, nodeCount).Select(i => $"Node{i}").ToList(),
                IsLoaded = true,
                HasUpdate = hasUpdate,
                LatestVersion = hasUpdate ? latestVersion : null
            });

        installedPackageGen.Sample(package =>
        {
            // Create a fresh TestContext for each iteration
            using var ctx = new TestContext();

            // Arrange & Act
            var cut = ctx.Render<PackageCard>(parameters => parameters
                .Add(p => p.PackageId, package.PackageId)
                .Add(p => p.Title, package.PackageId)
                .Add(p => p.Version, package.Version)
                .Add(p => p.NodeCount, package.NodeTypes.Count)
                .Add(p => p.IsInstalled, true)
                .Add(p => p.HasUpdate, package.HasUpdate)
                .Add(p => p.LatestVersion, package.LatestVersion)
                .Add(p => p.IsLoading, false));

            var markup = cut.Markup;

            // Assert - Package name is displayed
            Assert.Contains(package.PackageId, markup);

            // Assert - Version is displayed
            Assert.Contains($"v{package.Version}", markup);

            // Assert - Node count is displayed when > 0
            if (package.NodeTypes.Count > 0)
            {
                Assert.Contains($"{package.NodeTypes.Count} nodes", markup);
            }

            // Assert - Uninstall button is always present for installed packages
            Assert.Contains("Uninstall", markup);

            // Assert - Update button is present when HasUpdate is true
            if (package.HasUpdate)
            {
                Assert.Contains("Update", markup);
                // Update indicator should be visible
                Assert.Contains("⬆", markup);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 2: Search Result Card Information Completeness
    /// For any package in search results, the Package_Card SHALL display the package name, 
    /// latest version, description, author, download count, and either an "Install" button 
    /// (if not installed) or the installed version indicator (if installed).
    /// Validates: Requirements 3.3, 3.4, 3.5
    /// </summary>
    [Fact]
    public void SearchResultCard_DisplaysRequiredInformation()
    {
        var searchResultGen = Gen.Select(
            AlphaNumGen, // PackageId
            AlphaNumGen, // Title
            VersionGen, // LatestVersion
            AlphaNumGen, // Description
            AlphaNumGen, // Authors
            Gen.Long[0, 10_000_000], // DownloadCount
            Gen.Bool, // IsInstalled
            VersionGen, // InstalledVersion
            (packageId, title, latestVersion, description, authors, downloadCount, isInstalled, installedVersion) =>
                new PackageSearchItemModel
                {
                    PackageId = packageId,
                    Title = title,
                    LatestVersion = latestVersion,
                    Description = description,
                    Authors = authors,
                    DownloadCount = downloadCount,
                    IsInstalled = isInstalled,
                    InstalledVersion = isInstalled ? installedVersion : null
                });

        searchResultGen.Sample(package =>
        {
            // Create a fresh TestContext for each iteration
            using var ctx = new TestContext();

            // Arrange & Act
            var cut = ctx.Render<PackageCard>(parameters => parameters
                .Add(p => p.PackageId, package.PackageId)
                .Add(p => p.Title, package.Title)
                .Add(p => p.Version, package.LatestVersion)
                .Add(p => p.Description, package.Description)
                .Add(p => p.Authors, package.Authors)
                .Add(p => p.DownloadCount, package.DownloadCount)
                .Add(p => p.IsInstalled, package.IsInstalled)
                .Add(p => p.InstalledVersion, package.InstalledVersion)
                .Add(p => p.IsLoading, false));

            var markup = cut.Markup;

            // Assert - Package title is displayed
            Assert.Contains(package.Title, markup);

            // Assert - Version is displayed
            Assert.Contains($"v{package.LatestVersion}", markup);

            // Assert - Description is displayed (if not null)
            if (!string.IsNullOrEmpty(package.Description))
            {
                Assert.Contains(package.Description, markup);
            }

            // Assert - Authors are displayed (if not null)
            if (!string.IsNullOrEmpty(package.Authors))
            {
                Assert.Contains(package.Authors, markup);
            }

            // Assert - Download count is displayed (formatted)
            if (package.DownloadCount > 0)
            {
                var formattedCount = FormatDownloads(package.DownloadCount);
                Assert.Contains(formattedCount, markup);
            }

            // Assert - Install button for non-installed packages
            if (!package.IsInstalled)
            {
                Assert.Contains("Install", markup);
                Assert.DoesNotContain("Uninstall", markup);
            }
            else
            {
                // Assert - Installed version indicator for installed packages
                Assert.Contains($"Installed v{package.InstalledVersion}", markup);
                Assert.Contains("Uninstall", markup);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 1 &amp; 2: Action Buttons Disabled During Loading
    /// For any package card, when IsLoading is true, all action buttons SHALL be disabled.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void PackageCard_DisablesButtonsDuringLoading()
    {
        var packageGen = Gen.Select(
            AlphaNumGen,
            VersionGen,
            Gen.Bool,
            Gen.Bool,
            (packageId, version, isInstalled, hasUpdate) => (packageId, version, isInstalled, hasUpdate));

        packageGen.Sample(data =>
        {
            // Create a fresh TestContext for each iteration
            using var ctx = new TestContext();

            // Arrange & Act - Render with IsLoading = true
            var cut = ctx.Render<PackageCard>(parameters => parameters
                .Add(p => p.PackageId, data.packageId)
                .Add(p => p.Version, data.version)
                .Add(p => p.IsInstalled, data.isInstalled)
                .Add(p => p.HasUpdate, data.hasUpdate)
                .Add(p => p.IsLoading, true));

            // Assert - All buttons should be disabled
            var buttons = cut.FindAll("button");
            Assert.All(buttons, button =>
            {
                Assert.True(button.HasAttribute("disabled"),
                    $"Button '{button.TextContent}' should be disabled when loading");
            });
        }, iter: 100);
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
