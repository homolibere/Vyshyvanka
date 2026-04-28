using Bunit;
using CsCheck;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for untrusted source confirmation during package installation.
/// Feature: designer-plugin-management, Property 8: Untrusted Source Confirmation
/// </summary>
public class UntrustedSourceConfirmationTests
{
    // Generator for alphanumeric strings safe for HTML display
    private static readonly Gen<string> AlphaNumGen = Gen.Char['a', 'z'].Array[3, 20]
        .Select(chars => new string(chars));

    // Generator for package IDs
    private static readonly Gen<string> PackageIdGen = Gen.Select(
        AlphaNumGen,
        AlphaNumGen,
        (prefix, suffix) => $"{prefix}.{suffix}");

    // Generator for source names
    private static readonly Gen<string> SourceNameGen = Gen.Select(
        AlphaNumGen,
        Gen.Int[1, 100],
        (name, id) => $"{name}-source-{id}");

    // Generator for untrusted source models (1-5 sources, all untrusted)
    private static readonly Gen<PackageSourceModel[]> UntrustedSourcesGen = Gen.Select(
        SourceNameGen,
        AlphaNumGen,
        (name, urlPart) => new PackageSourceModel
        {
            Name = name,
            Url = $"https://{urlPart}.example.com/nuget",
            IsEnabled = true,
            IsTrusted = false
        }).Array[1, 5];

    // Generator for mixed sources (some trusted, some untrusted)
    private static readonly Gen<PackageSourceModel[]> MixedSourcesGen = Gen.Select(
        SourceNameGen.Array[1, 3],
        SourceNameGen.Array[1, 3],
        (trustedNames, untrustedNames) =>
        {
            var trusted = trustedNames.Select(name => new PackageSourceModel
            {
                Name = name,
                Url = $"https://{name}.trusted.com/nuget",
                IsEnabled = true,
                IsTrusted = true
            });
            var untrusted = untrustedNames.Select(name => new PackageSourceModel
            {
                Name = name,
                Url = $"https://{name}.untrusted.com/nuget",
                IsEnabled = true,
                IsTrusted = false
            });
            return trusted.Concat(untrusted).ToArray();
        });

    // Generator for all trusted sources
    private static readonly Gen<PackageSourceModel[]> TrustedSourcesGen = Gen.Select(
        SourceNameGen,
        AlphaNumGen,
        (name, urlPart) => new PackageSourceModel
        {
            Name = name,
            Url = $"https://{urlPart}.trusted.com/nuget",
            IsEnabled = true,
            IsTrusted = true
        }).Array[1, 5];

    /// <summary>
    /// Feature: designer-plugin-management, Property 8: Untrusted Source Confirmation
    /// For any package installation from an untrusted source, the Plugin_Manager_UI SHALL display
    /// a confirmation dialog before proceeding with the installation.
    /// Validates: Requirements 4.6
    /// </summary>
    [Fact]
    public void ConfirmDialog_ForUntrustedSource_DisplaysWarningWithSourceNames()
    {
        UntrustedSourcesGen.Sample(untrustedSources =>
        {
            using var ctx = new BunitContext();

            var untrustedNames = untrustedSources.Select(s => s.Name).ToList();

            // Arrange & Act - Render ConfirmDialog simulating untrusted source warning
            var cut = ctx.Render<ConfirmDialog>(parameters => parameters
                .Add(p => p.IsOpen, true)
                .Add(p => p.Title, "Install from Untrusted Source")
                .Add(p => p.Message,
                    "This package may be installed from an untrusted source. Packages from untrusted sources could potentially contain malicious code.")
                .Add(p => p.Icon, "⚠️")
                .Add(p => p.Variant, ConfirmDialogVariant.Warning)
                .Add(p => p.AffectedItems, untrustedNames)
                .Add(p => p.AffectedItemsLabel, "Untrusted sources:")
                .Add(p => p.ConfirmText, "Install Anyway")
                .Add(p => p.CancelText, "Cancel"));

            var markup = cut.Markup;

            // Assert - Dialog is visible
            Assert.Contains("open", markup, StringComparison.Ordinal);

            // Assert - Warning title is displayed
            Assert.Contains("Install from Untrusted Source", markup, StringComparison.Ordinal);

            // Assert - Warning message is displayed
            Assert.Contains("untrusted source", markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("malicious code", markup, StringComparison.OrdinalIgnoreCase);

            // Assert - Warning icon is displayed
            Assert.Contains("⚠️", markup, StringComparison.Ordinal);

            // Assert - All untrusted source names are displayed
            foreach (var sourceName in untrustedNames)
            {
                Assert.Contains(sourceName, markup, StringComparison.Ordinal);
            }

            // Assert - Affected items label is displayed
            Assert.Contains("Untrusted sources:", markup, StringComparison.Ordinal);

            // Assert - Install Anyway and Cancel buttons are displayed
            Assert.Contains("Install Anyway", markup, StringComparison.Ordinal);
            Assert.Contains("Cancel", markup, StringComparison.Ordinal);

            // Assert - Warning variant styling is applied
            Assert.Contains("header-warning", markup, StringComparison.Ordinal);
            Assert.Contains("btn-warning", markup, StringComparison.Ordinal);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 8: Untrusted Source Confirmation
    /// For any set of package sources containing at least one untrusted source,
    /// the HasUntrustedSources property SHALL return true.
    /// Validates: Requirements 4.6
    /// </summary>
    [Fact]
    public void PluginStateService_WithUntrustedSources_HasUntrustedSourcesReturnsTrue()
    {
        MixedSourcesGen.Sample(sources =>
        {
            // Arrange - Create service with real HttpClient (won't be used)
            var httpClient = new HttpClient { BaseAddress = new Uri("http://test") };
            var mockWorkflowState = new WorkflowStateService();

            var service = new PluginStateService(new VyshyvankaApiClient(httpClient), mockWorkflowState);

            // Use reflection to set the private _sources field
            var sourcesField = typeof(PluginStateService).GetField("_sources",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sourcesField?.SetValue(service, sources.ToList());

            // Act
            var hasUntrusted = service.HasUntrustedSources;
            var untrustedNames = service.UntrustedSourceNames;

            // Assert - Should have untrusted sources since MixedSourcesGen always includes some
            var expectedUntrusted = sources.Where(s => s.IsEnabled && !s.IsTrusted).ToList();
            Assert.Equal(expectedUntrusted.Count > 0, hasUntrusted);

            // Assert - Untrusted source names should match
            var expectedNames = expectedUntrusted.Select(s => s.Name).ToList();
            Assert.Equal(expectedNames.Count, untrustedNames.Count);
            foreach (var name in expectedNames)
            {
                Assert.Contains(name, untrustedNames);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 8: Untrusted Source Confirmation
    /// For any set of package sources where all sources are trusted,
    /// the HasUntrustedSources property SHALL return false.
    /// Validates: Requirements 4.6
    /// </summary>
    [Fact]
    public void PluginStateService_WithAllTrustedSources_HasUntrustedSourcesReturnsFalse()
    {
        TrustedSourcesGen.Sample(sources =>
        {
            // Arrange - Create service with real HttpClient (won't be used)
            var httpClient = new HttpClient { BaseAddress = new Uri("http://test") };
            var mockWorkflowState = new WorkflowStateService();

            var service = new PluginStateService(new VyshyvankaApiClient(httpClient), mockWorkflowState);

            // Use reflection to set the private _sources field
            var sourcesField = typeof(PluginStateService).GetField("_sources",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sourcesField?.SetValue(service, sources.ToList());

            // Act
            var hasUntrusted = service.HasUntrustedSources;
            var untrustedNames = service.UntrustedSourceNames;

            // Assert - Should not have untrusted sources
            Assert.False(hasUntrusted);
            Assert.Empty(untrustedNames);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 8: Untrusted Source Confirmation
    /// For any confirmation dialog during installation processing,
    /// the dialog SHALL show processing state and disable buttons.
    /// Validates: Requirements 4.2, 10.4, 10.5
    /// </summary>
    [Fact]
    public void ConfirmDialog_DuringInstallation_ShowsProcessingState()
    {
        PackageIdGen.Sample(packageId =>
        {
            using var ctx = new BunitContext();

            // Arrange & Act - Render ConfirmDialog in processing state
            var cut = ctx.Render<ConfirmDialog>(parameters => parameters
                .Add(p => p.IsOpen, true)
                .Add(p => p.Title, "Install from Untrusted Source")
                .Add(p => p.Message, $"Installing {packageId}...")
                .Add(p => p.Variant, ConfirmDialogVariant.Warning)
                .Add(p => p.IsProcessing, true)
                .Add(p => p.ProcessingText, "Installing...")
                .Add(p => p.ConfirmText, "Install Anyway")
                .Add(p => p.CancelText, "Cancel"));

            var markup = cut.Markup;

            // Assert - Processing indicator is shown
            Assert.Contains("Installing...", markup, StringComparison.Ordinal);
            Assert.Contains("processing-indicator", markup, StringComparison.Ordinal);

            // Assert - Buttons are disabled
            var disabledCount = markup.Split("disabled").Length - 1;
            Assert.True(disabledCount >= 2, "Both buttons should be disabled during processing");
        }, iter: 100);
    }
}
