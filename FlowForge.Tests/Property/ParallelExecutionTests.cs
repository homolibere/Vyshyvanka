using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Expressions;
using WorkflowExecutionContext = FlowForge.Engine.Execution.ExecutionContext;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for parallel branch execution.
/// Feature: flowforge, Property 8: Parallel Branch Execution
/// </summary>
public class ParallelExecutionTests
{
    private const int NodeDelayMs = 100; // Increased delay to make parallelism more obvious
    private const int SystemOverheadMs = 150; // Allow for task scheduling and system overhead

    /// <summary>
    /// Feature: flowforge, Property 8: Parallel Branch Execution
    /// For any workflow containing independent branches (nodes with no shared dependencies), 
    /// the Workflow_Engine SHALL execute those branches concurrently, and the execution time 
    /// SHALL be less than the sum of individual branch execution times.
    /// Validates: Requirements 3.5
    /// </summary>
    [Fact]
    public void ParallelBranches_ExecuteConcurrently_TotalTimeLessThanSum()
    {
        GenIndependentBranchWorkflow.Sample(workflow =>
        {
            // Arrange
            var executionTracker = new ParallelExecutionTracker();
            var registry = new DelayedNodeRegistry(executionTracker, NodeDelayMs);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // Calculate expected sequential time (sum of all node delays)
            var totalNodes = workflow.Nodes.Count;
            var expectedSequentialTimeMs = totalNodes * NodeDelayMs;

            // For parallel execution, we expect time ≈ depth of graph × delay + overhead
            // Independent branches from root: depth = 2 (root + branches)
            var expectedParallelTimeMs = 2 * NodeDelayMs;
            var maxAllowedTime = expectedParallelTimeMs + SystemOverheadMs;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();
            stopwatch.Stop();

            var actualTimeMs = stopwatch.ElapsedMilliseconds;

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - All nodes were executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.WasExecuted(node.Id),
                    $"Node '{node.Id}' was not executed");
            }

            // Assert - Parallel execution: total time should be significantly less than sequential time
            Assert.True(actualTimeMs < expectedSequentialTimeMs,
                $"No parallelism detected. Actual time: {actualTimeMs}ms should be less than sequential: {expectedSequentialTimeMs}ms");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 8: Parallel Branch Execution
    /// For any workflow with multiple independent root nodes (no connections between them),
    /// all roots SHALL execute in parallel.
    /// Validates: Requirements 3.5
    /// </summary>
    [Fact]
    public void MultipleRootNodes_ExecuteInParallel()
    {
        GenMultipleRootWorkflow.Sample(workflow =>
        {
            // Arrange
            var executionTracker = new ParallelExecutionTracker();
            var registry = new DelayedNodeRegistry(executionTracker, NodeDelayMs);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            var rootNodeCount = workflow.Nodes.Count;
            var expectedSequentialTimeMs = rootNodeCount * NodeDelayMs;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();
            stopwatch.Stop();

            var actualTimeMs = stopwatch.ElapsedMilliseconds;

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - All nodes were executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.WasExecuted(node.Id),
                    $"Node '{node.Id}' was not executed");
            }

            // Assert - Parallel execution of root nodes
            // With N independent roots each taking D ms, parallel time should be ~D + overhead, not N*D
            // We verify parallelism by checking actual time is significantly less than sequential time
            // Allow generous overhead for thread pool scheduling, GC, CI environments, etc.
            // For 6+ nodes at 100ms each, sequential is 600ms+, parallel should be ~100ms + overhead
            // Use 80% of sequential time as threshold to account for system variability
            var parallelThreshold = (long)(expectedSequentialTimeMs * 0.8);
            Assert.True(actualTimeMs < parallelThreshold,
                $"Root nodes not executing in parallel. Actual: {actualTimeMs}ms should be less than threshold: {parallelThreshold}ms (80% of sequential: {expectedSequentialTimeMs}ms)");
        }, iter: 100);
    }


    /// <summary>
    /// Feature: flowforge, Property 8: Parallel Branch Execution
    /// For any workflow where branches converge to a merge node, the branches SHALL execute
    /// in parallel before the merge node executes.
    /// Validates: Requirements 3.5
    /// </summary>
    [Fact]
    public void ConvergingBranches_ExecuteInParallel_BeforeMerge()
    {
        GenConvergingBranchWorkflow.Sample(workflow =>
        {
            // Arrange
            var executionTracker = new ParallelExecutionTracker();
            var registry = new DelayedNodeRegistry(executionTracker, NodeDelayMs);
            var expressionEvaluator = new ExpressionEvaluator();
            var engine = new WorkflowEngine(registry, expressionEvaluator);

            var context = new WorkflowExecutionContext(
                Guid.NewGuid(),
                workflow.Id,
                new NullCredentialProvider());

            // For converging workflow: branches (level 1) + merge (level 2) = 2 levels
            // Sequential time = total nodes × delay
            var totalNodes = workflow.Nodes.Count;
            var expectedSequentialTimeMs = totalNodes * NodeDelayMs;

            // Act
            var stopwatch = Stopwatch.StartNew();
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();
            stopwatch.Stop();

            var actualTimeMs = stopwatch.ElapsedMilliseconds;

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - Merge node executed after all branch nodes
            var mergeNode = workflow.Nodes.First(n => n.Id == "merge");
            var mergeStartTime = executionTracker.GetStartTime(mergeNode.Id);
            Assert.NotNull(mergeStartTime);

            foreach (var node in workflow.Nodes.Where(n => n.Id != "merge"))
            {
                var nodeEndTime = executionTracker.GetEndTime(node.Id);
                Assert.NotNull(nodeEndTime);
                Assert.True(nodeEndTime.Value <= mergeStartTime.Value,
                    $"Branch node '{node.Id}' should complete before merge node starts");
            }

            // Assert - Parallel execution: actual time should be less than sequential time
            // This proves parallelism is happening
            // Use 80% of sequential time as threshold to account for system variability
            var parallelThreshold = (long)(expectedSequentialTimeMs * 0.8);
            Assert.True(actualTimeMs < parallelThreshold,
                $"No parallelism detected. Actual: {actualTimeMs}ms should be less than threshold: {parallelThreshold}ms (80% of sequential: {expectedSequentialTimeMs}ms, {totalNodes} nodes)");
        }, iter: 100);
    }

    #region Test Infrastructure

    /// <summary>
    /// Tracks execution timing for parallel execution verification.
    /// </summary>
    private sealed class ParallelExecutionTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _startTimes = new();
        private readonly ConcurrentDictionary<string, DateTime> _endTimes = new();

        public void RecordStart(string nodeId)
        {
            _startTimes[nodeId] = DateTime.UtcNow;
        }

        public void RecordEnd(string nodeId)
        {
            _endTimes[nodeId] = DateTime.UtcNow;
        }

        public bool WasExecuted(string nodeId) => _endTimes.ContainsKey(nodeId);

        public DateTime? GetStartTime(string nodeId) =>
            _startTimes.TryGetValue(nodeId, out var time) ? time : null;

        public DateTime? GetEndTime(string nodeId) =>
            _endTimes.TryGetValue(nodeId, out var time) ? time : null;
    }

    /// <summary>
    /// A node registry that creates delayed nodes for testing parallel execution.
    /// </summary>
    private sealed class DelayedNodeRegistry : INodeRegistry
    {
        private readonly ParallelExecutionTracker _tracker;
        private readonly int _delayMs;

        public DelayedNodeRegistry(ParallelExecutionTracker tracker, int delayMs)
        {
            _tracker = tracker;
            _delayMs = delayMs;
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

            return new DelayedNode(_tracker, workflowNodeId ?? Guid.NewGuid().ToString(), _delayMs);
        }

        public NodeDefinition? GetDefinition(string nodeType) => null;

        public IEnumerable<NodeDefinition> GetAllDefinitions() => [];

        public bool IsRegistered(string nodeType) => true;
    }

    /// <summary>
    /// A test node that introduces a configurable delay to measure parallel execution.
    /// </summary>
    private sealed class DelayedNode : INode
    {
        private readonly ParallelExecutionTracker _tracker;
        private readonly string _workflowNodeId;
        private readonly int _delayMs;

        public DelayedNode(ParallelExecutionTracker tracker, string workflowNodeId, int delayMs)
        {
            _tracker = tracker;
            _workflowNodeId = workflowNodeId;
            _delayMs = delayMs;
        }

        public string Id => _workflowNodeId;
        public string Type => "delayed-node";
        public NodeCategory Category => NodeCategory.Action;

        public async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            _tracker.RecordStart(_workflowNodeId);

            // Introduce delay to measure parallel execution
            await Task.Delay(_delayMs);

            _tracker.RecordEnd(_workflowNodeId);

            return new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(new { nodeId = _workflowNodeId, executed = true }),
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


    #region Generators

    /// <summary>
    /// Generator for a workflow with multiple independent branches from a single root.
    /// Structure: root -> [branch1_node1, branch2_node1, ...] (independent branches)
    /// </summary>
    private static readonly Gen<Workflow> GenIndependentBranchWorkflow =
        from branchCount in Gen.Int[2, 4]
        select CreateIndependentBranchWorkflow(branchCount);

    /// <summary>
    /// Generator for a workflow with multiple independent root nodes (no connections).
    /// Uses 6-8 nodes to ensure parallelism benefit outweighs system overhead.
    /// With 6+ nodes at 100ms each, sequential time is 600ms+ while parallel is ~100ms + overhead.
    /// </summary>
    private static readonly Gen<Workflow> GenMultipleRootWorkflow =
        from rootCount in Gen.Int[6, 8]
        select CreateMultipleRootWorkflow(rootCount);

    /// <summary>
    /// Generator for a workflow where multiple branches converge to a single merge node.
    /// Structure: [branch1, branch2, ...] -> merge
    /// </summary>
    private static readonly Gen<Workflow> GenConvergingBranchWorkflow =
        from branchCount in Gen.Int[2, 4]
        select CreateConvergingBranchWorkflow(branchCount);

    private static Workflow CreateIndependentBranchWorkflow(int branchCount)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        // Create root node
        nodes.Add(new WorkflowNode
        {
            Id = "root",
            Type = "delayed-node",
            Name = "Root",
            Position = new Position(0, 0),
            Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = "root" })
        });

        // Create independent branch nodes (each connected only to root)
        for (int i = 0; i < branchCount; i++)
        {
            var nodeId = $"branch_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "delayed-node",
                Name = $"Branch {i}",
                Position = new Position(100, i * 100),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });

            connections.Add(new Connection
            {
                SourceNodeId = "root",
                SourcePort = "output",
                TargetNodeId = nodeId,
                TargetPort = "input"
            });
        }

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Independent Branch Workflow",
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

    private static Workflow CreateMultipleRootWorkflow(int rootCount)
    {
        var nodes = new List<WorkflowNode>();

        // Create multiple independent root nodes (no connections between them)
        for (int i = 0; i < rootCount; i++)
        {
            var nodeId = $"root_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "delayed-node",
                Name = $"Root {i}",
                Position = new Position(0, i * 100),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });
        }

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Multiple Root Workflow",
            Version = 1,
            IsActive = true,
            Nodes = nodes,
            Connections = [], // No connections - all nodes are independent
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

    private static Workflow CreateConvergingBranchWorkflow(int branchCount)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        // Create branch nodes (independent roots)
        for (int i = 0; i < branchCount; i++)
        {
            var nodeId = $"branch_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "delayed-node",
                Name = $"Branch {i}",
                Position = new Position(0, i * 100),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });

            // Connect each branch to the merge node
            connections.Add(new Connection
            {
                SourceNodeId = nodeId,
                SourcePort = "output",
                TargetNodeId = "merge",
                TargetPort = "input"
            });
        }

        // Create merge node
        nodes.Add(new WorkflowNode
        {
            Id = "merge",
            Type = "delayed-node",
            Name = "Merge",
            Position = new Position(200, branchCount * 50),
            Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = "merge" })
        });

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Test Converging Branch Workflow",
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
