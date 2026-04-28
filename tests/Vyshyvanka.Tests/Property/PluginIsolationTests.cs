using System.Text.Json;
using CsCheck;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Plugins;
using Vyshyvanka.Engine.Execution;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for plugin isolation and exception handling.
/// Feature: vyshyvanka, Property 18: Plugin Isolation and Exception Handling
/// </summary>
public class PluginIsolationTests
{
    /// <summary>
    /// Feature: vyshyvanka, Property 18: Plugin Isolation and Exception Handling
    /// For any plugin node execution that throws an exception, the Workflow_Engine SHALL 
    /// catch the exception, fail only that node gracefully, and continue system operation 
    /// without affecting other workflows or the core system.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void PluginNodeThrowingException_FailsGracefully_ReturnsErrorOutput()
    {
        // Generate different exception types and messages
        var exceptionGen = Gen.OneOf(
            Gen.String[1, 100].Select(msg => (Exception)new InvalidOperationException(msg)),
            Gen.String[1, 100].Select(msg => (Exception)new ArgumentException(msg)),
            Gen.String[1, 100].Select(msg => (Exception)new NullReferenceException(msg)),
            Gen.String[1, 100].Select(msg => (Exception)new FormatException(msg)),
            Gen.String[1, 100].Select(msg => (Exception)new NotSupportedException(msg)),
            Gen.String[1, 100].Select(msg => (Exception)new ApplicationException(msg))
        );

        exceptionGen.Sample(exception =>
        {
            // Arrange
            var throwingNode = new ThrowingPluginNode(exception);
            var pluginLoader = new TestPluginLoader(throwingNode);
            var pluginHost = new PluginHost(pluginLoader);

            var context = CreateTestExecutionContext();
            var input = new NodeInput
            {
                Data = JsonSerializer.SerializeToElement(new { test = "data" }),
                Configuration = JsonSerializer.SerializeToElement(new { })
            };

            // Act
            var result = pluginHost.ExecuteNodeInIsolationAsync(
                throwingNode,
                input,
                context,
                TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

            // Assert - Exception was caught and node failed gracefully
            Assert.False(result.Success, "Node should have failed");
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains(exception.Message, result.ErrorMessage);
        }, iter: 100);
    }


    /// <summary>
    /// Feature: vyshyvanka, Property 18: Plugin Isolation and Exception Handling
    /// For any sequence of plugin node executions where some throw exceptions,
    /// the system SHALL continue operating and successfully execute non-throwing nodes.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void PluginNodeException_DoesNotAffectOtherNodes()
    {
        // Generate a mix of throwing and non-throwing nodes
        var nodeCountGen = Gen.Int[2, 5];

        nodeCountGen.Sample(nodeCount =>
        {
            // Arrange
            var nodes = new List<INode>();
            var expectedSuccessCount = 0;

            for (int i = 0; i < nodeCount; i++)
            {
                if (i % 2 == 0)
                {
                    // Even indices: throwing nodes
                    nodes.Add(new ThrowingPluginNode(new InvalidOperationException($"Error from node {i}")));
                }
                else
                {
                    // Odd indices: successful nodes
                    nodes.Add(new SuccessfulPluginNode($"node-{i}"));
                    expectedSuccessCount++;
                }
            }

            var pluginLoader = new TestPluginLoader(nodes.ToArray());
            var pluginHost = new PluginHost(pluginLoader);
            var context = CreateTestExecutionContext();
            var input = new NodeInput
            {
                Data = JsonSerializer.SerializeToElement(new { }),
                Configuration = JsonSerializer.SerializeToElement(new { })
            };

            // Act - Execute all nodes
            var results = new List<NodeOutput>();
            foreach (var node in nodes)
            {
                var result = pluginHost.ExecuteNodeInIsolationAsync(
                    node, input, context, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                results.Add(result);
            }

            // Assert - Throwing nodes failed, successful nodes succeeded
            var successfulResults = results.Count(r => r.Success);
            var failedResults = results.Count(r => !r.Success);

            Assert.Equal(expectedSuccessCount, successfulResults);
            Assert.Equal(nodeCount - expectedSuccessCount, failedResults);

            // Verify failed nodes have error messages
            foreach (var result in results.Where(r => !r.Success))
            {
                Assert.NotNull(result.ErrorMessage);
                Assert.NotEmpty(result.ErrorMessage);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 18: Plugin Isolation and Exception Handling
    /// For any plugin node execution that times out, the system SHALL return a 
    /// failed output with timeout error message.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public async Task PluginNodeTimeout_FailsGracefully_ReturnsTimeoutError()
    {
        // Use a fixed short timeout to ensure consistent behavior
        // The node will take much longer than the timeout to make the test reliable
        var timeout = TimeSpan.FromMilliseconds(50);

        // Arrange - Node that takes significantly longer than the timeout
        // Using 2 seconds ensures the timeout will always trigger first
        var slowNode = new SlowPluginNode(TimeSpan.FromSeconds(2));
        var pluginLoader = new TestPluginLoader(slowNode);
        var pluginHost = new PluginHost(pluginLoader);

        var context = CreateTestExecutionContext();
        var input = new NodeInput
        {
            Data = JsonSerializer.SerializeToElement(new { }),
            Configuration = JsonSerializer.SerializeToElement(new { })
        };

        // Act
        var result = await pluginHost.ExecuteNodeInIsolationAsync(
            slowNode, input, context, timeout);

        // Assert - Node timed out gracefully
        Assert.False(result.Success, "Node should have failed due to timeout");
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// Feature: vyshyvanka, Property 18: Plugin Isolation and Exception Handling
    /// For any plugin node that throws an exception during execution, the exception
    /// SHALL NOT propagate to the caller - it must be caught and converted to a failed output.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void PluginNodeException_NeverPropagates()
    {
        // Generate various exception types including severe ones
        var severeExceptionGen = Gen.OneOf(
            Gen.Const((Exception)new OutOfMemoryException("Simulated OOM")),
            Gen.Const((Exception)new StackOverflowException()),
            Gen.Const((Exception)new AccessViolationException("Simulated access violation")),
            Gen.String[1, 50].Select(msg => (Exception)new AggregateException(msg, new Exception("Inner"))),
            Gen.String[1, 50].Select(msg => (Exception)new TaskCanceledException(msg))
        );

        severeExceptionGen.Sample(exception =>
        {
            // Arrange
            var throwingNode = new ThrowingPluginNode(exception);
            var pluginLoader = new TestPluginLoader(throwingNode);
            var pluginHost = new PluginHost(pluginLoader);

            var context = CreateTestExecutionContext();
            var input = new NodeInput
            {
                Data = JsonSerializer.SerializeToElement(new { }),
                Configuration = JsonSerializer.SerializeToElement(new { })
            };

            // Act & Assert - No exception should propagate
            var caughtException = Record.Exception(() =>
            {
                var result = pluginHost.ExecuteNodeInIsolationAsync(
                    throwingNode, input, context, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

                // Verify the result indicates failure
                Assert.False(result.Success);
                Assert.NotNull(result.ErrorMessage);
            });

            // The only exception that should propagate is if the test itself fails
            Assert.Null(caughtException);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 18: Plugin Isolation and Exception Handling
    /// For any successful plugin node execution, the output data SHALL be preserved
    /// and returned correctly.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void SuccessfulPluginNode_ReturnsCorrectOutput()
    {
        // Generate random output data
        var outputDataGen = Gen.Dictionary(Gen.String[1, 20], Gen.String[1, 50])[1, 5];

        outputDataGen.Sample(outputData =>
        {
            // Arrange
            var successNode = new SuccessfulPluginNode("test-node", outputData);
            var pluginLoader = new TestPluginLoader(successNode);
            var pluginHost = new PluginHost(pluginLoader);

            var context = CreateTestExecutionContext();
            var input = new NodeInput
            {
                Data = JsonSerializer.SerializeToElement(new { }),
                Configuration = JsonSerializer.SerializeToElement(new { })
            };

            // Act
            var result = pluginHost.ExecuteNodeInIsolationAsync(
                successNode, input, context, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();

            // Assert
            Assert.True(result.Success);
            Assert.Null(result.ErrorMessage);

            // Verify output data is preserved
            var resultData = JsonSerializer.Deserialize<Dictionary<string, string>>(result.Data.GetRawText());
            Assert.NotNull(resultData);
            foreach (var kvp in outputData)
            {
                Assert.True(resultData.ContainsKey(kvp.Key), $"Missing key: {kvp.Key}");
                Assert.Equal(kvp.Value, resultData[kvp.Key]);
            }
        }, iter: 100);
    }

    #region Test Infrastructure

    private static IExecutionContext CreateTestExecutionContext()
    {
        return new ExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new NullCredentialProvider());
    }

    /// <summary>
    /// A test plugin loader that provides test nodes.
    /// </summary>
    private sealed class TestPluginLoader : IPluginLoader
    {
        private readonly INode[] _nodes;
        private readonly PluginInfo _pluginInfo;

        public TestPluginLoader(params INode[] nodes)
        {
            _nodes = nodes;
            _pluginInfo = new PluginInfo
            {
                Id = "test-plugin",
                Name = "Test Plugin",
                Version = "1.0.0",
                IsLoaded = true,
                NodeTypes = nodes.Select(n => n.GetType()).ToList(),
                LoadedAt = DateTime.UtcNow
            };
        }

        public IEnumerable<PluginInfo> LoadPlugins(string pluginDirectory) => [_pluginInfo];

        public void UnloadPlugin(string pluginId)
        {
        }

        public PluginInfo? GetPlugin(string pluginId) => pluginId == "test-plugin" ? _pluginInfo : null;
        public IEnumerable<PluginInfo> GetLoadedPlugins() => [_pluginInfo];
    }


    /// <summary>
    /// A test node that throws a specified exception during execution.
    /// </summary>
    private sealed class ThrowingPluginNode : INode
    {
        private readonly Exception _exceptionToThrow;

        public ThrowingPluginNode(Exception exceptionToThrow)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public string Id => Guid.NewGuid().ToString();
        public string Type => "throwing-plugin-node";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            throw _exceptionToThrow;
        }
    }

    /// <summary>
    /// A test node that executes successfully and returns specified output.
    /// </summary>
    private sealed class SuccessfulPluginNode : INode
    {
        private readonly string _nodeId;
        private readonly Dictionary<string, string>? _outputData;

        public SuccessfulPluginNode(string nodeId, Dictionary<string, string>? outputData = null)
        {
            _nodeId = nodeId;
            _outputData = outputData;
        }

        public string Id => _nodeId;
        public string Type => "successful-plugin-node";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            var data = _outputData ?? new Dictionary<string, string> { { "result", "success" } };
            return Task.FromResult(new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(data),
                Success = true
            });
        }
    }

    /// <summary>
    /// A test node that takes a specified amount of time to execute.
    /// </summary>
    private sealed class SlowPluginNode : INode
    {
        private readonly TimeSpan _executionTime;

        public SlowPluginNode(TimeSpan executionTime)
        {
            _executionTime = executionTime;
        }

        public string Id => Guid.NewGuid().ToString();
        public string Type => "slow-plugin-node";
        public NodeCategory Category => NodeCategory.Action;

        public async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            await Task.Delay(_executionTime, context.CancellationToken);
            return new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(new { completed = true }),
                Success = true
            };
        }
    }

    /// <summary>
    /// A credential provider that returns no credentials (for testing).
    /// </summary>
    private sealed class NullCredentialProvider : ICredentialProvider
    {
        public Task<IDictionary<string, string>?> GetCredentialAsync(
            Guid credentialId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IDictionary<string, string>?>(null);
        }
    }

    #endregion
}
