using System.Net;
using System.Net.Http.Json;
using Bunit;
using CsCheck;
using FlowForge.Designer.Components;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for BrowsePackages component search results display.
/// Feature: designer-plugin-management, Property 9: Search Results Display All Matching Packages
/// </summary>
public class BrowsePackagesTests
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

    // Generator for a single search result item
    private static readonly Gen<PackageSearchItemModel> SearchItemGen = Gen.Select(
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

    /// <summary>
    /// Feature: designer-plugin-management, Property 9: Search Results Display All Matching Packages
    /// For any search query, all packages returned by the API SHALL be displayed in the Package_List,
    /// and the total count SHALL match the API response.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void SearchResults_DisplayAllMatchingPackages()
    {
        // Generate search results with 1-10 packages
        var searchResultsGen = Gen.Int[1, 10].SelectMany(count =>
            SearchItemGen.Array[count, count]
                .Select(packages => new PackageSearchResultModel
                {
                    Packages = packages.ToList(),
                    TotalCount = packages.Length,
                    Errors = []
                }));

        searchResultsGen.Sample(searchResults =>
        {
            // Create a fresh TestContext for each iteration
            using var ctx = new TestContext();

            // Create mock HTTP handler that returns the search results
            var mockHandler = new MockHttpMessageHandler(searchResults);
            var httpClient = new HttpClient(mockHandler)
            {
                BaseAddress = new Uri("http://localhost/")
            };

            // Create real services with mocked HTTP client
            var apiClient = new FlowForgeApiClient(httpClient);
            var workflowStateService = new WorkflowStateService();
            var pluginStateService = new PluginStateService(apiClient, workflowStateService);

            // Register services
            ctx.Services.AddSingleton(pluginStateService);
            ctx.Services.AddSingleton<ToastService>();

            // Set the search results directly on the service (simulating completed search)
            SetSearchResults(pluginStateService, searchResults);

            // Render the component
            var cut = ctx.RenderComponent<BrowsePackages>();

            var markup = cut.Markup;

            // Assert - Total count is displayed
            Assert.Contains($"{searchResults.TotalCount} package(s) found", markup, StringComparison.Ordinal);

            // Assert - All packages are displayed
            foreach (var package in searchResults.Packages)
            {
                // Package title should be displayed
                Assert.Contains(package.Title, markup, StringComparison.Ordinal);

                // Package version should be displayed
                Assert.Contains($"v{package.LatestVersion}", markup, StringComparison.Ordinal);

                // Description should be displayed (if not null)
                if (!string.IsNullOrEmpty(package.Description))
                {
                    Assert.Contains(package.Description, markup, StringComparison.Ordinal);
                }

                // Authors should be displayed (if not null)
                if (!string.IsNullOrEmpty(package.Authors))
                {
                    Assert.Contains(package.Authors, markup, StringComparison.Ordinal);
                }
            }

            // Assert - Number of package cards matches number of packages
            var packageCards = cut.FindAll(".package-card");
            Assert.Equal(searchResults.Packages.Count, packageCards.Count);
        }, iter: 100);
    }

    /// <summary>
    /// Sets the search results on the PluginStateService using reflection.
    /// This simulates the state after a successful search.
    /// </summary>
    private static void SetSearchResults(PluginStateService service, PackageSearchResultModel results)
    {
        var field = typeof(PluginStateService).GetField("_searchResults",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(service, results);

        // Trigger state change notification
        var notifyMethod = typeof(PluginStateService).GetMethod("NotifyStateChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        notifyMethod?.Invoke(service, null);
    }

    /// <summary>
    /// Mock HTTP message handler for testing.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly PackageSearchResultModel _searchResults;

        public MockHttpMessageHandler(PackageSearchResultModel searchResults)
        {
            _searchResults = searchResults;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            if (request.RequestUri?.PathAndQuery.Contains("search") == true)
            {
                // Return search results
                var apiResponse = new
                {
                    packages = _searchResults.Packages.Select(p => new
                    {
                        packageId = p.PackageId,
                        title = p.Title,
                        latestVersion = p.LatestVersion,
                        description = p.Description,
                        authors = p.Authors,
                        downloadCount = p.DownloadCount,
                        iconUrl = p.IconUrl,
                        tags = p.Tags,
                        isInstalled = p.IsInstalled,
                        installedVersion = p.InstalledVersion
                    }),
                    totalCount = _searchResults.TotalCount,
                    errors = _searchResults.Errors
                };
                response.Content = JsonContent.Create(apiResponse);
            }
            else if (request.RequestUri?.PathAndQuery.Contains("nodes") == true)
            {
                // Return empty node definitions
                response.Content = JsonContent.Create(Array.Empty<object>());
            }
            else
            {
                // Return empty array for other requests
                response.Content = JsonContent.Create(Array.Empty<object>());
            }

            return Task.FromResult(response);
        }
    }
}
