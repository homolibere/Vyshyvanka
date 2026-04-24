using Bunit;
using FlowForge.Designer.Components;
using Xunit.Abstractions;

namespace FlowForge.Tests.Unit;

/// <summary>
/// Unit tests for PackageCard component.
/// </summary>
public class PackageCardUnitTests : TestContext
{
    private readonly ITestOutputHelper _output;

    public PackageCardUnitTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PackageCard_RendersAllRequiredInformation()
    {
        // Arrange & Act
        var cut = Render<PackageCard>(parameters => parameters
            .Add(p => p.PackageId, "test-package")
            .Add(p => p.Title, "Test Package Title")
            .Add(p => p.Version, "1.2.3")
            .Add(p => p.Description, "This is a test description")
            .Add(p => p.Authors, "Test Author")
            .Add(p => p.DownloadCount, 1500)
            .Add(p => p.NodeCount, 5)
            .Add(p => p.IsInstalled, true)
            .Add(p => p.InstalledVersion, "1.0.0")
            .Add(p => p.HasUpdate, true)
            .Add(p => p.LatestVersion, "2.0.0")
            .Add(p => p.IsLoading, false));

        // Debug output
        var markup = cut.Markup;
        _output.WriteLine("=== FULL MARKUP ===");
        _output.WriteLine(markup);
        _output.WriteLine("=== END MARKUP ===");

        // Assert - Title is displayed
        Assert.Contains("Test Package Title", markup);

        // Assert - Version is displayed
        Assert.Contains("v1.2.3", markup);

        // Assert - Description is displayed
        Assert.Contains("This is a test description", markup);

        // Assert - Authors are displayed
        Assert.Contains("Test Author", markup);

        // Assert - Download count is displayed (formatted as 1.5K)
        Assert.Contains("1.5K", markup);

        // Assert - Node count is displayed
        Assert.Contains("5 nodes", markup);

        // Assert - Installed version is displayed
        Assert.Contains("Installed v1.0.0", markup);

        // Assert - Update button is present
        Assert.Contains("Update", markup);

        // Assert - Uninstall button is present
        Assert.Contains("Uninstall", markup);

        // Assert - Update indicator is present
        Assert.Contains("⬆", markup);
    }
}
