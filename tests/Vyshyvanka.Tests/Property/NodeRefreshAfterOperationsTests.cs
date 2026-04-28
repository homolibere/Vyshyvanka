using CsCheck;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for node refresh after package operations.
/// Feature: designer-plugin-management, Property 3: Node Refresh After Package Operations
/// </summary>
public class NodeRefreshAfterOperationsTests
{
    /// <summary>
    /// Feature: designer-plugin-management, Property 3: Node Refresh After Package Operations
    /// For any successful package installation, the Designer SHALL trigger a node definitions refresh,
    /// and the Node Palette SHALL reflect the changes without requiring a page refresh.
    /// Validates: Requirements 4.4, 5.4, 6.6, 9.1, 9.2, 9.3
    /// </summary>
    [Fact]
    public void NodeRefreshAfterInstallation_SuccessfulInstall_RefreshesNodeDefinitions()
    {
        GenInstallScenario.Sample(scenario =>
        {
            // Arrange
            var (apiClient, workflowStateService, pluginStateService, handler) = CreateTestServices();
            var stateChangedCount = 0;
            workflowStateService.OnStateChanged += () => stateChangedCount++;

            // Setup mock responses
            handler.SetupInstallResponse(scenario.PackageId, scenario.Version, success: true);
            handler.SetupNodeDefinitionsResponse(scenario.ExpectedNodeDefinitions);

            // Act
            var result = pluginStateService.InstallPackageAsync(scenario.PackageId, scenario.Version)
                .GetAwaiter().GetResult();

            // Assert
            Assert.True(result, "Installation should succeed");

            // Property: Node definitions should be refreshed after successful installation
            var definitions = workflowStateService.NodeDefinitions;
            Assert.Equal(scenario.ExpectedNodeDefinitions.Count, definitions.Count);

            // Property: State changed event should be triggered
            Assert.True(stateChangedCount > 0, "OnStateChanged should be triggered after node refresh");

            // Property: Plugin nodes should have SourcePackage set
            var pluginNodes = definitions.Where(d => d.IsPluginNode).ToList();
            foreach (var node in pluginNodes)
            {
                Assert.False(string.IsNullOrEmpty(node.SourcePackage),
                    "Plugin nodes must have SourcePackage set");
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 3: Node Refresh After Package Operations
    /// For any successful package update, the Designer SHALL trigger a node definitions refresh.
    /// Validates: Requirements 5.4, 9.3
    /// </summary>
    [Fact]
    public void NodeRefreshAfterUpdate_SuccessfulUpdate_RefreshesNodeDefinitions()
    {
        GenUpdateScenario.Sample(scenario =>
        {
            // Arrange
            var (apiClient, workflowStateService, pluginStateService, handler) = CreateTestServices();
            var stateChangedCount = 0;
            workflowStateService.OnStateChanged += () => stateChangedCount++;

            // Setup initial installed packages
            handler.SetupInstalledPackagesResponse([scenario.InstalledPackage]);
            pluginStateService.LoadInstalledPackagesAsync().GetAwaiter().GetResult();

            // Setup mock responses for update
            handler.SetupUpdateResponse(scenario.PackageId, scenario.TargetVersion, success: true);
            handler.SetupNodeDefinitionsResponse(scenario.ExpectedNodeDefinitions);

            // Reset state change counter after initial load
            stateChangedCount = 0;

            // Act
            var result = pluginStateService.UpdatePackageAsync(scenario.PackageId, scenario.TargetVersion)
                .GetAwaiter().GetResult();

            // Assert
            Assert.True(result, "Update should succeed");

            // Property: Node definitions should be refreshed after successful update
            var definitions = workflowStateService.NodeDefinitions;
            Assert.Equal(scenario.ExpectedNodeDefinitions.Count, definitions.Count);

            // Property: State changed event should be triggered
            Assert.True(stateChangedCount > 0, "OnStateChanged should be triggered after node refresh");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 3: Node Refresh After Package Operations
    /// For any successful package uninstallation, the Designer SHALL trigger a node definitions refresh,
    /// and the Node Palette SHALL remove the uninstalled package's nodes.
    /// Validates: Requirements 6.6, 9.2
    /// </summary>
    [Fact]
    public void NodeRefreshAfterUninstall_SuccessfulUninstall_RefreshesNodeDefinitions()
    {
        GenUninstallScenario.Sample(scenario =>
        {
            // Arrange
            var (apiClient, workflowStateService, pluginStateService, handler) = CreateTestServices();
            var stateChangedCount = 0;
            workflowStateService.OnStateChanged += () => stateChangedCount++;

            // Setup initial installed packages
            handler.SetupInstalledPackagesResponse([scenario.InstalledPackage]);
            pluginStateService.LoadInstalledPackagesAsync().GetAwaiter().GetResult();

            // Setup mock responses for uninstall
            handler.SetupUninstallResponse(scenario.PackageId, success: true);
            handler.SetupNodeDefinitionsResponse(scenario.ExpectedNodeDefinitionsAfterUninstall);

            // Reset state change counter after initial load
            stateChangedCount = 0;

            // Act
            var result = pluginStateService.UninstallPackageAsync(scenario.PackageId)
                .GetAwaiter().GetResult();

            // Assert
            Assert.True(result.Success, "Uninstall should succeed");

            // Property: Node definitions should be refreshed after successful uninstall
            var definitions = workflowStateService.NodeDefinitions;
            Assert.Equal(scenario.ExpectedNodeDefinitionsAfterUninstall.Count, definitions.Count);

            // Property: Uninstalled package's nodes should be removed
            var uninstalledPackageNodes = definitions
                .Where(d => d.SourcePackage == scenario.PackageId)
                .ToList();
            Assert.Empty(uninstalledPackageNodes);

            // Property: State changed event should be triggered
            Assert.True(stateChangedCount > 0, "OnStateChanged should be triggered after node refresh");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 3: Node Refresh After Package Operations
    /// For any failed package operation, the Designer SHALL NOT modify the current node definitions.
    /// Validates: Requirements 4.4, 5.4, 6.6
    /// </summary>
    [Fact]
    public void NodeRefreshAfterFailedOperation_FailedInstall_PreservesNodeDefinitions()
    {
        GenFailedInstallScenario.Sample(scenario =>
        {
            // Arrange
            var (apiClient, workflowStateService, pluginStateService, handler) = CreateTestServices();

            // Setup initial node definitions
            workflowStateService.SetNodeDefinitions(scenario.InitialNodeDefinitions);
            var initialCount = workflowStateService.NodeDefinitions.Count;

            // Setup mock responses for failed install
            handler.SetupInstallResponse(scenario.PackageId, scenario.Version, success: false);

            // Act
            var result = pluginStateService.InstallPackageAsync(scenario.PackageId, scenario.Version)
                .GetAwaiter().GetResult();

            // Assert
            Assert.False(result, "Installation should fail");

            // Property: Node definitions should remain unchanged after failed operation
            Assert.Equal(initialCount, workflowStateService.NodeDefinitions.Count);
        }, iter: 100);
    }

    #region Test Scenarios

    private record InstallScenario
    {
        public required string PackageId { get; init; }
        public string? Version { get; init; }
        public required List<NodeDefinition> ExpectedNodeDefinitions { get; init; }
    }

    private record UpdateScenario
    {
        public required string PackageId { get; init; }
        public required string TargetVersion { get; init; }
        public required InstalledPackageModel InstalledPackage { get; init; }
        public required List<NodeDefinition> ExpectedNodeDefinitions { get; init; }
    }

    private record UninstallScenario
    {
        public required string PackageId { get; init; }
        public required InstalledPackageModel InstalledPackage { get; init; }
        public required List<NodeDefinition> ExpectedNodeDefinitionsAfterUninstall { get; init; }
    }

    private record FailedInstallScenario
    {
        public required string PackageId { get; init; }
        public string? Version { get; init; }
        public required List<NodeDefinition> InitialNodeDefinitions { get; init; }
    }

    #endregion

    #region Generators

    private static readonly Gen<string> GenPackageId =
        Gen.Char['a', 'z'].Array[5, 15].Select(chars => $"Vyshyvanka.Plugin.{new string(chars)}");

    private static readonly Gen<string> GenVersion =
        from major in Gen.Int[1, 5]
        from minor in Gen.Int[0, 10]
        from patch in Gen.Int[0, 20]
        select $"{major}.{minor}.{patch}";

    private static readonly Gen<NodeDefinition> GenNodeDefinition =
        from type in Gen.Char['a', 'z'].Array[5, 15].Select(chars => new string(chars))
        from name in Gen.Char['A', 'Z'].Array[1, 1].Select(c => new string(c))
            .SelectMany(first => Gen.Char['a', 'z'].Array[4, 14].Select(rest => first + new string(rest)))
        from category in Gen.Int[0, 3].Select(i => (NodeCategory)i)
        from hasSourcePackage in Gen.Bool
        from sourcePackageId in GenPackageId
        select new NodeDefinition
        {
            Type = type,
            Name = name,
            Description = $"Description for {name}",
            Category = category,
            Icon = "📦",
            SourcePackage = hasSourcePackage ? sourcePackageId : null,
            ConfigurationSchema = JsonDocument.Parse("{}").RootElement
        };

    private static readonly Gen<List<NodeDefinition>> GenNodeDefinitions =
        GenNodeDefinition.List[1, 10];

    private static readonly Gen<InstalledPackageModel> GenInstalledPackage =
        from packageId in GenPackageId
        from version in GenVersion
        from daysAgo in Gen.Int[1, 30]
        select new InstalledPackageModel
        {
            PackageId = packageId,
            Version = version,
            SourceName = "nuget.org",
            InstalledAt = DateTime.UtcNow.AddDays(-daysAgo),
            NodeTypes = ["TestNode1", "TestNode2"],
            IsLoaded = true,
            HasUpdate = false
        };

    private static readonly Gen<InstallScenario> GenInstallScenario =
        from packageId in GenPackageId
        from version in GenVersion
        from definitions in GenNodeDefinitions
        select new InstallScenario
        {
            PackageId = packageId,
            Version = version,
            ExpectedNodeDefinitions = definitions
        };

    private static readonly Gen<UpdateScenario> GenUpdateScenario =
        from packageId in GenPackageId
        from currentVersion in GenVersion
        from targetVersion in GenVersion
        from definitions in GenNodeDefinitions
        select new UpdateScenario
        {
            PackageId = packageId,
            TargetVersion = targetVersion,
            InstalledPackage = new InstalledPackageModel
            {
                PackageId = packageId,
                Version = currentVersion,
                SourceName = "nuget.org",
                InstalledAt = DateTime.UtcNow.AddDays(-7),
                NodeTypes = ["TestNode"],
                IsLoaded = true,
                HasUpdate = true,
                LatestVersion = targetVersion
            },
            ExpectedNodeDefinitions = definitions
        };

    private static readonly Gen<UninstallScenario> GenUninstallScenario =
        from packageId in GenPackageId
        from version in GenVersion
        from remainingDefinitions in GenNodeDefinitions
        select new UninstallScenario
        {
            PackageId = packageId,
            InstalledPackage = new InstalledPackageModel
            {
                PackageId = packageId,
                Version = version,
                SourceName = "nuget.org",
                InstalledAt = DateTime.UtcNow.AddDays(-7),
                NodeTypes = ["TestNode"],
                IsLoaded = true,
                HasUpdate = false
            },
            ExpectedNodeDefinitionsAfterUninstall = remainingDefinitions
                .Where(d => d.SourcePackage != packageId)
                .ToList()
        };

    private static readonly Gen<FailedInstallScenario> GenFailedInstallScenario =
        from packageId in GenPackageId
        from version in GenVersion
        from initialDefinitions in GenNodeDefinitions
        select new FailedInstallScenario
        {
            PackageId = packageId,
            Version = version,
            InitialNodeDefinitions = initialDefinitions
        };

    #endregion

    #region Test Setup

    private static (VyshyvankaApiClient ApiClient, WorkflowStateService WorkflowStateService,
        PluginStateService PluginStateService, MockHttpMessageHandler Handler) CreateTestServices()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5000/")
        };

        var apiClient = new VyshyvankaApiClient(httpClient);
        var workflowStateService = new WorkflowStateService();
        var pluginStateService = new PluginStateService(apiClient, workflowStateService);

        return (apiClient, workflowStateService, pluginStateService, handler);
    }

    /// <summary>
    /// Mock HTTP message handler for testing API calls.
    /// </summary>
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public void SetupInstallResponse(string packageId, string? version, bool success)
        {
            var url = $"api/packages/{packageId}/install";
            _responses[url] = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    Success = success,
                    Package = success
                        ? new
                        {
                            PackageId = packageId,
                            Version = version ?? "1.0.0",
                            SourceName = "nuget.org",
                            InstallPath = $"/packages/{packageId}",
                            InstalledAt = DateTime.UtcNow,
                            NodeTypes = new[] { "TestNode" },
                            Dependencies = Array.Empty<string>(),
                            IsLoaded = true
                        }
                        : null,
                    InstalledDependencies = Array.Empty<object>(),
                    Errors = success ? Array.Empty<string>() : new[] { "Installation failed" },
                    Warnings = Array.Empty<string>()
                })
            };
        }

        public void SetupUpdateResponse(string packageId, string targetVersion, bool success)
        {
            var url = $"api/packages/{packageId}/update";
            _responses[url] = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    Success = success,
                    Package = success
                        ? new
                        {
                            PackageId = packageId,
                            Version = targetVersion,
                            SourceName = "nuget.org",
                            InstallPath = $"/packages/{packageId}",
                            InstalledAt = DateTime.UtcNow,
                            NodeTypes = new[] { "TestNode" },
                            Dependencies = Array.Empty<string>(),
                            IsLoaded = true
                        }
                        : null,
                    PreviousVersion = "1.0.0",
                    Errors = success ? Array.Empty<string>() : new[] { "Update failed" },
                    Warnings = Array.Empty<string>()
                })
            };
        }

        public void SetupUninstallResponse(string packageId, bool success)
        {
            var url = $"api/packages/{packageId}";
            _responses[url] = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    Success = success,
                    PackageId = packageId,
                    RemovedDependencies = Array.Empty<string>(),
                    AffectedWorkflows = Array.Empty<string>(),
                    Errors = success ? Array.Empty<string>() : new[] { "Uninstall failed" }
                })
            };
        }

        public void SetupNodeDefinitionsResponse(List<NodeDefinition> definitions)
        {
            _responses["api/nodes"] = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(definitions.Select(d => new
                {
                    d.Type,
                    d.Name,
                    d.Description,
                    Category = (int)d.Category,
                    d.Icon,
                    Inputs = d.Inputs.Select(i => new { i.Name, i.DisplayName, Type = (int)i.Type, i.IsRequired }),
                    Outputs = d.Outputs.Select(o => new { o.Name, o.DisplayName, Type = (int)o.Type, o.IsRequired }),
                    ConfigurationSchema = new { },
                    d.RequiredCredentialType,
                    d.SourcePackage
                }).ToList())
            };
        }

        public void SetupInstalledPackagesResponse(List<InstalledPackageModel> packages)
        {
            _responses["api/packages"] = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(packages.Select(p => new
                {
                    p.PackageId,
                    p.Version,
                    p.SourceName,
                    InstallPath = $"/packages/{p.PackageId}",
                    p.InstalledAt,
                    p.NodeTypes,
                    Dependencies = Array.Empty<string>(),
                    p.IsLoaded
                }).ToList())
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery.TrimStart('/') ?? "";

            // Remove query string for matching
            var pathWithoutQuery = path.Split('?')[0];

            // Try exact match first
            if (_responses.TryGetValue(pathWithoutQuery, out var handler))
            {
                return Task.FromResult(handler(request));
            }

            // Try prefix match for parameterized routes
            foreach (var (key, value) in _responses)
            {
                if (pathWithoutQuery.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(value(request));
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    #endregion
}
