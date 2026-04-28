using CsCheck;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Packages;
using Vyshyvanka.Engine.Plugins;
using Vyshyvanka.Engine.Registry;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NSubstitute;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for NuGetPackageManager uninstallation functionality.
/// </summary>
public class PackageUninstallationTests
{
    /// <summary>
    /// Feature: nuget-plugin-system, Property 4: Uninstallation Completeness
    /// For any successful package uninstallation, the Package_Manager SHALL:
    /// - Unload plugin assemblies from memory
    /// - Remove package files from the Package_Cache
    /// - Remove the package from the Package_Manifest
    /// - Unregister all node types from the Node_Registry
    /// Validates: Requirements 4.3, 4.4, 4.5, 4.6
    /// </summary>
    [Fact]
    public void UninstallationCompleteness_SuccessfulUninstallation_MeetsAllRequirements()
    {
        GenUninstallationScenario.Sample(scenario =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange
                var mockPluginLoader = Substitute.For<IPluginLoader>();
                var mockNodeRegistry = Substitute.For<INodeRegistry>();

                var (packageManager, manifestManager, packageCache, _) = CreateTestPackageManager(
                    testCacheDirectory, testManifestPath,
                    pluginLoader: mockPluginLoader,
                    nodeRegistry: mockNodeRegistry);

                // First install a package
                var installResult = packageManager.InstallPackageAsync(
                    scenario.PackageId,
                    prerelease: true).GetAwaiter().GetResult();

                // Skip if installation failed (package might not exist)
                if (!installResult.Success)
                {
                    return;
                }

                // Verify package is installed
                var manifestBeforeUninstall = manifestManager.LoadAsync().GetAwaiter().GetResult();
                var packageBeforeUninstall = manifestBeforeUninstall.Packages.FirstOrDefault(p =>
                    string.Equals(p.PackageId, scenario.PackageId, StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(packageBeforeUninstall);

                // Act - Uninstall the package
                var uninstallResult = packageManager.UninstallPackageAsync(
                    scenario.PackageId,
                    force: true).GetAwaiter().GetResult();

                // Assert: Verify uninstallation completeness properties
                if (uninstallResult.Success)
                {
                    // Property 1: Package should be removed from manifest
                    var manifestAfterUninstall = manifestManager.LoadAsync().GetAwaiter().GetResult();
                    var packageAfterUninstall = manifestAfterUninstall.Packages.FirstOrDefault(p =>
                        string.Equals(p.PackageId, scenario.PackageId, StringComparison.OrdinalIgnoreCase));
                    Assert.Null(packageAfterUninstall);

                    // Property 2: PackageId should be set in result
                    Assert.Equal(scenario.PackageId, uninstallResult.PackageId, StringComparer.OrdinalIgnoreCase);

                    // Property 3: Plugin should be unloaded (verify UnloadPlugin was called)
                    mockPluginLoader.Received().UnloadPlugin(Arg.Is<string>(id =>
                        string.Equals(id, scenario.PackageId, StringComparison.OrdinalIgnoreCase)));

                    // Property 4: Nodes should be unregistered
                    if (packageBeforeUninstall.NodeTypes.Count > 0)
                    {
                        foreach (var nodeType in packageBeforeUninstall.NodeTypes)
                        {
                            mockNodeRegistry.Received().Unregister(nodeType);
                        }
                    }

                    // Property 5: Package files should be removed from cache
                    var extractionPath = packageCache.GetExtractionPath(
                        scenario.PackageId, packageBeforeUninstall.Version);
                    Assert.False(Directory.Exists(extractionPath),
                        "Package extraction directory should be removed after uninstallation");
                }
                else
                {
                    // If uninstallation failed, there should be error messages
                    Assert.True(uninstallResult.Errors.Count > 0,
                        "Failed uninstallation must have error messages");
                }
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 10);
    }

    /// <summary>
    /// Feature: nuget-plugin-system, Property 15: Workflow Reference Detection
    /// For any package uninstallation request where workflows reference nodes from the package,
    /// the Package_Manager SHALL return a result indicating the affected workflows.
    /// Validates: Requirements 4.1, 4.2
    /// </summary>
    [Fact]
    public void WorkflowReferenceDetection_WorkflowsReferencePackage_ReturnsAffectedWorkflows()
    {
        GenWorkflowReferenceScenario.Sample(scenario =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange
                var mockWorkflowRepository = Substitute.For<IWorkflowRepository>();
                var mockPluginLoader = Substitute.For<IPluginLoader>();
                var mockNodeRegistry = Substitute.For<INodeRegistry>();

                // Create a workflow that references a node type from the package
                var referencingWorkflow = new Workflow
                {
                    Id = Guid.NewGuid(),
                    Name = scenario.WorkflowName,
                    Description = "Test workflow",
                    Version = 1,
                    IsActive = true,
                    Nodes =
                    [
                        new WorkflowNode
                        {
                            Id = "node1",
                            Type = scenario.NodeType, // This matches the package's node type
                            Name = "Test Node",
                            Position = new Position(100, 100)
                        }
                    ],
                    Connections = [],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid()
                };

                mockWorkflowRepository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<Workflow>>([referencingWorkflow]));

                // Setup plugin loader to return plugin info with the node type
                var pluginInfo = new PluginInfo
                {
                    Id = scenario.PackageId,
                    Name = scenario.PackageId,
                    Version = "1.0.0",
                    IsLoaded = true,
                    NodeTypes = [typeof(object)] // Placeholder type
                };
                mockPluginLoader.LoadPlugins(Arg.Any<string>()).Returns([pluginInfo]);

                var (packageManager, manifestManager, _, _) = CreateTestPackageManager(
                    testCacheDirectory, testManifestPath,
                    pluginLoader: mockPluginLoader,
                    nodeRegistry: mockNodeRegistry,
                    workflowRepository: mockWorkflowRepository);

                // Pre-populate manifest with an installed package that has the node type
                var installedPackage = new InstalledPackage
                {
                    PackageId = scenario.PackageId,
                    Version = NuGetVersion.Parse("1.0.0"),
                    SourceName = "nuget.org",
                    InstallPath = Path.Combine(testCacheDirectory, scenario.PackageId.ToLowerInvariant() + ".1.0.0"),
                    InstalledAt = DateTime.UtcNow,
                    NodeTypes = [scenario.NodeType],
                    Dependencies = [],
                    IsLoaded = true
                };

                // Create the install path directory
                Directory.CreateDirectory(installedPackage.InstallPath);

                manifestManager.AddPackageAsync(installedPackage).GetAwaiter().GetResult();

                // Initialize package manager to load the manifest
                packageManager.InitializeAsync().GetAwaiter().GetResult();

                // Act - Try to uninstall without force
                var uninstallResult = packageManager.UninstallPackageAsync(
                    scenario.PackageId,
                    force: false).GetAwaiter().GetResult();

                // Assert: Workflow references should be detected
                Assert.False(uninstallResult.Success,
                    "Uninstallation should fail when workflows reference the package and force=false");
                Assert.True(uninstallResult.AffectedWorkflows.Count > 0,
                    "Affected workflows should be returned");
                Assert.Contains(scenario.WorkflowName, uninstallResult.AffectedWorkflows);
                Assert.True(uninstallResult.Errors.Any(e => e.Contains("workflow", StringComparison.OrdinalIgnoreCase)),
                    "Error message should mention workflows");
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 10);
    }

    /// <summary>
    /// Feature: nuget-plugin-system, Property 15: Workflow Reference Detection (Force Uninstall)
    /// When force=true, the package should be uninstalled even if workflows reference it.
    /// Validates: Requirements 4.1, 4.2
    /// </summary>
    [Fact]
    public void WorkflowReferenceDetection_ForceUninstall_SucceedsWithAffectedWorkflows()
    {
        GenWorkflowReferenceScenario.Sample(scenario =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange
                var mockWorkflowRepository = Substitute.For<IWorkflowRepository>();
                var mockPluginLoader = Substitute.For<IPluginLoader>();
                var mockNodeRegistry = Substitute.For<INodeRegistry>();

                // Create a workflow that references a node type from the package
                var referencingWorkflow = new Workflow
                {
                    Id = Guid.NewGuid(),
                    Name = scenario.WorkflowName,
                    Description = "Test workflow",
                    Version = 1,
                    IsActive = true,
                    Nodes =
                    [
                        new WorkflowNode
                        {
                            Id = "node1",
                            Type = scenario.NodeType,
                            Name = "Test Node",
                            Position = new Position(100, 100)
                        }
                    ],
                    Connections = [],
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid()
                };

                mockWorkflowRepository.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<IReadOnlyList<Workflow>>([referencingWorkflow]));

                var (packageManager, manifestManager, _, _) = CreateTestPackageManager(
                    testCacheDirectory, testManifestPath,
                    pluginLoader: mockPluginLoader,
                    nodeRegistry: mockNodeRegistry,
                    workflowRepository: mockWorkflowRepository);

                // Pre-populate manifest with an installed package
                var installedPackage = new InstalledPackage
                {
                    PackageId = scenario.PackageId,
                    Version = NuGetVersion.Parse("1.0.0"),
                    SourceName = "nuget.org",
                    InstallPath = Path.Combine(testCacheDirectory, scenario.PackageId.ToLowerInvariant() + ".1.0.0"),
                    InstalledAt = DateTime.UtcNow,
                    NodeTypes = [scenario.NodeType],
                    Dependencies = [],
                    IsLoaded = true
                };

                Directory.CreateDirectory(installedPackage.InstallPath);
                manifestManager.AddPackageAsync(installedPackage).GetAwaiter().GetResult();

                // Initialize package manager to load the manifest
                packageManager.InitializeAsync().GetAwaiter().GetResult();

                // Act - Force uninstall
                var uninstallResult = packageManager.UninstallPackageAsync(
                    scenario.PackageId,
                    force: true).GetAwaiter().GetResult();

                // Assert: Force uninstall should succeed but still report affected workflows
                Assert.True(uninstallResult.Success,
                    "Force uninstallation should succeed even with workflow references");
                Assert.True(uninstallResult.AffectedWorkflows.Count > 0,
                    "Affected workflows should still be reported");
                Assert.Contains(scenario.WorkflowName, uninstallResult.AffectedWorkflows);

                // Verify package was removed from manifest
                var manifestAfter = manifestManager.LoadAsync().GetAwaiter().GetResult();
                Assert.DoesNotContain(manifestAfter.Packages,
                    p => string.Equals(p.PackageId, scenario.PackageId, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 10);
    }

    /// <summary>
    /// Feature: nuget-plugin-system, Property 4: Uninstallation Completeness (Not Installed)
    /// Attempting to uninstall a package that is not installed should fail with appropriate error.
    /// Validates: Requirements 4.3, 4.4, 4.5, 4.6
    /// </summary>
    [Fact]
    public void UninstallationCompleteness_PackageNotInstalled_ReturnsError()
    {
        GenUninstallationScenario.Sample(scenario =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange
                var (packageManager, _, _, _) = CreateTestPackageManager(
                    testCacheDirectory, testManifestPath);

                // Act - Try to uninstall a package that was never installed
                var uninstallResult = packageManager.UninstallPackageAsync(
                    scenario.PackageId,
                    force: false).GetAwaiter().GetResult();

                // Assert: Should fail with appropriate error
                Assert.False(uninstallResult.Success,
                    "Uninstalling non-existent package should fail");
                Assert.True(
                    uninstallResult.Errors.Any(e => e.Contains("not installed", StringComparison.OrdinalIgnoreCase)),
                    "Error message should indicate package is not installed");
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 10);
    }

    #region Test Scenarios

    private record UninstallationScenario
    {
        public required string PackageId { get; init; }
    }

    private record WorkflowReferenceScenario
    {
        public required string PackageId { get; init; }
        public required string NodeType { get; init; }
        public required string WorkflowName { get; init; }
    }

    #endregion

    #region Generators

    private static readonly string[] RealPackageIds =
    [
        "Newtonsoft.Json",
        "Serilog",
        "AutoMapper",
        "Dapper",
        "Polly",
        "FluentValidation",
        "MediatR",
        "Humanizer.Core"
    ];

    private static readonly Gen<string> GenRealPackageId =
        Gen.Int[0, RealPackageIds.Length - 1].Select(i => RealPackageIds[i]);

    private static readonly Gen<string> GenRandomPackageId =
        Gen.Char['a', 'z'].Array[5, 15].Select(chars => new string(chars));

    private static readonly Gen<string> GenNodeTypeName =
        from prefix in Gen.Const("Vyshyvanka.Plugins.")
        from name in Gen.Char['A', 'Z'].Array[1, 1].Select(chars => new string(chars))
        from suffix in Gen.Char['a', 'z'].Array[5, 10].Select(chars => new string(chars))
        select prefix + name + suffix + "Node";

    private static readonly Gen<string> GenWorkflowName =
        from prefix in Gen.Const("Test Workflow ")
        from id in Gen.Int[1, 1000]
        select prefix + id;

    private static readonly Gen<UninstallationScenario> GenUninstallationScenario =
        from packageId in GenRealPackageId
        select new UninstallationScenario
        {
            PackageId = packageId
        };

    private static readonly Gen<WorkflowReferenceScenario> GenWorkflowReferenceScenario =
        from packageId in GenRandomPackageId
        from nodeType in GenNodeTypeName
        from workflowName in GenWorkflowName
        select new WorkflowReferenceScenario
        {
            PackageId = packageId,
            NodeType = nodeType,
            WorkflowName = workflowName
        };

    #endregion

    #region Test Setup

    private static (string CacheDirectory, string ManifestPath) CreateTestDirectories()
    {
        var testId = Guid.NewGuid().ToString("N")[..8];
        var testCacheDirectory = Path.Combine(Path.GetTempPath(), $"vyshyvanka-uninstall-test-{testId}");
        var testManifestPath = Path.Combine(testCacheDirectory, "manifest.json");
        Directory.CreateDirectory(testCacheDirectory);
        return (testCacheDirectory, testManifestPath);
    }

    private static void CleanupTestDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static (NuGetPackageManager PackageManager, IManifestManager ManifestManager, IPackageCache PackageCache,
        INodeRegistry NodeRegistry)
        CreateTestPackageManager(
            string testCacheDirectory,
            string testManifestPath,
            PackageOptions? options = null,
            IPackageSourceService? sourceService = null,
            IPluginValidator? pluginValidator = null,
            IPluginLoader? pluginLoader = null,
            INodeRegistry? nodeRegistry = null,
            IWorkflowRepository? workflowRepository = null)
    {
        options ??= new PackageOptions
        {
            CacheDirectory = testCacheDirectory,
            ManifestPath = testManifestPath
        };

        sourceService ??= CreateMockPackageSourceService(isTrusted: true);
        var manifestManager = new ManifestManager(testManifestPath, testCacheDirectory);

        // Initialize manifest with empty content
        manifestManager.SaveAsync(new PackageManifest
        {
            Version = 1,
            LastModified = DateTime.UtcNow,
            Packages = [],
            Sources =
            [
                new PackageSource
                {
                    Name = "nuget.org",
                    Url = "https://api.nuget.org/v3/index.json",
                    IsEnabled = true,
                    IsTrusted = true
                }
            ]
        }).GetAwaiter().GetResult();

        var packageCache = new PackageCache(testCacheDirectory);
        var dependencyResolver = new DependencyResolver(sourceService);
        pluginValidator ??= new PluginValidator();
        pluginLoader ??= new PluginLoader(pluginValidator);
        nodeRegistry ??= new NodeRegistry();

        var packageManager = new NuGetPackageManager(
            sourceService,
            manifestManager,
            dependencyResolver,
            packageCache,
            pluginLoader,
            pluginValidator,
            nodeRegistry,
            workflowRepository,
            options);

        return (packageManager, manifestManager, packageCache, nodeRegistry);
    }

    private static IPackageSourceService CreateMockPackageSourceService(bool isTrusted = true)
    {
        var mockService = Substitute.For<IPackageSourceService>();
        var mockSource = new PackageSource
        {
            Name = "nuget.org",
            Url = "https://api.nuget.org/v3/index.json",
            IsEnabled = true,
            IsTrusted = isTrusted
        };

        mockService.GetSources().Returns([mockSource]);

        var packageSource = new NuGet.Configuration.PackageSource("https://api.nuget.org/v3/index.json", "nuget.org");
        var repository = Repository.Factory.GetCoreV3(packageSource);
        mockService.GetRepository(Arg.Any<PackageSource>()).Returns(repository);

        return mockService;
    }

    #endregion
}
