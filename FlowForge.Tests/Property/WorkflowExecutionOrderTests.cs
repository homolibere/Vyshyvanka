using System.Collections.Concurrent;
using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Expressions;
using FlowForge.Engine.Registry;
using WorkflowExecutionContext = FlowForge.Engine.Execution.ExecutionContext;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for workflow execution order.
/// Feature: flowforge, Property 6: Topological Execution Order
/// </summary>
public class WorkflowExecutionOrderTests
{
    /// <summary>
    /// Feature: flowforge, Property 6: Topological Execution Order
    /// For any workflow with connected nodes, the Workflow_Engine SHALL execute nodes 
    /// such that no node executes before all of its upstream dependencies have completed successfully.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void WorkflowExecution_RespectsTopologicalOrder()
    {
        GenValidDagWorkflow.Sample(workflow =>
        {
            // Arrange
            var executionTracker = new ExecutionOrderTracker();
            var registry = new TrackingNodeRegistry(executionTracker);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Verify topological order
            // For each connection, the source node must have completed before the target node started
            foreach (var connection in workflow.Connections)
            {
                var sourceOrder = executionTracker.GetExecutionOrder(connection.SourceNodeId);
                var targetOrder = executionTracker.GetExecutionOrder(connection.TargetNodeId);

                Assert.True(sourceOrder.HasValue,
                    $"Source node '{connection.SourceNodeId}' was not executed");
                Assert.True(targetOrder.HasValue,
                    $"Target node '{connection.TargetNodeId}' was not executed");
                Assert.True(sourceOrder.Value < targetOrder.Value,
                    $"Topological order violated: '{connection.SourceNodeId}' (order={sourceOrder}) " +
                    $"should execute before '{connection.TargetNodeId}' (order={targetOrder})");
            }

            // Verify all nodes were executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.GetExecutionOrder(node.Id).HasValue,
                    $"Node '{node.Id}' was not executed");
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 6: Topological Execution Order
    /// For any linear chain workflow (A -> B -> C -> ...), nodes SHALL execute in exact chain order.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void LinearChainWorkflow_ExecutesInChainOrder()
    {
        GenLinearChainWorkflow.Sample(workflow =>
        {
            // Arrange
            var executionTracker = new ExecutionOrderTracker();
            var registry = new TrackingNodeRegistry(executionTracker);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Verify strict sequential order for linear chain
            for (int i = 0; i < workflow.Nodes.Count - 1; i++)
            {
                var currentNode = workflow.Nodes[i];
                var nextNode = workflow.Nodes[i + 1];

                var currentOrder = executionTracker.GetExecutionOrder(currentNode.Id);
                var nextOrder = executionTracker.GetExecutionOrder(nextNode.Id);

                Assert.True(currentOrder.HasValue && nextOrder.HasValue,
                    $"Nodes '{currentNode.Id}' and '{nextNode.Id}' should both be executed");
                Assert.True(currentOrder.Value < nextOrder.Value,
                    $"Node '{currentNode.Id}' (order={currentOrder}) should execute before '{nextNode.Id}' (order={nextOrder})");
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 6: Topological Execution Order
    /// For any workflow with multiple independent branches, all branches SHALL complete
    /// and no node SHALL execute before its dependencies.
    /// Validates: Requirements 3.2
    /// </summary>
    [Fact]
    public void MultiBranchWorkflow_AllBranchesExecuteWithCorrectOrder()
    {
        GenMultiBranchWorkflow.Sample(workflow =>
        {
            // Arrange
            var executionTracker = new ExecutionOrderTracker();
            var registry = new TrackingNodeRegistry(executionTracker);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - All nodes executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.GetExecutionOrder(node.Id).HasValue,
                    $"Node '{node.Id}' was not executed");
            }

            // Assert - Topological order respected for all connections
            foreach (var connection in workflow.Connections)
            {
                var sourceOrder = executionTracker.GetExecutionOrder(connection.SourceNodeId);
                var targetOrder = executionTracker.GetExecutionOrder(connection.TargetNodeId);

                Assert.True(sourceOrder!.Value < targetOrder!.Value,
                    $"Topological order violated: '{connection.SourceNodeId}' should execute before '{connection.TargetNodeId}'");
            }
        }, iter: 100);
    }

    #region Test Infrastructure

    /// <summary>
    /// Tracks the execution order of nodes using atomic sequence numbers.
    /// </summary>
    private sealed class ExecutionOrderTracker
    {
        private int _counter;
        private readonly ConcurrentDictionary<string, int> _executionOrder = new();

        public int RecordExecution(string nodeId)
        {
            var order = Interlocked.Increment(ref _counter);
            _executionOrder[nodeId] = order;
            return order;
        }

        public int? GetExecutionOrder(string nodeId)
        {
            return _executionOrder.TryGetValue(nodeId, out var order) ? order : null;
        }
    }

    /// <summary>
    /// A node registry that returns tracking nodes for testing execution order.
    /// </summary>
    private sealed class TrackingNodeRegistry : INodeRegistry
    {
        private readonly ExecutionOrderTracker _tracker;

        public TrackingNodeRegistry(ExecutionOrderTracker tracker)
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
            // Extract the workflow node ID from configuration
            string? workflowNodeId = null;
            if (configuration.ValueKind == JsonValueKind.Object &&
                configuration.TryGetProperty("workflowNodeId", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String)
            {
                workflowNodeId = idElement.GetString();
            }

            return new TrackingNode(_tracker, workflowNodeId ?? Guid.NewGuid().ToString());
        }

        public NodeDefinition? GetDefinition(string nodeType) => null;

        public IEnumerable<NodeDefinition> GetAllDefinitions() => [];

        public bool IsRegistered(string nodeType) => true;
    }

    /// <summary>
    /// A test node that records its execution order using the workflow node ID.
    /// </summary>
    private sealed class TrackingNode : INode
    {
        private readonly ExecutionOrderTracker _tracker;
        private readonly string _workflowNodeId;

        public TrackingNode(ExecutionOrderTracker tracker, string workflowNodeId)
        {
            _tracker = tracker;
            _workflowNodeId = workflowNodeId;
        }

        public string Id => _workflowNodeId;
        public string Type => "tracking-node";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            // Small delay to ensure parallel execution can interleave
            Thread.Sleep(1);

            // Record execution order using the workflow node ID
            _tracker.RecordExecution(_workflowNodeId);

            return Task.FromResult(new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(new { executed = true, nodeId = _workflowNodeId }),
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

    /// <summary>Generator for non-empty alphanumeric strings.</summary>
    private static Gen<string> GenNodeId(int index) =>
        Gen.Const($"node_{index}");

    /// <summary>Generator for Position.</summary>
    private static readonly Gen<Position> GenPosition =
        from x in Gen.Double[0, 1000]
        from y in Gen.Double[0, 1000]
        select new Position(x, y);

    /// <summary>
    /// Generator for a valid DAG workflow.
    /// Creates nodes and only adds connections from earlier nodes to later nodes (ensures no cycles).
    /// </summary>
    private static readonly Gen<Workflow> GenValidDagWorkflow =
        from nodeCount in Gen.Int[2, 8]
        from connectionDensity in Gen.Double[0.2, 0.6]
        select CreateDagWorkflow(nodeCount, connectionDensity);

    /// <summary>
    /// Generator for a linear chain workflow (A -> B -> C -> ...).
    /// </summary>
    private static readonly Gen<Workflow> GenLinearChainWorkflow =
        from nodeCount in Gen.Int[2, 6]
        select CreateLinearChainWorkflow(nodeCount);

    /// <summary>
    /// Generator for a workflow with multiple independent branches from a root node.
    /// </summary>
    private static readonly Gen<Workflow> GenMultiBranchWorkflow =
        from branchCount in Gen.Int[2, 4]
        from nodesPerBranch in Gen.Int[1, 3]
        select CreateMultiBranchWorkflow(branchCount, nodesPerBranch);

    private static Workflow CreateDagWorkflow(int nodeCount, double connectionDensity)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();
        var random = new Random();

        // Create nodes with their ID embedded in configuration for tracking
        for (int i = 0; i < nodeCount; i++)
        {
            var nodeId = $"node_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "tracking-node",
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

        // Ensure at least one connection if we have multiple nodes
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

    private static Workflow CreateLinearChainWorkflow(int nodeCount)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        // Create nodes with their ID embedded in configuration for tracking
        for (int i = 0; i < nodeCount; i++)
        {
            var nodeId = $"node_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "tracking-node",
                Name = $"Node {i}",
                Position = new Position(i * 100, 0),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });
        }

        // Create linear chain connections
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

    private static Workflow CreateMultiBranchWorkflow(int branchCount, int nodesPerBranch)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        // Create root node with ID embedded in configuration
        nodes.Add(new WorkflowNode
        {
            Id = "root",
            Type = "tracking-node",
            Name = "Root",
            Position = new Position(0, 0),
            Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = "root" })
        });

        // Create branches
        for (int branch = 0; branch < branchCount; branch++)
        {
            var previousNodeId = "root";

            for (int nodeInBranch = 0; nodeInBranch < nodesPerBranch; nodeInBranch++)
            {
                var nodeId = $"branch_{branch}_node_{nodeInBranch}";

                nodes.Add(new WorkflowNode
                {
                    Id = nodeId,
                    Type = "tracking-node",
                    Name = $"Branch {branch} Node {nodeInBranch}",
                    Position = new Position((nodeInBranch + 1) * 100, branch * 100),
                    Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
                });

                connections.Add(new Connection
                {
                    SourceNodeId = previousNodeId,
                    SourcePort = "output",
                    TargetNodeId = nodeId,
                    TargetPort = "input"
                });

                previousNodeId = nodeId;
            }
        }

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Multi-Branch Workflow",
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
