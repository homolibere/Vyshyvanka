using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Packages;
using Microsoft.Extensions.Logging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetLogger = NuGet.Common.ILogger;

namespace Vyshyvanka.Tests.Unit;

public class PackageSearchServiceTests
{
    private readonly IPackageSourceService _sourceService = Substitute.For<IPackageSourceService>();
    private readonly ILogger<PackageSearchService> _logger = Substitute.For<ILogger<PackageSearchService>>();
    private readonly PackageSearchService _sut;

    public PackageSearchServiceTests()
    {
        _sut = new PackageSearchService(_sourceService, _logger);
    }

    private static PackageSource CreateSource(string name = "test-source", bool isEnabled = true, bool isTrusted = true)
    {
        return new PackageSource
        {
            Name = name,
            Url = $"https://nuget.example.com/{name}/v3/index.json",
            IsEnabled = isEnabled,
            IsTrusted = isTrusted,
            Priority = 0
        };
    }

    private static InstalledPackage CreateInstalledPackage(string packageId, string version = "1.0.0")
    {
        return new InstalledPackage
        {
            PackageId = packageId,
            Version = new NuGetVersion(version),
            SourceName = "test-source",
            InstallPath = $"/packages/{packageId}",
            InstalledAt = DateTime.UtcNow,
            NodeTypes = [],
            Dependencies = [],
            IsLoaded = true
        };
    }

    #region SearchPackagesAsync

    [Fact]
    public async Task WhenNoSourcesEnabledThenReturnsEmptyResults()
    {
        var disabledSource = CreateSource(isEnabled: false);
        _sourceService.GetSources().Returns([disabledSource]);

        var result = await _sut.SearchPackagesAsync("test");

        result.Packages.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenNoSourcesConfiguredThenReturnsEmptyResults()
    {
        _sourceService.GetSources().Returns([]);

        var result = await _sut.SearchPackagesAsync("test");

        result.Packages.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task WhenSourceThrowsThenCapturesErrorAndContinues()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);
        repository.GetResourceAsync<PackageSearchResource>(Arg.Any<CancellationToken>())
            .Returns<PackageSearchResource>(_ => throw new HttpRequestException("Connection refused"));

        var result = await _sut.SearchPackagesAsync("test");

        result.Packages.Should().BeEmpty();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("test-source");
    }

    #endregion

    #region GetPackageDetailsAsync

    [Fact]
    public async Task WhenNoSourcesEnabledThenReturnsNull()
    {
        var disabledSource = CreateSource(isEnabled: false);
        _sourceService.GetSources().Returns([disabledSource]);

        var result = await _sut.GetPackageDetailsAsync("SomePackage");

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenAllSourcesFailThenReturnsNull()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);
        repository.GetResourceAsync<PackageMetadataResource>(Arg.Any<CancellationToken>())
            .Returns<PackageMetadataResource>(_ => throw new Exception("Network error"));

        var result = await _sut.GetPackageDetailsAsync("SomePackage");

        result.Should().BeNull();
    }

    #endregion

    #region CheckForUpdatesAsync

    [Fact]
    public async Task WhenNoPackagesInstalledThenReturnsEmptyList()
    {
        var result = await _sut.CheckForUpdatesAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenNewerVersionAvailableThenReturnsUpdateInfo()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.GetAllVersionsAsync("TestPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0"), new NuGetVersion("2.0.0"), new NuGetVersion("3.0.0")]);

        var installed = CreateInstalledPackage("TestPackage", "1.0.0");

        var result = await _sut.CheckForUpdatesAsync([installed]);

        result.Should().HaveCount(1);
        result[0].PackageId.Should().Be("TestPackage");
        result[0].CurrentVersion.Should().Be(new NuGetVersion("1.0.0"));
        result[0].LatestVersion.Should().Be(new NuGetVersion("3.0.0"));
    }

    [Fact]
    public async Task WhenAlreadyOnLatestVersionThenReturnsEmptyList()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.GetAllVersionsAsync("TestPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0"), new NuGetVersion("2.0.0")]);

        var installed = CreateInstalledPackage("TestPackage", "2.0.0");

        var result = await _sut.CheckForUpdatesAsync([installed]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenCheckForUpdatesFailsForOnePackageThenContinuesWithOthers()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        // First package throws
        findResource.GetAllVersionsAsync("FailingPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns<IEnumerable<NuGetVersion>>(_ => throw new Exception("Network error"));

        // Second package has update
        findResource.GetAllVersionsAsync("GoodPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0"), new NuGetVersion("2.0.0")]);

        var packages = new[]
        {
            CreateInstalledPackage("FailingPackage", "1.0.0"),
            CreateInstalledPackage("GoodPackage", "1.0.0")
        };

        var result = await _sut.CheckForUpdatesAsync(packages);

        result.Should().HaveCount(1);
        result[0].PackageId.Should().Be("GoodPackage");
    }

    [Fact]
    public async Task WhenPrereleaseVersionsAvailableThenExcludesThem()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.GetAllVersionsAsync("TestPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0"), new NuGetVersion("2.0.0-beta1")]);

        var installed = CreateInstalledPackage("TestPackage", "1.0.0");

        var result = await _sut.CheckForUpdatesAsync([installed]);

        // 2.0.0-beta1 is prerelease, so no stable update available
        result.Should().BeEmpty();
    }

    #endregion

    #region ResolveLatestVersionAsync

    [Fact]
    public async Task WhenPackageExistsThenReturnsLatestStableVersion()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.GetAllVersionsAsync("TestPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0"), new NuGetVersion("2.0.0"), new NuGetVersion("3.0.0-rc1")]);

        var result = await _sut.ResolveLatestVersionAsync("TestPackage", prerelease: false);

        result.Should().Be(new NuGetVersion("2.0.0"));
    }

    [Fact]
    public async Task WhenPrereleaseAllowedThenReturnsLatestIncludingPrerelease()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.GetAllVersionsAsync("TestPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0"), new NuGetVersion("2.0.0"), new NuGetVersion("3.0.0-rc1")]);

        var result = await _sut.ResolveLatestVersionAsync("TestPackage", prerelease: true);

        result.Should().Be(new NuGetVersion("3.0.0-rc1"));
    }

    [Fact]
    public async Task WhenPackageNotFoundInAnySourceThenReturnsNull()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.GetAllVersionsAsync("NonExistent", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<NuGetVersion>());

        var result = await _sut.ResolveLatestVersionAsync("NonExistent", prerelease: false);

        result.Should().BeNull();
    }

    [Fact]
    public async Task WhenFirstSourceFailsThenTriesNextSource()
    {
        var source1 = CreateSource("failing-source");
        var source2 = CreateSource("good-source");
        _sourceService.GetSources().Returns([source1, source2]);

        var failingRepo = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source1).Returns(failingRepo);
        failingRepo.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns<FindPackageByIdResource>(_ => throw new Exception("Source down"));

        var goodRepo = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source2).Returns(goodRepo);
        var findResource = Substitute.For<FindPackageByIdResource>();
        goodRepo.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);
        findResource.GetAllVersionsAsync("TestPackage", Arg.Any<SourceCacheContext>(),
                Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns([new NuGetVersion("1.0.0")]);

        var result = await _sut.ResolveLatestVersionAsync("TestPackage", prerelease: false);

        result.Should().Be(new NuGetVersion("1.0.0"));
    }

    #endregion

    #region FindPackageSourceAsync

    [Fact]
    public async Task WhenPackageExistsInSourceThenReturnsRepositoryAndSource()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.DoesPackageExistAsync("TestPackage", new NuGetVersion("1.0.0"),
                Arg.Any<SourceCacheContext>(), Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (repo, src) = await _sut.FindPackageSourceAsync("TestPackage", new NuGetVersion("1.0.0"));

        repo.Should().NotBeNull();
        src.Should().NotBeNull();
        src!.Name.Should().Be("test-source");
    }

    [Fact]
    public async Task WhenPackageNotFoundInAnySourceThenReturnsNulls()
    {
        var source = CreateSource();
        _sourceService.GetSources().Returns([source]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);

        findResource.DoesPackageExistAsync("NonExistent", new NuGetVersion("1.0.0"),
                Arg.Any<SourceCacheContext>(), Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var (repo, src) = await _sut.FindPackageSourceAsync("NonExistent", new NuGetVersion("1.0.0"));

        repo.Should().BeNull();
        src.Should().BeNull();
    }

    [Fact]
    public async Task WhenSourceThrowsDuringFindThenTriesNextSource()
    {
        var source1 = CreateSource("failing");
        var source2 = CreateSource("working");
        _sourceService.GetSources().Returns([source1, source2]);

        var failingRepo = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source1).Returns(failingRepo);
        failingRepo.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns<FindPackageByIdResource>(_ => throw new Exception("Timeout"));

        var workingRepo = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(source2).Returns(workingRepo);
        var findResource = Substitute.For<FindPackageByIdResource>();
        workingRepo.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);
        findResource.DoesPackageExistAsync("TestPackage", new NuGetVersion("1.0.0"),
                Arg.Any<SourceCacheContext>(), Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (repo, src) = await _sut.FindPackageSourceAsync("TestPackage", new NuGetVersion("1.0.0"));

        repo.Should().NotBeNull();
        src.Should().NotBeNull();
        src!.Name.Should().Be("working");
    }

    [Fact]
    public async Task WhenDisabledSourcesThenSkipsThem()
    {
        var disabledSource = CreateSource("disabled", isEnabled: false);
        var enabledSource = CreateSource("enabled", isEnabled: true);
        _sourceService.GetSources().Returns([disabledSource, enabledSource]);

        var repository = Substitute.For<SourceRepository>();
        _sourceService.GetRepository(enabledSource).Returns(repository);

        var findResource = Substitute.For<FindPackageByIdResource>();
        repository.GetResourceAsync<FindPackageByIdResource>(Arg.Any<CancellationToken>())
            .Returns(findResource);
        findResource.DoesPackageExistAsync("TestPackage", new NuGetVersion("1.0.0"),
                Arg.Any<SourceCacheContext>(), Arg.Any<NuGetLogger>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (repo, src) = await _sut.FindPackageSourceAsync("TestPackage", new NuGetVersion("1.0.0"));

        repo.Should().NotBeNull();
        src!.Name.Should().Be("enabled");
        _sourceService.DidNotReceive().GetRepository(disabledSource);
    }

    #endregion
}
