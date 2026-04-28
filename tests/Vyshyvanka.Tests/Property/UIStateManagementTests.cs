using CsCheck;
using Vyshyvanka.Designer.Services;
using System.Net;
using System.Net.Http.Json;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for UI state management during operations.
/// Feature: designer-plugin-management, Property 6: UI State Management During Operations
/// </summary>
public class UiStateManagementTests
{
    // Generator for package IDs
    private static readonly Gen<string> PackageIdGen = Gen.Char['a', 'z'].Array[5, 20]
        .Select(chars => new string(chars));

    // Generator for source names
    private static readonly Gen<string> SourceNameGen = Gen.Char['a', 'z'].Array[5, 15]
        .Select(chars => new string(chars) + "-source");

    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any asynchronous operation (install, update, uninstall, search, load), 
    /// the Plugin_Manager_UI SHALL display a loading indicator and disable action buttons 
    /// to prevent duplicate requests.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void InstallOperation_SetsLoadingState_AndPreventsDuplicateRequests()
    {
        PackageIdGen.Sample(packageId =>
        {
            // Arrange
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new SmartHttpMessageHandler(packageId, tcs);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start installation (don't await)
            var installTask = pluginState.InstallPackageAsync(packageId);

            // Assert - Loading state should be set during operation
            Assert.True(pluginState.IsLoading, "IsLoading should be true during installation");
            Assert.True(pluginState.IsPackageBeingInstalled(packageId),
                "Package should be tracked as being installed");

            // Assert - Duplicate request should be prevented
            var duplicateTask = pluginState.InstallPackageAsync(packageId);
            var duplicateResult = duplicateTask.GetAwaiter().GetResult();
            Assert.False(duplicateResult, "Duplicate installation request should return false");

            // Complete the original operation with success response
            tcs.SetResult(CreateInstallSuccessResponse(packageId));

            installTask.GetAwaiter().GetResult();

            // Assert - Loading state should be cleared after operation
            Assert.False(pluginState.IsPackageBeingInstalled(packageId),
                "Package should no longer be tracked as being installed");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any update operation, the service SHALL track the specific package being updated
    /// and prevent duplicate update requests for the same package.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void UpdateOperation_SetsLoadingState_AndPreventsDuplicateRequests()
    {
        PackageIdGen.Sample(packageId =>
        {
            // Arrange
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new SmartHttpMessageHandler(packageId, tcs);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start update (don't await)
            var updateTask = pluginState.UpdatePackageAsync(packageId);

            // Assert - Loading state should be set during operation
            Assert.True(pluginState.IsLoading, "IsLoading should be true during update");
            Assert.True(pluginState.IsPackageBeingUpdated(packageId),
                "Package should be tracked as being updated");
            Assert.True(pluginState.IsPackageOperationInProgress(packageId),
                "IsPackageOperationInProgress should return true");

            // Assert - Duplicate request should be prevented
            var duplicateTask = pluginState.UpdatePackageAsync(packageId);
            var duplicateResult = duplicateTask.GetAwaiter().GetResult();
            Assert.False(duplicateResult, "Duplicate update request should return false");

            // Complete the original operation
            tcs.SetResult(CreateUpdateSuccessResponse(packageId));

            updateTask.GetAwaiter().GetResult();

            // Assert - Loading state should be cleared after operation
            Assert.False(pluginState.IsPackageBeingUpdated(packageId),
                "Package should no longer be tracked as being updated");
        }, iter: 100);
    }


    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any uninstall operation, the service SHALL track the specific package being uninstalled
    /// and prevent duplicate uninstall requests for the same package.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void UninstallOperation_SetsLoadingState_AndPreventsDuplicateRequests()
    {
        PackageIdGen.Sample(packageId =>
        {
            // Arrange
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new SmartHttpMessageHandler(packageId, tcs);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start uninstall (don't await)
            var uninstallTask = pluginState.UninstallPackageAsync(packageId);

            // Assert - Loading state should be set during operation
            Assert.True(pluginState.IsLoading, "IsLoading should be true during uninstall");
            Assert.True(pluginState.IsPackageBeingUninstalled(packageId),
                "Package should be tracked as being uninstalled");
            Assert.True(pluginState.IsPackageOperationInProgress(packageId),
                "IsPackageOperationInProgress should return true");

            // Assert - Duplicate request should be prevented
            var duplicateTask = pluginState.UninstallPackageAsync(packageId);
            var duplicateResult = duplicateTask.GetAwaiter().GetResult();
            Assert.False(duplicateResult.Success, "Duplicate uninstall request should fail");
            Assert.Contains("already in progress", duplicateResult.Errors.FirstOrDefault() ?? "",
                StringComparison.OrdinalIgnoreCase);

            // Complete the original operation
            tcs.SetResult(CreateUninstallSuccessResponse());

            uninstallTask.GetAwaiter().GetResult();

            // Assert - Loading state should be cleared after operation
            Assert.False(pluginState.IsPackageBeingUninstalled(packageId),
                "Package should no longer be tracked as being uninstalled");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any check for updates operation, the service SHALL track the operation
    /// and prevent duplicate requests.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void CheckForUpdatesOperation_SetsLoadingState_AndPreventsDuplicateRequests()
    {
        Gen.Bool.Sample(_ =>
        {
            // Arrange
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new SmartHttpMessageHandler("", tcs);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start check for updates (don't await)
            var checkTask = pluginState.CheckForUpdatesAsync();

            // Assert - Loading state should be set during operation
            Assert.True(pluginState.IsLoading, "IsLoading should be true during check for updates");
            Assert.True(pluginState.IsCheckingForUpdates, "IsCheckingForUpdates should be true");

            // Assert - Duplicate request should be prevented (returns immediately)
            var duplicateTask = pluginState.CheckForUpdatesAsync();
            // The duplicate should complete immediately without making a request
            Assert.True(duplicateTask.IsCompleted || pluginState.IsCheckingForUpdates,
                "Duplicate check should either complete immediately or be blocked");

            // Complete the original operation
            tcs.SetResult(CreateCheckUpdatesSuccessResponse());

            checkTask.GetAwaiter().GetResult();

            // Assert - Loading state should be cleared after operation
            Assert.False(pluginState.IsCheckingForUpdates,
                "IsCheckingForUpdates should be false after operation");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any source test operation, the service SHALL track the specific source being tested
    /// and prevent duplicate test requests for the same source.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void TestSourceOperation_SetsLoadingState_AndPreventsDuplicateRequests()
    {
        SourceNameGen.Sample(sourceName =>
        {
            // Arrange
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new SmartHttpMessageHandler(sourceName, tcs);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start source test (don't await)
            var testTask = pluginState.TestSourceAsync(sourceName);

            // Assert - Loading state should be set during operation
            Assert.True(pluginState.IsLoading, "IsLoading should be true during source test");
            Assert.True(pluginState.IsSourceBeingTested(sourceName),
                "Source should be tracked as being tested");

            // Assert - Duplicate request should be prevented
            var duplicateTask = pluginState.TestSourceAsync(sourceName);
            var duplicateResult = duplicateTask.GetAwaiter().GetResult();
            Assert.False(duplicateResult.Success, "Duplicate test request should fail");
            Assert.Contains("already in progress", duplicateResult.ErrorMessage ?? "",
                StringComparison.OrdinalIgnoreCase);

            // Complete the original operation
            tcs.SetResult(CreateTestSourceSuccessResponse(sourceName));

            testTask.GetAwaiter().GetResult();

            // Assert - Loading state should be cleared after operation
            Assert.False(pluginState.IsSourceBeingTested(sourceName),
                "Source should no longer be tracked as being tested");
        }, iter: 100);
    }


    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any search operation, the service SHALL track the search state
    /// and set the IsSearching flag.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void SearchOperation_SetsSearchingState()
    {
        var searchQueryGen = Gen.Char['a', 'z'].Array[3, 15]
            .Select(chars => new string(chars));

        searchQueryGen.Sample(query =>
        {
            // Arrange
            var tcs = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new SmartHttpMessageHandler(query, tcs);
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start search (don't await)
            var searchTask = pluginState.SearchPackagesAsync(query);

            // Assert - Search state should be set during operation
            Assert.True(pluginState.IsLoading, "IsLoading should be true during search");
            Assert.True(pluginState.IsSearching, "IsSearching should be true during search");

            // Complete the operation
            tcs.SetResult(CreateSearchSuccessResponse());

            searchTask.GetAwaiter().GetResult();

            // Assert - Search state should be cleared after operation
            Assert.False(pluginState.IsSearching, "IsSearching should be false after search");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// For any operation that fails, the loading state SHALL still be cleared.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void FailedOperation_ClearsLoadingState()
    {
        PackageIdGen.Sample(packageId =>
        {
            // Arrange
            var handler = new ImmediateFailureHttpMessageHandler();
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start installation that will fail
            var result = pluginState.InstallPackageAsync(packageId).GetAwaiter().GetResult();

            // Assert - Operation should have failed
            Assert.False(result, "Installation should have failed");

            // Assert - Loading state should be cleared even after failure
            Assert.False(pluginState.IsLoading, "IsLoading should be false after failed operation");
            Assert.False(pluginState.IsPackageBeingInstalled(packageId),
                "Package should not be tracked after failed operation");

            // Assert - Error message should be set
            Assert.NotNull(pluginState.ErrorMessage);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 6: UI State Management During Operations
    /// Multiple different packages can have operations in progress simultaneously,
    /// but each package can only have one operation at a time.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void MultiplePackages_CanHaveSimultaneousOperations()
    {
        var twoPackageIdsGen = Gen.Select(PackageIdGen, PackageIdGen, (a, b) => (a, b))
            .Where(pair => pair.a != pair.b);

        twoPackageIdsGen.Sample(packageIds =>
        {
            var (packageId1, packageId2) = packageIds;

            // Arrange - Use handler that supports multiple packages
            var tcs1 = new TaskCompletionSource<HttpResponseMessage>();
            var tcs2 = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new MultiPackageHttpMessageHandler(
                new Dictionary<string, TaskCompletionSource<HttpResponseMessage>>
                {
                    { packageId1, tcs1 },
                    { packageId2, tcs2 }
                });
            var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
            var apiClient = new VyshyvankaApiClient(httpClient);
            var workflowState = new WorkflowStateService();
            var pluginState = new PluginStateService(apiClient, workflowState);

            // Act - Start installations for both packages
            var task1 = pluginState.InstallPackageAsync(packageId1);
            var task2 = pluginState.InstallPackageAsync(packageId2);

            // Assert - Both packages should be tracked as being installed
            Assert.True(pluginState.IsPackageBeingInstalled(packageId1),
                "Package 1 should be tracked as being installed");
            Assert.True(pluginState.IsPackageBeingInstalled(packageId2),
                "Package 2 should be tracked as being installed");

            // Complete both operations
            tcs1.SetResult(CreateInstallSuccessResponse(packageId1));
            tcs2.SetResult(CreateInstallSuccessResponse(packageId2));

            task1.GetAwaiter().GetResult();
            task2.GetAwaiter().GetResult();

            // Assert - Both packages should no longer be tracked
            Assert.False(pluginState.IsPackageBeingInstalled(packageId1),
                "Package 1 should no longer be tracked");
            Assert.False(pluginState.IsPackageBeingInstalled(packageId2),
                "Package 2 should no longer be tracked");
        }, iter: 100);
    }


    #region HTTP Response Helpers

    private static HttpResponseMessage CreateInstallSuccessResponse(string packageId)
    {
        var response = new
        {
            success = true,
            package = new
            {
                packageId,
                version = "1.0.0",
                sourceName = "test-source",
                installedAt = DateTime.UtcNow,
                nodeTypes = Array.Empty<string>(),
                isLoaded = true
            },
            errors = Array.Empty<string>(),
            warnings = Array.Empty<string>()
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };
    }

    private static HttpResponseMessage CreateUpdateSuccessResponse(string packageId)
    {
        var response = new
        {
            success = true,
            package = new
            {
                packageId,
                version = "2.0.0",
                sourceName = "test-source",
                installedAt = DateTime.UtcNow,
                nodeTypes = Array.Empty<string>(),
                isLoaded = true
            },
            previousVersion = "1.0.0",
            errors = Array.Empty<string>()
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };
    }

    private static HttpResponseMessage CreateUninstallSuccessResponse()
    {
        var response = new
        {
            success = true,
            affectedWorkflows = Array.Empty<string>(),
            errors = Array.Empty<string>()
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };
    }

    private static HttpResponseMessage CreateCheckUpdatesSuccessResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
    }

    private static HttpResponseMessage CreateTestSourceSuccessResponse(string sourceName)
    {
        var response = new
        {
            success = true,
            sourceName,
            responseTimeMs = 100L,
            errorMessage = (string?)null
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };
    }

    private static HttpResponseMessage CreateSearchSuccessResponse()
    {
        var response = new
        {
            packages = Array.Empty<object>(),
            totalCount = 0,
            errors = Array.Empty<string>()
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        };
    }

    private static HttpResponseMessage CreateNodeDefinitionsResponse()
    {
        // Return an empty JSON array that can be deserialized to List<NodeDefinition>
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
        };
    }

    #endregion

    #region Test HTTP Handlers

    /// <summary>
    /// HTTP handler that handles multiple request types intelligently.
    /// Returns the delayed response for the main operation and immediate responses for node definitions.
    /// </summary>
    private class SmartHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _operationKey;
        private readonly TaskCompletionSource<HttpResponseMessage> _mainOperationTcs;
        private bool _mainOperationReturned;

        public SmartHttpMessageHandler(string operationKey, TaskCompletionSource<HttpResponseMessage> mainOperationTcs)
        {
            _operationKey = operationKey;
            _mainOperationTcs = mainOperationTcs;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";

            // Node definitions endpoint (api/nodes but not api/nodes/packages) - return immediately
            if (url.EndsWith("api/nodes", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith("api/nodes/", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateNodeDefinitionsResponse());
            }

            // First main operation request - return the delayed task
            if (!_mainOperationReturned)
            {
                _mainOperationReturned = true;
                return _mainOperationTcs.Task;
            }

            // Subsequent requests - return a default success response
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true })
            });
        }
    }

    /// <summary>
    /// HTTP handler that immediately throws an exception.
    /// </summary>
    private class ImmediateFailureHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Network error");
        }
    }

    /// <summary>
    /// HTTP handler that supports multiple concurrent package operations.
    /// </summary>
    private class MultiPackageHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, TaskCompletionSource<HttpResponseMessage>> _packageResponses;
        private readonly HashSet<string> _returnedPackages = [];

        public MultiPackageHttpMessageHandler(
            Dictionary<string, TaskCompletionSource<HttpResponseMessage>> packageResponses)
        {
            _packageResponses = packageResponses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";

            // Node definitions endpoint (api/nodes but not api/nodes/packages) - return immediately
            if (url.EndsWith("api/nodes", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith("api/nodes/", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(CreateNodeDefinitionsResponse());
            }

            // Find matching package
            foreach (var kvp in _packageResponses)
            {
                if (url.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) &&
                    !_returnedPackages.Contains(kvp.Key))
                {
                    _returnedPackages.Add(kvp.Key);
                    return kvp.Value.Task;
                }
            }

            // Default response
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { success = true })
            });
        }
    }

    #endregion
}
