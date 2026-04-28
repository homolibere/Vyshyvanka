using System.Collections.Concurrent;
using System.Text.Json;
using CsCheck;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Execution;
using Vyshyvanka.Engine.Expressions;
using WorkflowExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for data flow between nodes.
/// Feature: vyshyvanka, Property 7: Data Flow Between Nodes
/// </summary>
public class DataFlowTests
{
    /// <summary>
    /// Feature: vyshyvanka, Property 7: Data Flow Between Nodes
    /// For any completed node execution, all downstream nodes connected to that node 
    /// SHALL receive the output data as their input, and the data SHALL be unmodified during transmission.
    /// Validates: Requirements 3.3, 5.1
    /// </summary>
    [Fact]
    public void DataFlow_DownstreamNodesReceiveUpstreamOutput_Unmodified()
    {
        GenLinearChainWorkflow.Sample(workflow =>
        {
            // Arrange
            var dataFlowTracker = new DataFlowTracker();
            var registry = new DataFlowTrackingNodeRegistry(dataFlowTracker);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - For each connection, verify data flow
            foreach (var connection in workflow.Connections)
            {
                var sourceOutput = dataFlowTracker.GetOutput(connection.SourceNodeId);
                var targetInput = dataFlowTracker.GetInput(connection.TargetNodeId);

                Assert.NotNull(sourceOutput);
                Assert.NotNull(targetInput);

                // For single upstream connection, target should receive source output directly
                var incomingConnectionsToTarget = workflow.Connections
                    .Count(c => c.TargetNodeId == connection.TargetNodeId);

                if (incomingConnectionsToTarget == 1)
                {
                    // Direct pass-through - data should be identical
                    AssertJsonEqual(sourceOutput.Value, targetInput.Value,
                        $"Data from '{connection.SourceNodeId}' to '{connection.TargetNodeId}' was modified");
                }
                else
                {
                    // Multiple inputs - data should be in merged dictionary
                    var key = $"{connection.SourceNodeId}_{connection.SourcePort}";
                    Assert.True(targetInput.Value.TryGetProperty(key, out var mergedValue),
                        $"Merged input for '{connection.TargetNodeId}' missing key '{key}'");
                    AssertJsonEqual(sourceOutput.Value, mergedValue,
                        $"Merged data from '{connection.SourceNodeId}' was modified");
                }
            }
        }, iter: 100);
    }


    /// <summary>
    /// Feature: vyshyvanka, Property 7: Data Flow Between Nodes
    /// For any DAG workflow with multiple connections, data flows correctly through all paths.
    /// Validates: Requirements 3.3, 5.1
    /// </summary>
    [Fact]
    public void DataFlow_DagWorkflow_AllPathsReceiveCorrectData()
    {
        GenValidDagWorkflow.Sample(workflow =>
        {
            // Arrange
            var dataFlowTracker = new DataFlowTracker();
            var registry = new DataFlowTrackingNodeRegistry(dataFlowTracker);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - All nodes produced output
            foreach (var node in workflow.Nodes)
            {
                Assert.True(dataFlowTracker.GetOutput(node.Id).HasValue,
                    $"Node '{node.Id}' did not produce output");
            }

            // Assert - For each connection, verify data was transmitted
            foreach (var connection in workflow.Connections)
            {
                var sourceOutput = dataFlowTracker.GetOutput(connection.SourceNodeId);
                var targetInput = dataFlowTracker.GetInput(connection.TargetNodeId);

                Assert.NotNull(sourceOutput);

                // Root nodes have no input, skip them
                var incomingConnections = workflow.Connections
                    .Where(c => c.TargetNodeId == connection.TargetNodeId)
                    .ToList();

                if (incomingConnections.Count > 0)
                {
                    Assert.NotNull(targetInput);

                    // Verify source data is present in target input
                    VerifyDataPresent(sourceOutput.Value, targetInput.Value, connection, incomingConnections.Count);
                }
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 7: Data Flow Between Nodes
    /// For any workflow with merge points (multiple inputs to one node), all inputs are preserved.
    /// Validates: Requirements 3.3, 5.1
    /// </summary>
    [Fact]
    public void DataFlow_MergePoints_AllInputsPreserved()
    {
        GenMergeWorkflow.Sample(workflow =>
        {
            // Arrange
            var dataFlowTracker = new DataFlowTracker();
            var registry = new DataFlowTrackingNodeRegistry(dataFlowTracker);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Find merge node (node with multiple incoming connections)
            var mergeNodeId = workflow.Connections
                .GroupBy(c => c.TargetNodeId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .FirstOrDefault();

            if (mergeNodeId is not null)
            {
                var mergeInput = dataFlowTracker.GetInput(mergeNodeId);
                Assert.NotNull(mergeInput);

                // Verify all upstream outputs are present in merge input
                var upstreamConnections = workflow.Connections
                    .Where(c => c.TargetNodeId == mergeNodeId)
                    .ToList();

                foreach (var conn in upstreamConnections)
                {
                    var sourceOutput = dataFlowTracker.GetOutput(conn.SourceNodeId);
                    Assert.NotNull(sourceOutput);

                    var key = $"{conn.SourceNodeId}_{conn.SourcePort}";
                    Assert.True(mergeInput.Value.TryGetProperty(key, out var mergedValue),
                        $"Merge node missing input from '{conn.SourceNodeId}'");

                    AssertJsonEqual(sourceOutput.Value, mergedValue,
                        $"Data from '{conn.SourceNodeId}' was modified during merge");
                }
            }
        }, iter: 100);
    }


    #region Helper Methods

    private static void AssertJsonEqual(JsonElement expected, JsonElement actual, string message)
    {
        var expectedJson = expected.GetRawText();
        var actualJson = actual.GetRawText();
        Assert.Equal(expectedJson, actualJson);
    }

    private static void VerifyDataPresent(
        JsonElement sourceOutput,
        JsonElement targetInput,
        Connection connection,
        int incomingConnectionCount)
    {
        if (incomingConnectionCount == 1)
        {
            // Direct pass-through
            AssertJsonEqual(sourceOutput, targetInput,
                $"Data from '{connection.SourceNodeId}' to '{connection.TargetNodeId}' was modified");
        }
        else
        {
            // Merged input - look for the key
            var key = $"{connection.SourceNodeId}_{connection.SourcePort}";
            Assert.True(targetInput.TryGetProperty(key, out var mergedValue),
                $"Merged input for '{connection.TargetNodeId}' missing key '{key}'");
            AssertJsonEqual(sourceOutput, mergedValue,
                $"Merged data from '{connection.SourceNodeId}' was modified");
        }
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// Tracks input and output data for each node during execution.
    /// </summary>
    private sealed class DataFlowTracker
    {
        private readonly ConcurrentDictionary<string, JsonElement> _inputs = new();
        private readonly ConcurrentDictionary<string, JsonElement> _outputs = new();

        public void RecordInput(string nodeId, JsonElement input)
        {
            _inputs[nodeId] = input;
        }

        public void RecordOutput(string nodeId, JsonElement output)
        {
            _outputs[nodeId] = output;
        }

        public JsonElement? GetInput(string nodeId)
        {
            return _inputs.TryGetValue(nodeId, out var input) ? input : null;
        }

        public JsonElement? GetOutput(string nodeId)
        {
            return _outputs.TryGetValue(nodeId, out var output) ? output : null;
        }
    }

    /// <summary>
    /// A node registry that creates data flow tracking nodes.
    /// </summary>
    private sealed class DataFlowTrackingNodeRegistry : INodeRegistry
    {
        private readonly DataFlowTracker _tracker;

        public DataFlowTrackingNodeRegistry(DataFlowTracker tracker)
        {
            _tracker = tracker;
        }

        public void Register<TNode>() where TNode : INode
        {
        }

        public void Register(Type nodeType)
        {
        }

        public void RegisterFromAssembly(System.Reflection.Assembly assembly)
        {
        }

        public bool Unregister(string nodeType) => false;

        public void UnregisterFromAssembly(System.Reflection.Assembly assembly)
        {
        }

        public INode CreateNode(string nodeType, JsonElement configuration)
        {
            string? workflowNodeId = null;
            if (configuration.ValueKind == JsonValueKind.Object &&
                configuration.TryGetProperty("workflowNodeId", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String)
            {
                workflowNodeId = idElement.GetString();
            }

            return new DataFlowTrackingNode(_tracker, workflowNodeId ?? Guid.NewGuid().ToString());
        }

        public NodeDefinition? GetDefinition(string nodeType) => null;

        public IEnumerable<NodeDefinition> GetAllDefinitions() => [];

        public bool IsRegistered(string nodeType) => true;
    }

    /// <summary>
    /// A test node that records its input and produces unique output for tracking data flow.
    /// </summary>
    private sealed class DataFlowTrackingNode : INode
    {
        private readonly DataFlowTracker _tracker;
        private readonly string _workflowNodeId;

        public DataFlowTrackingNode(DataFlowTracker tracker, string workflowNodeId)
        {
            _tracker = tracker;
            _workflowNodeId = workflowNodeId;
        }

        public string Id => _workflowNodeId;
        public string Type => "data-flow-tracking-node";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            // Record the input this node received
            _tracker.RecordInput(_workflowNodeId, input.Data);

            // Produce unique output that can be traced
            var outputData = new
            {
                sourceNodeId = _workflowNodeId,
                timestamp = DateTime.UtcNow.Ticks,
                uniqueValue = Guid.NewGuid().ToString()
            };

            var output = JsonSerializer.SerializeToElement(outputData);

            // Record the output this node produced
            _tracker.RecordOutput(_workflowNodeId, output);

            return Task.FromResult(new NodeOutput
            {
                Data = output,
                Success = true
            });
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


    #region Generators

    /// <summary>
    /// Generator for a linear chain workflow (A -> B -> C -> ...).
    /// </summary>
    private static readonly Gen<Workflow> GenLinearChainWorkflow =
        from nodeCount in Gen.Int[2, 6]
        select CreateLinearChainWorkflow(nodeCount);

    /// <summary>
    /// Generator for a valid DAG workflow.
    /// </summary>
    private static readonly Gen<Workflow> GenValidDagWorkflow =
        from nodeCount in Gen.Int[2, 8]
        from connectionDensity in Gen.Double[0.2, 0.6]
        select CreateDagWorkflow(nodeCount, connectionDensity);

    /// <summary>
    /// Generator for a workflow with a merge point (multiple nodes feeding into one).
    /// </summary>
    private static readonly Gen<Workflow> GenMergeWorkflow =
        from branchCount in Gen.Int[2, 4]
        select CreateMergeWorkflow(branchCount);

    private static Workflow CreateLinearChainWorkflow(int nodeCount)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        for (int i = 0; i < nodeCount; i++)
        {
            var nodeId = $"node_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "data-flow-tracking-node",
                Name = $"Node {i}",
                Position = new Position(i * 100, 0),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });
        }

        for (int i = 0; i < nodeCount - 1; i++)
        {
            connections.Add(new Connection
            {
                SourceNodeId = $"node_{i}",
                SourcePort = "output",
                TargetNodeId = $"node_{i + 1}",
                TargetPort = "input"
            });
        }

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Linear Chain Workflow",
            Version = 1,
            IsActive = true,
            Nodes = nodes,
            Connections = connections,
            Settings = new WorkflowSettings
            {
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static Workflow CreateDagWorkflow(int nodeCount, double connectionDensity)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();
        var random = new Random();

        for (int i = 0; i < nodeCount; i++)
        {
            var nodeId = $"node_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "data-flow-tracking-node",
                Name = $"Node {i}",
                Position = new Position(i * 100, 0),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });
        }

        // Create connections (only from earlier to later nodes to ensure DAG)
        for (int i = 0; i < nodeCount; i++)
        {
            for (int j = i + 1; j < nodeCount; j++)
            {
                if (random.NextDouble() < connectionDensity)
                {
                    connections.Add(new Connection
                    {
                        SourceNodeId = $"node_{i}",
                        SourcePort = "output",
                        TargetNodeId = $"node_{j}",
                        TargetPort = "input"
                    });
                }
            }
        }

        // Ensure at least one connection
        if (connections.Count == 0 && nodeCount > 1)
        {
            connections.Add(new Connection
            {
                SourceNodeId = "node_0",
                SourcePort = "output",
                TargetNodeId = "node_1",
                TargetPort = "input"
            });
        }

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test DAG Workflow",
            Version = 1,
            IsActive = true,
            Nodes = nodes,
            Connections = connections,
            Settings = new WorkflowSettings
            {
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static Workflow CreateMergeWorkflow(int branchCount)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        // Create branch nodes
        for (int i = 0; i < branchCount; i++)
        {
            var nodeId = $"branch_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "data-flow-tracking-node",
                Name = $"Branch {i}",
                Position = new Position(0, i * 100),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });
        }

        // Create merge node
        nodes.Add(new WorkflowNode
        {
            Id = "merge",
            Type = "data-flow-tracking-node",
            Name = "Merge",
            Position = new Position(200, branchCount * 50),
            Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = "merge" })
        });

        // Connect all branches to merge node
        for (int i = 0; i < branchCount; i++)
        {
            connections.Add(new Connection
            {
                SourceNodeId = $"branch_{i}",
                SourcePort = "output",
                TargetNodeId = "merge",
                TargetPort = "input"
            });
        }

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Merge Workflow",
            Version = 1,
            IsActive = true,
            Nodes = nodes,
            Connections = connections,
            Settings = new WorkflowSettings
            {
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    #endregion
}
