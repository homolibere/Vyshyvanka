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
/// Property-based tests for SourceManager component information completeness.
/// Feature: designer-plugin-management, Property 4: Source List Information Completeness
/// </summary>
public class SourceManagerTests
{
    // Generator for alphanumeric strings safe for HTML display
    private static readonly Gen<string> AlphaNumGen = Gen.Char['a', 'z'].Array[3, 20]
        .Select(chars => new string(chars));

    // Generator for URL strings
    private static readonly Gen<string> UrlGen = Gen.Select(
        Gen.OneOfConst("http", "https"),
        AlphaNumGen,
        Gen.OneOfConst(".com", ".org", ".net", ".io"),
        (scheme, domain, tld) => $"{scheme}://{domain}{tld}/v3/index.json");

    // Generator for a single package source
    private static readonly Gen<PackageSourceModel> SourceGen = Gen.Select(
        AlphaNumGen, // Name
        UrlGen, // Url
        Gen.Bool, // IsEnabled
        Gen.Bool, // IsTrusted
        Gen.Bool, // HasCredentials
        Gen.Int[0, 10], // Priority
        (name, url, isEnabled, isTrusted, hasCredentials, priority) => new PackageSourceModel
        {
            Name = name,
            Url = url,
            IsEnabled = isEnabled,
            IsTrusted = isTrusted,
            HasCredentials = hasCredentials,
            Priority = priority
        });

    /// <summary>
    /// Feature: designer-plugin-management, Property 4: Source List Information Completeness
    /// For any configured package source, the source list item SHALL display the source name, URL,
    /// enabled status, trusted status, and provide Edit, Remove, and Test Connection actions.
    /// Validates: Requirements 7.1, 7.2, 7.5
    /// </summary>
    [Fact]
    public void SourceList_DisplaysRequiredInformation()
    {
        // Generate source lists with 1-5 sources
        var sourcesGen = Gen.Int[1, 5].SelectMany(count =>
            SourceGen.Array[count, count].Select(sources => sources.ToList()));

        sourcesGen.Sample(sources =>
        {
            // Create a fresh BunitContext for each iteration
            using var ctx = new BunitContext();

            // Create mock HTTP handler
            var mockHandler = new MockHttpMessageHandler(sources);
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

            // Set the sources directly on the service (simulating loaded sources)
            SetSources(pluginStateService, sources);

            // Render the component
            var cut = ctx.Render<SourceManager>();

            var markup = cut.Markup;

            // Assert - All sources are displayed
            foreach (var source in sources)
            {
                // Source name should be displayed
                Assert.Contains(source.Name, markup, StringComparison.Ordinal);

                // Source URL should be displayed
                Assert.Contains(source.Url, markup, StringComparison.Ordinal);

                // Trusted badge should be displayed when IsTrusted is true
                if (source.IsTrusted)
                {
                    Assert.Contains("Trusted", markup, StringComparison.Ordinal);
                }

                // Disabled badge should be displayed when IsEnabled is false
                if (!source.IsEnabled)
                {
                    Assert.Contains("Disabled", markup, StringComparison.Ordinal);
                }

                // Credentials indicator should be displayed when HasCredentials is true
                if (source.HasCredentials)
                {
                    Assert.Contains("🔑", markup, StringComparison.Ordinal);
                }
            }

            // Assert - Number of source items matches number of sources
            var sourceItems = cut.FindAll(".source-item");
            Assert.Equal(sources.Count, sourceItems.Count);

            // Assert - Each source item has Test, Edit, and Remove buttons
            foreach (var sourceItem in sourceItems)
            {
                var buttons = sourceItem.QuerySelectorAll("button");
                var buttonTexts = buttons.Select(b => b.TextContent.Trim()).ToList();

                Assert.Contains("Test", buttonTexts);
                Assert.Contains("Edit", buttonTexts);
                Assert.Contains("Remove", buttonTexts);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 4: Action Buttons Disabled During Loading
    /// For any source list, when IsLoading is true, all action buttons SHALL be disabled.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void SourceManager_DisablesButtonsDuringLoading()
    {
        // Generate source lists with 1-3 sources
        var sourcesGen = Gen.Int[1, 3].SelectMany(count =>
            SourceGen.Array[count, count].Select(sources => sources.ToList()));

        sourcesGen.Sample(sources =>
        {
            // Create a fresh BunitContext for each iteration
            using var ctx = new BunitContext();

            // Create mock HTTP handler
            var mockHandler = new MockHttpMessageHandler(sources);
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

            // Set the sources and loading state
            SetSources(pluginStateService, sources);
            SetLoading(pluginStateService, true);

            // Render the component
            var cut = ctx.Render<SourceManager>();

            // Assert - All buttons should be disabled
            var buttons = cut.FindAll("button");
            Assert.All(buttons, button =>
            {
                Assert.True(button.HasAttribute("disabled"),
                    $"Button '{button.TextContent}' should be disabled when loading");
            });
        }, iter: 100);
    }

    /// <summary>
    /// Sets the sources on the PluginStateService using reflection.
    /// This simulates the state after successfully loading sources.
    /// </summary>
    private static void SetSources(PluginStateService service, List<PackageSourceModel> sources)
    {
        var field = typeof(PluginStateService).GetField("_sources",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(service, sources);

        // Trigger state change notification
        var notifyMethod = typeof(PluginStateService).GetMethod("NotifyStateChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        notifyMethod?.Invoke(service, null);
    }

    /// <summary>
    /// Sets the loading state on the PluginStateService using reflection.
    /// </summary>
    private static void SetLoading(PluginStateService service, bool isLoading)
    {
        var field = typeof(PluginStateService).GetField("_isLoading",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(service, isLoading);

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
        private readonly List<PackageSourceModel> _sources;

        public MockHttpMessageHandler(List<PackageSourceModel> sources)
        {
            _sources = sources;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            if (request.RequestUri?.PathAndQuery.Contains("sources") == true)
            {
                // Return sources
                var apiResponse = _sources.Select(s => new
                {
                    name = s.Name,
                    url = s.Url,
                    isEnabled = s.IsEnabled,
                    isTrusted = s.IsTrusted,
                    hasCredentials = s.HasCredentials,
                    priority = s.Priority
                });
                response.Content = JsonContent.Create(apiResponse);
            }
            else if (request.RequestUri?.PathAndQuery.Contains("nodes") == true)
            {
                // Return empty node definitions
                response.Content = JsonContent.Create(Array.Empty<object>());
            }
            else if (request.RequestUri?.PathAndQuery.Contains("packages/installed") == true)
            {
                // Return empty installed packages
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
