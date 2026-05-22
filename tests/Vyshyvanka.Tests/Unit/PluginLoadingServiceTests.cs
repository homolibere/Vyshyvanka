using System.Reflection;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Packages;
using Microsoft.Extensions.Logging;

namespace Vyshyvanka.Tests.Unit;

public class PluginLoadingServiceTests
{
    private readonly IPluginLoader _pluginLoader = Substitute.For<IPluginLoader>();
    private readonly IPluginValidator _pluginValidator = Substitute.For<IPluginValidator>();
    private readonly INodeRegistry _nodeRegistry = Substitute.For<INodeRegistry>();
    private readonly IPackageCache _packageCache = Substitute.For<IPackageCache>();
    private readonly ILogger<PluginLoadingService> _logger = Substitute.For<ILogger<PluginLoadingService>>();
    private readonly PluginLoadingService _sut;

    public PluginLoadingServiceTests()
    {
        _sut = new PluginLoadingService(_pluginLoader, _pluginValidator, _nodeRegistry, _packageCache, _logger);
    }

    private static PluginInfo CreatePluginInfo(
        string id = "test-plugin",
        Assembly? assembly = null,
        IReadOnlyList<Type>? nodeTypes = null,
        string filePath = "/plugins/test",
        bool isLoaded = true,
        bool includeAssembly = true)
    {
        return new PluginInfo
        {
            Id = id,
            Name = "Test Plugin",
            Version = "1.0.0",
            Description = "A test plugin",
            Author = "Test",
            FilePath = filePath,
            Assembly = includeAssembly ? (assembly ?? typeof(PluginLoadingServiceTests).Assembly) : null,
            NodeTypes = nodeTypes ?? [],
            IsLoaded = isLoaded,
            LoadedAt = DateTime.UtcNow
        };
    }

    #region LoadAndValidatePluginsAsync

    [Fact]
    public async Task WhenPluginsLoadSuccessfullyThenRegistersAssemblyAndReturnsNodeTypes()
    {
        var assembly = typeof(PluginLoadingServiceTests).Assembly;
        var plugin = CreatePluginInfo(assembly: assembly, nodeTypes: [typeof(TestNode)]);

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);
        _pluginValidator.ValidatePlugin(assembly).Returns(PluginValidationResult.Success());

        var result = await _sut.LoadAndValidatePluginsAsync("test-pkg", new NuGet.Versioning.NuGetVersion("1.0.0"),
            "/install/path");

        result.Failure.Should().BeNull();
        result.NodeTypes.Should().Contain("test-node-type");
        _nodeRegistry.Received(1).RegisterFromAssembly(assembly);
    }

    [Fact]
    public async Task WhenPluginValidationFailsThenReturnsFailureAndRemovesPackage()
    {
        var assembly = typeof(PluginLoadingServiceTests).Assembly;
        var plugin = CreatePluginInfo(assembly: assembly);
        var version = new NuGet.Versioning.NuGetVersion("1.0.0");

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);
        _pluginValidator.ValidatePlugin(assembly).Returns(
            PluginValidationResult.Failure(new PluginValidationError("ERR001", "Invalid plugin")));

        var result = await _sut.LoadAndValidatePluginsAsync("test-pkg", version, "/install/path");

        result.Failure.Should().NotBeNull();
        result.Failure!.Success.Should().BeFalse();
        result.Failure.Errors.Should().Contain(e => e.Contains("Plugin validation failed"));
        await _packageCache.Received(1).RemovePackageAsync("test-pkg", version);
        _nodeRegistry.DidNotReceive().RegisterFromAssembly(Arg.Any<Assembly>());
    }

    [Fact]
    public async Task WhenPluginHasWarningsThenReturnsWarningsWithoutFailure()
    {
        var assembly = typeof(PluginLoadingServiceTests).Assembly;
        var plugin = CreatePluginInfo(assembly: assembly, nodeTypes: [typeof(TestNode)]);

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);
        _pluginValidator.ValidatePlugin(assembly).Returns(new PluginValidationResult
        {
            Warnings = [new PluginValidationWarning("WARN001", "Deprecated API usage")]
        });

        var result = await _sut.LoadAndValidatePluginsAsync("test-pkg", new NuGet.Versioning.NuGetVersion("1.0.0"),
            "/install/path");

        result.Failure.Should().BeNull();
        result.Warnings.Should().Contain("Deprecated API usage");
    }

    [Fact]
    public async Task WhenPluginAssemblyIsNullThenSkipsIt()
    {
        var plugin = CreatePluginInfo(includeAssembly: false);

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);

        var result = await _sut.LoadAndValidatePluginsAsync("test-pkg", new NuGet.Versioning.NuGetVersion("1.0.0"),
            "/install/path");

        result.Failure.Should().BeNull();
        result.NodeTypes.Should().BeEmpty();
        _pluginValidator.DidNotReceive().ValidatePlugin(Arg.Any<Assembly>());
        _nodeRegistry.DidNotReceive().RegisterFromAssembly(Arg.Any<Assembly>());
    }

    [Fact]
    public async Task WhenLoadPluginsThrowsThenTreatsAsLibraryDependency()
    {
        _pluginLoader.LoadPlugins("/install/path").Returns(_ => throw new FileNotFoundException("No DLL found"));

        var result = await _sut.LoadAndValidatePluginsAsync("test-pkg", new NuGet.Versioning.NuGetVersion("1.0.0"),
            "/install/path");

        result.Failure.Should().BeNull();
        result.NodeTypes.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenNoPluginsReturnedThenReturnsEmptyResult()
    {
        _pluginLoader.LoadPlugins("/install/path").Returns([]);

        var result = await _sut.LoadAndValidatePluginsAsync("test-pkg", new NuGet.Versioning.NuGetVersion("1.0.0"),
            "/install/path");

        result.Failure.Should().BeNull();
        result.NodeTypes.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    #endregion

    #region UnloadPlugins

    [Fact]
    public void WhenUnloadingPluginsThenUnloadsById()
    {
        _pluginLoader.GetLoadedPlugins().Returns([]);

        _sut.UnloadPlugins("test-pkg", "/install/path");

        _pluginLoader.Received(1).UnloadPlugin("test-pkg");
    }

    [Fact]
    public void WhenUnloadingPluginsThenAlsoUnloadsMatchingByPath()
    {
        var loadedPlugin = CreatePluginInfo(id: "other-id", filePath: "/install/path/plugin.dll");
        _pluginLoader.GetLoadedPlugins().Returns([loadedPlugin]);

        _sut.UnloadPlugins("test-pkg", "/install/path");

        _pluginLoader.Received(1).UnloadPlugin("test-pkg");
        _pluginLoader.Received(1).UnloadPlugin("other-id");
    }

    [Fact]
    public void WhenUnloadingPluginsThenDoesNotUnloadUnrelatedPlugins()
    {
        var unrelatedPlugin = CreatePluginInfo(id: "unrelated", filePath: "/other/path/plugin.dll");
        _pluginLoader.GetLoadedPlugins().Returns([unrelatedPlugin]);

        _sut.UnloadPlugins("test-pkg", "/install/path");

        _pluginLoader.Received(1).UnloadPlugin("test-pkg");
        _pluginLoader.DidNotReceive().UnloadPlugin("unrelated");
    }

    #endregion

    #region UnloadAndUnregisterPlugins

    [Fact]
    public void WhenUnloadAndUnregisterThenUnregistersAssembly()
    {
        var assembly = typeof(PluginLoadingServiceTests).Assembly;
        var plugin = CreatePluginInfo(id: "test-pkg", assembly: assembly, filePath: "/install/path/plugin.dll");

        _pluginLoader.GetPlugin("test-pkg").Returns(plugin);
        _pluginLoader.GetLoadedPlugins().Returns([]);

        _sut.UnloadAndUnregisterPlugins("test-pkg", "/install/path");

        _pluginLoader.Received(1).UnloadPlugin("test-pkg");
        _nodeRegistry.Received(1).UnregisterFromAssembly(assembly);
    }

    [Fact]
    public void WhenUnloadAndUnregisterWithNoPluginFoundThenDoesNotUnregister()
    {
        _pluginLoader.GetPlugin("test-pkg").Returns((PluginInfo?)null);
        _pluginLoader.GetLoadedPlugins().Returns([]);

        _sut.UnloadAndUnregisterPlugins("test-pkg", "/install/path");

        _pluginLoader.Received(1).UnloadPlugin("test-pkg");
        _nodeRegistry.DidNotReceive().UnregisterFromAssembly(Arg.Any<Assembly>());
    }

    [Fact]
    public void WhenUnloadAndUnregisterFindsPluginByPathThenUnregistersItsAssembly()
    {
        var assembly = typeof(PluginLoadingServiceTests).Assembly;
        var loadedPlugin = CreatePluginInfo(id: "path-match", assembly: assembly, filePath: "/install/path/plugin.dll");

        _pluginLoader.GetPlugin("test-pkg").Returns((PluginInfo?)null);
        _pluginLoader.GetLoadedPlugins().Returns([loadedPlugin]);

        _sut.UnloadAndUnregisterPlugins("test-pkg", "/install/path");

        _pluginLoader.Received(1).UnloadPlugin("test-pkg");
        _pluginLoader.Received(1).UnloadPlugin("path-match");
        _nodeRegistry.Received(1).UnregisterFromAssembly(assembly);
    }

    #endregion

    #region TryLoadPluginsForInitialization

    [Fact]
    public void WhenPluginsLoadSuccessfullyForInitThenReturnsTrue()
    {
        var assembly = typeof(PluginLoadingServiceTests).Assembly;
        var plugin = CreatePluginInfo(assembly: assembly, isLoaded: true);

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);

        var result = _sut.TryLoadPluginsForInitialization("test-pkg", "/install/path");

        result.Should().BeTrue();
        _nodeRegistry.Received(1).RegisterFromAssembly(assembly);
    }

    [Fact]
    public void WhenPluginIsNotLoadedForInitThenSkipsRegistration()
    {
        var plugin = CreatePluginInfo(isLoaded: false);

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);

        var result = _sut.TryLoadPluginsForInitialization("test-pkg", "/install/path");

        result.Should().BeTrue();
        _nodeRegistry.DidNotReceive().RegisterFromAssembly(Arg.Any<Assembly>());
    }

    [Fact]
    public void WhenPluginLoadThrowsForInitThenReturnsFalse()
    {
        _pluginLoader.LoadPlugins("/install/path").Returns(_ => throw new InvalidOperationException("Load failed"));

        var result = _sut.TryLoadPluginsForInitialization("test-pkg", "/install/path");

        result.Should().BeFalse();
    }

    [Fact]
    public void WhenPluginAssemblyIsNullForInitThenSkipsRegistration()
    {
        var plugin = CreatePluginInfo(includeAssembly: false, isLoaded: true);

        _pluginLoader.LoadPlugins("/install/path").Returns([plugin]);

        var result = _sut.TryLoadPluginsForInitialization("test-pkg", "/install/path");

        result.Should().BeTrue();
        _nodeRegistry.DidNotReceive().RegisterFromAssembly(Arg.Any<Assembly>());
    }

    #endregion

    #region ResolveNodeTypeIdentifiers

    [Fact]
    public void WhenNodeTypeIsValidThenReturnsTypeIdentifier()
    {
        var result = _sut.ResolveNodeTypeIdentifiers([typeof(TestNode)]);

        result.Should().Contain("test-node-type");
    }

    [Fact]
    public void WhenNodeTypeCannotBeInstantiatedThenSkipsIt()
    {
        var result = _sut.ResolveNodeTypeIdentifiers([typeof(AbstractNode)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void WhenNodeTypeIsNotINodeThenSkipsIt()
    {
        var result = _sut.ResolveNodeTypeIdentifiers([typeof(string)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void WhenMultipleNodeTypesThenReturnsAll()
    {
        var result = _sut.ResolveNodeTypeIdentifiers([typeof(TestNode), typeof(AnotherTestNode)]);

        result.Should().HaveCount(2);
        result.Should().Contain("test-node-type");
        result.Should().Contain("another-node-type");
    }

    [Fact]
    public void WhenEmptyCollectionThenReturnsEmpty()
    {
        var result = _sut.ResolveNodeTypeIdentifiers([]);

        result.Should().BeEmpty();
    }

    #endregion

    #region Test Helpers

    private class TestNode : INode
    {
        public string Id => "test-node-instance";
        public string Type => "test-node-type";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
            => Task.FromResult(new NodeOutput { Data = default, Success = true });
    }

    private class AnotherTestNode : INode
    {
        public string Id => "another-node-instance";
        public string Type => "another-node-type";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
            => Task.FromResult(new NodeOutput { Data = default, Success = true });
    }

    private abstract class AbstractNode : INode
    {
        public string Id => "abstract";
        public string Type => "abstract";
        public NodeCategory Category => NodeCategory.Action;

        public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);
    }

    #endregion
}
