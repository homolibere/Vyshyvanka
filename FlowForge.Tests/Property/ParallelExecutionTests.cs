using System.Collections.Concurrent;
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
    private const int NodeDelayMs = 50;

    /// <summary>
    /// Feature: flowforge, Property 8: Parallel Branch Execution
    /// For any workflow containing independent branches (nodes with no shared dependencies), 
    /// the Workflow_Engine SHALL execute those branches concurrently.
    /// Validates: Requirements 3.5
    /// </summary>
    [Fact]
    public void ParallelBranches_ExecuteConcurrently()
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

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - All nodes were executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.WasExecuted(node.Id),
                    $"Node '{node.Id}' was not executed");
            }

            // Assert - Parallel execution detected: at least 2 nodes were executing simultaneously
            // The workflow has 1 root + N branches, branches should execute in parallel
            var branchCount = workflow.Nodes.Count - 1; // Exclude root
            if (branchCount >= 2)
            {
                Assert.True(executionTracker.MaxConcurrentExecutions >= 2,
                    $"No parallelism detected. Max concurrent executions: {executionTracker.MaxConcurrentExecutions}, expected at least 2 for {branchCount} branches");
            }
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

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - All nodes were executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.WasExecuted(node.Id),
                    $"Node '{node.Id}' was not executed");
            }

            // Assert - Parallel execution: multiple roots executing simultaneously
            var rootCount = workflow.Nodes.Count;
            Assert.True(executionTracker.MaxConcurrentExecutions >= 2,
                $"Root nodes not executing in parallel. Max concurrent: {executionTracker.MaxConcurrentExecutions}, expected at least 2 for {rootCount} independent roots");
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

            // Act
            var result = engine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();

            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");

            // Assert - All nodes were executed
            foreach (var node in workflow.Nodes)
            {
                Assert.True(executionTracker.WasExecuted(node.Id),
                    $"Node '{node.Id}' was not executed");
            }

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

            // Assert - Parallel execution among branches
            var branchCount = workflow.Nodes.Count - 1; // Exclude merge
            if (branchCount >= 2)
            {
                Assert.True(executionTracker.MaxConcurrentExecutions >= 2,
                    $"Branches not executing in parallel. Max concurrent: {executionTracker.MaxConcurrentExecutions}, expected at least 2 for {branchCount} branches");
            }
        }, iter: 100);
    }

    #region Test Infrastructure

    /// <summary>
    /// Tracks execution for parallel execution verification.
    /// Uses concurrent execution count instead of timing to detect parallelism.
    /// </summary>
    private sealed class ParallelExecutionTracker
    {
        private readonly ConcurrentDictionary<string, DateTime> _startTimes = new();
        private readonly ConcurrentDictionary<string, DateTime> _endTimes = new();
        private int _currentConcurrentExecutions;
        private int _maxConcurrentExecutions;

        /// <summary>
        /// Gets the maximum number of nodes that were executing simultaneously.
        /// </summary>
        public int MaxConcurrentExecutions => _maxConcurrentExecutions;

        public void RecordStart(string nodeId)
        {
            _startTimes[nodeId] = DateTime.UtcNow;
            
            // Increment concurrent count and update max
            var current = Interlocked.Increment(ref _currentConcurrentExecutions);
            
            // Update max using compare-and-swap pattern
            int initialMax;
            do
            {
                initialMax = _maxConcurrentExecutions;
                if (current <= initialMax) break;
            } while (Interlocked.CompareExchange(ref _maxConcurrentExecutions, current, initialMax) != initialMax);
        }

        public void RecordEnd(string nodeId)
        {
            _endTimes[nodeId] = DateTime.UtcNow;
            Interlocked.Decrement(ref _currentConcurrentExecutions);
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

        public void Register<TNode>() where TNode : INode { }
        public void Register(Type nodeType) { }
        public void RegisterFromAssembly(System.Reflection.Assembly assembly) { }
        public bool Unregister(string nodeType) => false;
        public void UnregisterFromAssembly(System.Reflection.Assembly assembly) { }

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
    /// A test node that introduces a configurable delay to detect parallel execution.
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

            // Delay to allow concurrent execution detection
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
    /// Structure: root -> [branch1, branch2, ...] (independent branches)
    /// </summary>
    private static readonly Gen<Workflow> GenIndependentBranchWorkflow =
        from branchCount in Gen.Int[2, 4]
        select CreateIndependentBranchWorkflow(branchCount);

    /// <summary>
    /// Generator for a workflow with multiple independent root nodes (no connections).
    /// </summary>
    private static readonly Gen<Workflow> GenMultipleRootWorkflow =
        from rootCount in Gen.Int[3, 5]
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
        for (var i = 0; i < branchCount; i++)
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
            Settings = new WorkflowSettings { ErrorHandling = ErrorHandlingMode.StopOnFirstError },
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
        for (var i = 0; i < rootCount; i++)
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
            Connections = [],
            Settings = new WorkflowSettings { ErrorHandling = ErrorHandlingMode.StopOnFirstError },
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
        for (var i = 0; i < branchCount; i++)
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
            Settings = new WorkflowSettings { ErrorHandling = ErrorHandlingMode.StopOnFirstError },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    #endregion
}
