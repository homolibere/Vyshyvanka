using CsCheck;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Packages;
using Vyshyvanka.Engine.Registry;
using NuGet.Versioning;
using NSubstitute;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for NuGetPackageManager offline loading functionality.
/// </summary>
public class OfflineLoadingTests
{
    /// <summary>
    /// Feature: nuget-plugin-system, Property 9: Offline Loading
    /// For any valid Package_Manifest with packages in the Package_Cache,
    /// the Package_Manager SHALL successfully load all packages on startup without network access.
    /// Validates: Requirements 8.1, 8.2
    /// </summary>
    [Fact]
    public void OfflineLoading_ValidManifestWithCachedPackages_LoadsWithoutNetwork()
    {
        GenOfflineLoadingScenario().Sample(scenario =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange: Create manifest with packages and simulate cached packages
                var manifestManager = new ManifestManager(testManifestPath, testCacheDirectory);
                var installedPackages = new List<InstalledPackage>();

                // Create package directories in cache to simulate installed packages
                foreach (var packageInfo in scenario.Packages)
                {
                    var extractionPath = Path.Combine(
                        testCacheDirectory,
                        $"{packageInfo.PackageId.ToLowerInvariant()}.{packageInfo.Version}");

                    Directory.CreateDirectory(extractionPath);

                    // Create a dummy DLL file to simulate an extracted package
                    var dllPath = Path.Combine(extractionPath, $"{packageInfo.PackageId}.dll");
                    File.WriteAllText(dllPath, "dummy content");

                    var installedPackage = new InstalledPackage
                    {
                        PackageId = packageInfo.PackageId,
                        Version = NuGetVersion.Parse(packageInfo.Version),
                        SourceName = "nuget.org",
                        InstallPath = extractionPath,
                        InstalledAt = DateTime.UtcNow.AddDays(-packageInfo.DaysAgo),
                        NodeTypes = packageInfo.NodeTypes,
                        Dependencies = [],
                        IsLoaded = false
                    };

                    installedPackages.Add(installedPackage);
                }

                // Save manifest with installed packages
                var manifest = new PackageManifest
                {
                    Version = 1,
                    LastModified = DateTime.UtcNow,
                    Packages = installedPackages,
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
                };

                manifestManager.SaveAsync(manifest).GetAwaiter().GetResult();

                // Create mock source service that throws if network is accessed
                var mockSourceService = CreateOfflineSourceService();

                // Create mock plugin loader that simulates successful loading
                var mockPluginLoader = Substitute.For<IPluginLoader>();
                mockPluginLoader.LoadPlugins(Arg.Any<string>())
                    .Returns(callInfo =>
                    {
                        var path = callInfo.Arg<string>();
                        var packageId = Path.GetFileName(path).Split('.')[0];
                        return
                        [
                            new PluginInfo
                            {
                                Id = packageId,
                                Name = packageId,
                                Version = "1.0.0",
                                IsLoaded = true,
                                NodeTypes = []
                            }
                        ];
                    });

                var mockPluginValidator = Substitute.For<IPluginValidator>();
                mockPluginValidator.ValidatePlugin(Arg.Any<System.Reflection.Assembly>())
                    .Returns(PluginValidationResult.Success());

                var nodeRegistry = new NodeRegistry();
                var packageCache = new PackageCache(testCacheDirectory);
                var dependencyResolver = new DependencyResolver(mockSourceService);

                var options = new PackageOptions
                {
                    CacheDirectory = testCacheDirectory,
                    ManifestPath = testManifestPath
                };

                var packageManager = new NuGetPackageManager(
                    mockSourceService,
                    manifestManager,
                    dependencyResolver,
                    packageCache,
                    mockPluginLoader,
                    mockPluginValidator,
                    nodeRegistry,
                    workflowRepository: null,
                    options);

                // Act: Initialize package manager (should load from cache without network)
                packageManager.InitializeAsync().GetAwaiter().GetResult();

                // Assert: All packages from manifest should be loaded
                var loadedPackages = packageManager.GetInstalledPackages();

                // Property 1: All packages in manifest should be present after initialization
                Assert.Equal(scenario.Packages.Count, loadedPackages.Count);

                // Property 2: Each package should have correct metadata
                foreach (var expectedPackage in scenario.Packages)
                {
                    var loadedPackage = loadedPackages.FirstOrDefault(p =>
                        string.Equals(p.PackageId, expectedPackage.PackageId, StringComparison.OrdinalIgnoreCase));

                    Assert.NotNull(loadedPackage);
                    Assert.Equal(expectedPackage.Version, loadedPackage.Version.ToNormalizedString());
                }

                // Property 3: Plugin loader should have been called for each package
                foreach (var package in installedPackages)
                {
                    mockPluginLoader.Received().LoadPlugins(package.InstallPath);
                }

                // Property 4: No network calls should have been made
                mockSourceService.DidNotReceive().GetRepository(Arg.Any<PackageSource>());
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: nuget-plugin-system, Property 9: Offline Loading (Missing Package)
    /// When a package in the manifest is missing from the cache, initialization should
    /// handle it gracefully and continue loading other packages.
    /// Validates: Requirements 8.1, 8.2
    /// </summary>
    [Fact]
    public void OfflineLoading_MissingPackageInCache_HandlesGracefully()
    {
        GenMissingPackageScenario().Sample(scenario =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange: Create manifest with packages but only some in cache
                var manifestManager = new ManifestManager(testManifestPath, testCacheDirectory);
                var installedPackages = new List<InstalledPackage>();

                for (var i = 0; i < scenario.Packages.Count; i++)
                {
                    var packageInfo = scenario.Packages[i];
                    var extractionPath = Path.Combine(
                        testCacheDirectory,
                        $"{packageInfo.PackageId.ToLowerInvariant()}.{packageInfo.Version}");

                    // Only create cache directory for packages not in missing list
                    var isMissing = scenario.MissingIndices.Contains(i);
                    if (!isMissing)
                    {
                        Directory.CreateDirectory(extractionPath);
                        var dllPath = Path.Combine(extractionPath, $"{packageInfo.PackageId}.dll");
                        File.WriteAllText(dllPath, "dummy content");
                    }

                    var installedPackage = new InstalledPackage
                    {
                        PackageId = packageInfo.PackageId,
                        Version = NuGetVersion.Parse(packageInfo.Version),
                        SourceName = "nuget.org",
                        InstallPath = extractionPath,
                        InstalledAt = DateTime.UtcNow.AddDays(-packageInfo.DaysAgo),
                        NodeTypes = packageInfo.NodeTypes,
                        Dependencies = [],
                        IsLoaded = false
                    };

                    installedPackages.Add(installedPackage);
                }

                var manifest = new PackageManifest
                {
                    Version = 1,
                    LastModified = DateTime.UtcNow,
                    Packages = installedPackages,
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
                };

                manifestManager.SaveAsync(manifest).GetAwaiter().GetResult();

                var mockSourceService = CreateOfflineSourceService();
                var mockPluginLoader = Substitute.For<IPluginLoader>();
                mockPluginLoader.LoadPlugins(Arg.Any<string>())
                    .Returns(callInfo =>
                    {
                        var path = callInfo.Arg<string>();
                        if (!Directory.Exists(path))
                        {
                            return [];
                        }

                        var packageId = Path.GetFileName(path).Split('.')[0];
                        return
                        [
                            new PluginInfo
                            {
                                Id = packageId,
                                Name = packageId,
                                Version = "1.0.0",
                                IsLoaded = true,
                                NodeTypes = []
                            }
                        ];
                    });

                var mockPluginValidator = Substitute.For<IPluginValidator>();
                mockPluginValidator.ValidatePlugin(Arg.Any<System.Reflection.Assembly>())
                    .Returns(PluginValidationResult.Success());

                var nodeRegistry = new NodeRegistry();
                var packageCache = new PackageCache(testCacheDirectory);
                var dependencyResolver = new DependencyResolver(mockSourceService);

                var options = new PackageOptions
                {
                    CacheDirectory = testCacheDirectory,
                    ManifestPath = testManifestPath
                };

                var packageManager = new NuGetPackageManager(
                    mockSourceService,
                    manifestManager,
                    dependencyResolver,
                    packageCache,
                    mockPluginLoader,
                    mockPluginValidator,
                    nodeRegistry,
                    workflowRepository: null,
                    options);

                // Act: Initialize should not throw even with missing packages
                var exception = Record.Exception(() =>
                    packageManager.InitializeAsync().GetAwaiter().GetResult());

                // Assert: Initialization should complete without throwing
                Assert.Null(exception);

                // Property: Missing packages should be removed, present ones should remain
                var loadedPackages = packageManager.GetInstalledPackages();
                var expectedCount = scenario.Packages.Count - scenario.MissingIndices.Count;
                Assert.Equal(expectedCount, loadedPackages.Count);
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: nuget-plugin-system, Property 9: Offline Loading (Empty Manifest)
    /// When the manifest is empty, initialization should complete successfully.
    /// Validates: Requirements 8.1, 8.2
    /// </summary>
    [Fact]
    public void OfflineLoading_EmptyManifest_InitializesSuccessfully()
    {
        Gen.Int[1, 100].Sample(_ =>
        {
            var (testCacheDirectory, testManifestPath) = CreateTestDirectories();

            try
            {
                // Arrange: Create empty manifest
                var manifestManager = new ManifestManager(testManifestPath, testCacheDirectory);
                var manifest = new PackageManifest
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
                };

                manifestManager.SaveAsync(manifest).GetAwaiter().GetResult();

                var mockSourceService = CreateOfflineSourceService();
                var mockPluginLoader = Substitute.For<IPluginLoader>();
                var mockPluginValidator = Substitute.For<IPluginValidator>();
                var nodeRegistry = new NodeRegistry();
                var packageCache = new PackageCache(testCacheDirectory);
                var dependencyResolver = new DependencyResolver(mockSourceService);

                var options = new PackageOptions
                {
                    CacheDirectory = testCacheDirectory,
                    ManifestPath = testManifestPath
                };

                var packageManager = new NuGetPackageManager(
                    mockSourceService,
                    manifestManager,
                    dependencyResolver,
                    packageCache,
                    mockPluginLoader,
                    mockPluginValidator,
                    nodeRegistry,
                    workflowRepository: null,
                    options);

                // Act
                var exception = Record.Exception(() =>
                    packageManager.InitializeAsync().GetAwaiter().GetResult());

                // Assert
                Assert.Null(exception);
                Assert.Empty(packageManager.GetInstalledPackages());

                // No plugin loading should occur
                mockPluginLoader.DidNotReceive().LoadPlugins(Arg.Any<string>());
            }
            finally
            {
                CleanupTestDirectory(testCacheDirectory);
            }
        }, iter: 100);
    }

    #region Test Scenarios

    private record PackageInfo
    {
        public required string PackageId { get; init; }
        public required string Version { get; init; }
        public int DaysAgo { get; init; }
        public IReadOnlyList<string> NodeTypes { get; init; } = [];
    }

    private record OfflineLoadingScenario
    {
        public required IReadOnlyList<PackageInfo> Packages { get; init; }
    }

    private record MissingPackageScenario
    {
        public required IReadOnlyList<PackageInfo> Packages { get; init; }
        public required IReadOnlyList<int> MissingIndices { get; init; }
    }

    #endregion

    #region Generators

    private static Gen<string> GenPackageId() =>
        Gen.Select(
            Gen.Const("TestPackage"),
            Gen.Char['A', 'Z'].Array[1, 1].Select(chars => new string(chars)),
            Gen.Int[1, 999],
            (prefix, suffix, num) => $"{prefix}.{suffix}{num}");

    private static Gen<string> GenVersion() =>
        Gen.Select(
            Gen.Int[1, 5],
            Gen.Int[0, 10],
            Gen.Int[0, 20],
            (major, minor, patch) => $"{major}.{minor}.{patch}");

    private static Gen<string> GenNodeTypeName() =>
        Gen.Select(
            Gen.Const("Vyshyvanka.Plugins."),
            Gen.Char['A', 'Z'].Array[1, 1].Select(chars => new string(chars)),
            Gen.Char['a', 'z'].Array[5, 10].Select(chars => new string(chars)),
            (prefix, name, suffix) => prefix + name + suffix + "Node");

    private static Gen<PackageInfo> GenPackageInfo() =>
        Gen.Select(
            GenPackageId(),
            GenVersion(),
            Gen.Int[1, 30],
            GenNodeTypeName().Array[0, 3],
            (packageId, version, daysAgo, nodeTypes) => new PackageInfo
            {
                PackageId = packageId,
                Version = version,
                DaysAgo = daysAgo,
                NodeTypes = nodeTypes
            });

    private static Gen<OfflineLoadingScenario> GenOfflineLoadingScenario() =>
        GenPackageInfo().Array[1, 5].Select(packages => new OfflineLoadingScenario
        {
            Packages = EnsureUniquePackageIds(packages)
        });

    private static Gen<MissingPackageScenario> GenMissingPackageScenario() =>
        Gen.Select(
            GenPackageInfo().Array[2, 5],
            Gen.Int[1, 100],
            (packages, seed) =>
            {
                var uniquePackages = EnsureUniquePackageIds(packages);
                var packageCount = uniquePackages.Count;

                // Generate missing indices (at least 1, at most packageCount - 1)
                var missingCount = Math.Max(1, Math.Min(packageCount - 1, (seed % (packageCount - 1)) + 1));
                var missingIndices = Enumerable.Range(0, packageCount)
                    .OrderBy(x => (x + seed) % packageCount)
                    .Take(missingCount)
                    .OrderBy(x => x)
                    .ToList();

                return new MissingPackageScenario
                {
                    Packages = uniquePackages,
                    MissingIndices = missingIndices
                };
            });

    private static IReadOnlyList<PackageInfo> EnsureUniquePackageIds(IReadOnlyList<PackageInfo> packages)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PackageInfo>();
        var counter = 1;

        foreach (var package in packages)
        {
            var packageId = package.PackageId;
            while (seen.Contains(packageId))
            {
                packageId = $"{package.PackageId}_{counter++}";
            }

            seen.Add(packageId);
            result.Add(package with { PackageId = packageId });
        }

        return result;
    }

    #endregion

    #region Test Setup

    private static (string CacheDirectory, string ManifestPath) CreateTestDirectories()
    {
        var testId = Guid.NewGuid().ToString("N")[..8];
        var testCacheDirectory = Path.Combine(Path.GetTempPath(), $"vyshyvanka-offline-test-{testId}");
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
                // Ignore cleanup errors in tests
            }
        }
    }

    private static IPackageSourceService CreateOfflineSourceService()
    {
        var mockService = Substitute.For<IPackageSourceService>();
        var mockSource = new PackageSource
        {
            Name = "nuget.org",
            Url = "https://api.nuget.org/v3/index.json",
            IsEnabled = true,
            IsTrusted = true
        };

        mockService.GetSources().Returns([mockSource]);

        // GetRepository should not be called during offline initialization
        // If it is called, it means network access was attempted
        mockService.GetRepository(Arg.Any<PackageSource>())
            .Returns(_ => throw new InvalidOperationException(
                "Network access attempted during offline initialization"));

        return mockService;
    }

    #endregion
}
