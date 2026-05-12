using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Expressions;

namespace Vyshyvanka.Engine.Execution;

/// <summary>
/// Default implementation of the workflow execution engine.
/// Supports topological execution order, data flow between nodes, and parallel branch execution.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly IPluginHost? _pluginHost;
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeExecutions = new();

    /// <summary>
    /// Cached empty JSON object element to avoid repeated allocations.
    /// </summary>
    private static readonly JsonElement EmptyObjectElement = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>
    /// Default max degree of parallelism when not specified (uses processor count * 2).
    /// </summary>
    private static readonly int DefaultMaxParallelism = Environment.ProcessorCount * 2;

    /// <summary>
    /// Default timeout for plugin node execution.
    /// </summary>
    private static readonly TimeSpan DefaultPluginTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a new workflow engine instance.
    /// </summary>
    public WorkflowEngine(
        INodeRegistry nodeRegistry,
        IExpressionEvaluator expressionEvaluator,
        IPluginHost? pluginHost = null,
        ILogger<WorkflowEngine>? logger = null)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
        _pluginHost = pluginHost;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<WorkflowEngine>();
    }

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteAsync(
        Workflow workflow,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        var startTime = DateTime.UtcNow;
        var nodeResults = new ConcurrentBag<NodeExecutionResult>();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeExecutions[context.ExecutionId] = cts;

        try
        {
            _logger.LogInformation(
                "Starting execution {ExecutionId} for workflow {WorkflowId} ({NodeCount} nodes)",
                context.ExecutionId, workflow.Id, workflow.Nodes.Count);

            var executionLevels = BuildExecutionLevels(workflow);
            var maxParallelism = GetEffectiveMaxParallelism(workflow.Settings.MaxDegreeOfParallelism);

            var earlyTermination = await ExecuteLevelsAsync(
                workflow, executionLevels, context, nodeResults, maxParallelism, startTime, cts.Token);

            if (earlyTermination is not null)
                return earlyTermination;

            var allSuccess = nodeResults.All(r => r.Success);

            _logger.LogInformation(
                "Execution {ExecutionId} completed: success={Success}, duration={Duration}ms",
                context.ExecutionId, allSuccess, (DateTime.UtcNow - startTime).TotalMilliseconds);

            return BuildResult(context.ExecutionId, nodeResults, startTime, workflow,
                success: allSuccess,
                errorMessage: allSuccess ? null : nodeResults.FirstOrDefault(r => !r.Success)?.ErrorMessage);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Execution {ExecutionId} was cancelled", context.ExecutionId);

            return BuildResult(context.ExecutionId, nodeResults, startTime, workflow,
                success: false, errorMessage: "Execution was cancelled");
        }
        finally
        {
            _activeExecutions.TryRemove(context.ExecutionId, out _);
        }
    }

    /// <summary>
    /// Iterates through execution levels, running nodes and processing loop iterations.
    /// Returns an <see cref="ExecutionResult"/> for early termination (StopOnFirstError), or null if all levels complete.
    /// </summary>
    private async Task<ExecutionResult?> ExecuteLevelsAsync(
        Workflow workflow,
        List<List<string>> executionLevels,
        IExecutionContext context,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        int maxParallelism,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        foreach (var level in executionLevels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var levelResults = await ExecuteLevelWithThrottlingAsync(
                workflow, level, context, nodeResults, maxParallelism, cancellationToken);

            var earlyFailure = CheckStopOnFirstError(
                workflow, levelResults, context.ExecutionId, nodeResults, startTime);
            if (earlyFailure is not null)
                return earlyFailure;

            var loopFailure = await ProcessLoopNodesInLevelAsync(
                workflow, level, context, nodeResults, maxParallelism, startTime, cancellationToken);
            if (loopFailure is not null)
                return loopFailure;
        }

        return null;
    }

    /// <summary>
    /// Checks if StopOnFirstError is enabled and a node in the level failed.
    /// Returns an <see cref="ExecutionResult"/> if execution should stop, null otherwise.
    /// </summary>
    private static ExecutionResult? CheckStopOnFirstError(
        Workflow workflow,
        NodeExecutionResult[] levelResults,
        Guid executionId,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        DateTime startTime)
    {
        if (workflow.Settings.ErrorHandling != ErrorHandlingMode.StopOnFirstError)
            return null;

        var failedResult = levelResults.FirstOrDefault(r => !r.Success);
        if (failedResult is null)
            return null;

        return BuildResult(executionId, nodeResults, startTime, workflow,
            success: false, errorMessage: failedResult.ErrorMessage);
    }

    /// <summary>
    /// Processes loop nodes within a level: detects __loopItems output and executes
    /// the downstream subgraph for each item.
    /// </summary>
    private async Task<ExecutionResult?> ProcessLoopNodesInLevelAsync(
        Workflow workflow,
        List<string> level,
        IExecutionContext context,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        int maxParallelism,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        foreach (var loopNodeId in level)
        {
            var loopItems = GetLoopItems(context, loopNodeId);
            if (loopItems is null || loopItems.Count == 0)
                continue;

            var itemSubgraph = GetItemPortSubgraph(workflow, loopNodeId);
            if (itemSubgraph.Count == 0)
                continue;

            var subgraphLevels = BuildSubgraphExecutionLevels(workflow, itemSubgraph, loopNodeId);

            var loopFailure = await ExecuteLoopSubgraphAsync(
                workflow, loopNodeId, loopItems, subgraphLevels, context,
                nodeResults, maxParallelism, startTime, cancellationToken);
            if (loopFailure is not null)
                return loopFailure;

            SetLoopDoneOutput(context, loopNodeId, loopItems.Count);
            MarkSubgraphAsProcessed(context, itemSubgraph);
        }

        return null;
    }

    /// <summary>
    /// Extracts the __loopItems array from a node's output, if present.
    /// </summary>
    private static List<JsonElement>? GetLoopItems(IExecutionContext context, string loopNodeId)
    {
        var defaultOutput = context.NodeOutputs.Get(loopNodeId);
        if (!defaultOutput.HasValue ||
            defaultOutput.Value.ValueKind != JsonValueKind.Object ||
            !defaultOutput.Value.TryGetProperty("__loopItems", out var loopItemsElement) ||
            loopItemsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return loopItemsElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>
    /// Executes the loop subgraph for each item, setting per-iteration context.
    /// Returns an <see cref="ExecutionResult"/> for early termination, or null if all iterations complete.
    /// </summary>
    private async Task<ExecutionResult?> ExecuteLoopSubgraphAsync(
        Workflow workflow,
        string loopNodeId,
        List<JsonElement> items,
        List<List<string>> subgraphLevels,
        IExecutionContext context,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        int maxParallelism,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SetLoopIterationContext(context, loopNodeId, items, i);

            foreach (var subLevel in subgraphLevels)
            {
                var subResults = await ExecuteLevelWithThrottlingAsync(
                    workflow, subLevel, context, nodeResults, maxParallelism, cancellationToken);

                var earlyFailure = CheckStopOnFirstError(
                    workflow, subResults, context.ExecutionId, nodeResults, startTime);
                if (earlyFailure is not null)
                    return earlyFailure;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets the execution context for a single loop iteration.
    /// </summary>
    private static void SetLoopIterationContext(
        IExecutionContext context, string loopNodeId, List<JsonElement> items, int index)
    {
        var itemData = JsonSerializer.SerializeToElement(new
        {
            index,
            item = JsonSerializer.Deserialize<object>(items[index].GetRawText()),
            isFirst = index == 0,
            isLast = index == items.Count - 1,
            totalCount = items.Count,
            outputPort = "item"
        });
        context.NodeOutputs.Set(loopNodeId, "item", itemData);
        context.NodeOutputs.Set(loopNodeId, itemData);
    }

    /// <summary>
    /// Sets the "done" port output after all loop iterations complete.
    /// </summary>
    private static void SetLoopDoneOutput(IExecutionContext context, string loopNodeId, int totalCount)
    {
        context.NodeOutputs.Set(loopNodeId, "done", JsonSerializer.SerializeToElement(new
        {
            totalCount,
            processedCount = totalCount,
            isComplete = true
        }));
    }

    /// <summary>
    /// Marks subgraph nodes as processed so they are skipped in later levels.
    /// </summary>
    private static void MarkSubgraphAsProcessed(IExecutionContext context, HashSet<string> subgraphNodes)
    {
        foreach (var subNodeId in subgraphNodes)
        {
            context.Variables[$"__loop_processed_{subNodeId}"] = true;
        }
    }

    /// <inheritdoc />
    public async Task<ExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var startTime = DateTime.UtcNow;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var evaluatedConfig = EvaluateConfiguration(node.Configuration, context, node.Id);
            var nodeInstance = _nodeRegistry.CreateNode(node.Type, evaluatedConfig);

            var input = new NodeInput
            {
                Data = default,
                Configuration = evaluatedConfig,
                CredentialId = node.CredentialId
            };

            var output = await ExecuteNodeInstanceAsync(nodeInstance, input, context, DefaultPluginTimeout);

            if (output.Success)
            {
                context.NodeOutputs.Set(node.Id, output.Data);
            }

            return new ExecutionResult
            {
                ExecutionId = context.ExecutionId,
                Success = output.Success,
                OutputData = output.Data,
                ErrorMessage = output.ErrorMessage,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (ExpressionEvaluationException ex)
        {
            return new ExecutionResult
            {
                ExecutionId = context.ExecutionId,
                Success = false,
                ErrorMessage = $"Expression evaluation failed in node '{node.Id}': {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                ExecutionId = context.ExecutionId,
                Success = false,
                ErrorMessage = "Node execution was cancelled",
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                ExecutionId = context.ExecutionId,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <inheritdoc />
    public Task CancelExecutionAsync(Guid executionId)
    {
        if (_activeExecutions.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
        }

        return Task.CompletedTask;
    }

    private static int GetEffectiveMaxParallelism(int configured)
    {
        return configured > 0 ? configured : DefaultMaxParallelism;
    }

    /// <summary>
    /// Builds an ExecutionResult with OutputData from the terminal node(s) of the workflow.
    /// Terminal nodes are those with no outgoing connections — the final step(s) in the graph.
    /// Falls back to the last node in execution order if no terminal node output is found.
    /// </summary>
    private static ExecutionResult BuildResult(
        Guid executionId,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        DateTime startTime,
        Workflow workflow,
        bool success,
        string? errorMessage = null)
    {
        var resultList = nodeResults.ToList();

        // Find terminal nodes: nodes that are not the source of any connection
        var sourceNodeIds = workflow.Connections
            .Select(c => c.SourceNodeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var terminalNodeIds = workflow.Nodes
            .Where(n => !sourceNodeIds.Contains(n.Id))
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Get output from the last successful terminal node
        object? outputData = null;
        if (terminalNodeIds.Count > 0)
        {
            outputData = resultList
                .Where(r => r.Success && terminalNodeIds.Contains(r.NodeId) && r.OutputData is not null)
                .LastOrDefault()?.OutputData;
        }

        // Fallback: last successful node result with non-null output
        outputData ??= resultList.LastOrDefault(r => r.Success && r.OutputData is not null)?.OutputData;

        return new ExecutionResult
        {
            ExecutionId = executionId,
            Success = success,
            OutputData = outputData,
            ErrorMessage = errorMessage,
            Duration = DateTime.UtcNow - startTime,
            NodeResults = resultList
        };
    }

    private async Task<NodeExecutionResult[]> ExecuteLevelWithThrottlingAsync(
        Workflow workflow,
        List<string> level,
        IExecutionContext context,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        int maxParallelism,
        CancellationToken cancellationToken)
    {
        using var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);

        var tasks = level.Select(async nodeId =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteNodeInWorkflowAsync(workflow, nodeId, context, nodeResults, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        return await Task.WhenAll(tasks);
    }

    private async Task<NodeExecutionResult> ExecuteNodeInWorkflowAsync(
        Workflow workflow,
        string nodeId,
        IExecutionContext context,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        CancellationToken cancellationToken)
    {
        var node = workflow.Nodes.First(n => n.Id == nodeId);
        var startTime = DateTime.UtcNow;
        var timeout = workflow.Settings.Timeout ?? DefaultPluginTimeout;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if this node is on an inactive branch (all incoming connections
            // come from ports that were not activated by their source nodes).
            if (IsOnInactiveBranch(workflow, nodeId, context))
            {
                // Don't add a result — this node was skipped, not executed.
                return new NodeExecutionResult
                {
                    NodeId = nodeId,
                    Success = true,
                    InputData = EmptyObjectElement,
                    Duration = TimeSpan.Zero
                };
            }

            // Skip nodes that were already executed as part of a loop iteration
            if (context.Variables.ContainsKey($"__loop_processed_{nodeId}"))
            {
                return new NodeExecutionResult
                {
                    NodeId = nodeId,
                    Success = true,
                    InputData = EmptyObjectElement,
                    Duration = TimeSpan.Zero
                };
            }

            // Gather input data first so expressions in config can reference it via "input."
            var inputData = GatherInputData(workflow, nodeId, context);

            // If this node is marked as "gather input only", record input and stop without executing
            if (context.Variables.TryGetValue("__gatherInputOnlyNodeId", out var stopNodeObj) &&
                stopNodeObj is string stopNodeId &&
                string.Equals(stopNodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            {
                var inputOnlyResult = new NodeExecutionResult
                {
                    NodeId = nodeId,
                    Success = true,
                    InputData = inputData,
                    Duration = TimeSpan.Zero
                };
                nodeResults.Add(inputOnlyResult);
                return inputOnlyResult;
            }

            context.Variables["__currentInput"] = inputData;

            var evaluatedConfig = EvaluateConfiguration(node.Configuration, context, nodeId);
            var nodeInstance = _nodeRegistry.CreateNode(node.Type, evaluatedConfig);

            // Clean up temporary variable
            context.Variables.Remove("__currentInput");

            var input = new NodeInput
            {
                Data = inputData,
                Configuration = evaluatedConfig,
                CredentialId = node.CredentialId
            };

            _logger.LogInformation(
                "Executing node {NodeId} (type={NodeType}) in execution {ExecutionId}",
                nodeId, node.Type, context.ExecutionId);

            var output = await ExecuteNodeInstanceAsync(nodeInstance, input, context, timeout);

            if (output.Success)
            {
                StoreNodeOutput(context, node.Id, output);
                _logger.LogDebug(
                    "Node {NodeId} completed successfully in {Duration}ms",
                    nodeId, (DateTime.UtcNow - startTime).TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Node {NodeId} failed: {Error}",
                    nodeId, output.ErrorMessage);
            }

            var result = new NodeExecutionResult
            {
                NodeId = nodeId,
                Success = output.Success,
                InputData = inputData,
                OutputData = output.Data,
                ErrorMessage = output.ErrorMessage,
                Duration = DateTime.UtcNow - startTime
            };

            nodeResults.Add(result);
            return result;
        }
        catch (ExpressionEvaluationException ex)
        {
            var result = new NodeExecutionResult
            {
                NodeId = nodeId,
                Success = false,
                ErrorMessage = $"Expression evaluation failed in node '{nodeId}': {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
            nodeResults.Add(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new NodeExecutionResult
            {
                NodeId = nodeId,
                Success = false,
                ErrorMessage = "Node execution was cancelled",
                Duration = DateTime.UtcNow - startTime
            };
            nodeResults.Add(result);
            return result;
        }
        catch (Exception ex)
        {
            var result = new NodeExecutionResult
            {
                NodeId = nodeId,
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
            nodeResults.Add(result);
            return result;
        }
    }

    /// <summary>
    /// Executes a node instance, routing plugin nodes through IPluginHost for isolation.
    /// </summary>
    private async Task<NodeOutput> ExecuteNodeInstanceAsync(
        INode nodeInstance,
        NodeInput input,
        IExecutionContext context,
        TimeSpan timeout)
    {
        if (_pluginHost is not null && _pluginHost.IsPluginNode(nodeInstance.Type))
        {
            return await _pluginHost.ExecuteNodeInIsolationAsync(nodeInstance, input, context, timeout);
        }

        return await nodeInstance.ExecuteAsync(input, context);
    }

    private static JsonElement GatherInputData(Workflow workflow, string nodeId, IExecutionContext context)
    {
        var incomingConnections = workflow.Connections
            .Where(c => c.TargetNodeId == nodeId)
            .ToList();

        if (incomingConnections.Count == 0)
        {
            return EmptyObjectElement;
        }

        if (incomingConnections.Count == 1)
        {
            var connection = incomingConnections[0];

            // Check port-based routing: if the source node produced an outputPort
            // value that differs from this connection's source port, the branch is
            // inactive and should not receive data.
            if (!string.IsNullOrEmpty(connection.SourcePort))
            {
                var defaultOutput = context.NodeOutputs.Get(connection.SourceNodeId);
                if (defaultOutput.HasValue &&
                    defaultOutput.Value.ValueKind == JsonValueKind.Object &&
                    defaultOutput.Value.TryGetProperty("outputPort", out var routedPort) &&
                    routedPort.ValueKind == JsonValueKind.String)
                {
                    var activePort = routedPort.GetString();
                    if (!string.Equals(activePort, connection.SourcePort, StringComparison.OrdinalIgnoreCase))
                    {
                        // This branch is not active — return empty input
                        return EmptyObjectElement;
                    }
                }
            }

            var output = context.NodeOutputs.Get(connection.SourceNodeId, connection.SourcePort);
            return output ?? EmptyObjectElement;
        }

        var mergedNode = new JsonObject();

        foreach (var connection in incomingConnections)
        {
            // Apply the same port-routing check for multi-input merges
            if (!string.IsNullOrEmpty(connection.SourcePort))
            {
                var defaultOutput = context.NodeOutputs.Get(connection.SourceNodeId);
                if (defaultOutput.HasValue &&
                    defaultOutput.Value.ValueKind == JsonValueKind.Object &&
                    defaultOutput.Value.TryGetProperty("outputPort", out var routedPort) &&
                    routedPort.ValueKind == JsonValueKind.String)
                {
                    var activePort = routedPort.GetString();
                    if (!string.Equals(activePort, connection.SourcePort, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip inactive branch
                    }
                }
            }

            var output = context.NodeOutputs.Get(connection.SourceNodeId, connection.SourcePort);
            if (output.HasValue)
            {
                var key = $"{connection.SourceNodeId}_{connection.SourcePort}";
                var valueNode = JsonNode.Parse(output.Value.GetRawText());
                mergedNode[key] = valueNode;
            }
        }

        return JsonElementFromNode(mergedNode);
    }

    private static void StoreNodeOutput(IExecutionContext context, string nodeId, NodeOutput output)
    {
        if (output.Data.ValueKind == JsonValueKind.Object &&
            output.Data.TryGetProperty("outputPort", out var portElement) &&
            portElement.ValueKind == JsonValueKind.String)
        {
            var portName = portElement.GetString() ?? "output";
            context.NodeOutputs.Set(nodeId, portName, output.Data);
        }

        context.NodeOutputs.Set(nodeId, output.Data);
    }

    /// <summary>
    /// Checks whether a node sits on an inactive branch. A node is inactive when
    /// every incoming connection originates from a source port that was not the
    /// active output port of its source node (e.g. the "true" branch of an If
    /// node whose condition evaluated to false).
    /// </summary>
    private static bool IsOnInactiveBranch(Workflow workflow, string nodeId, IExecutionContext context)
    {
        var incomingConnections = workflow.Connections
            .Where(c => c.TargetNodeId == nodeId)
            .ToList();

        if (incomingConnections.Count == 0)
            return false;

        foreach (var connection in incomingConnections)
        {
            if (string.IsNullOrEmpty(connection.SourcePort))
                return false; // Default port — always active

            var defaultOutput = context.NodeOutputs.Get(connection.SourceNodeId);
            if (!defaultOutput.HasValue)
                continue; // Source hasn't run yet — not inactive, just pending

            // If the source output doesn't declare an outputPort, the connection is active
            if (defaultOutput.Value.ValueKind != JsonValueKind.Object ||
                !defaultOutput.Value.TryGetProperty("outputPort", out var routedPort) ||
                routedPort.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            // If this connection's source port matches the active port, the branch is active
            if (string.Equals(routedPort.GetString(), connection.SourcePort, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // All incoming connections are from inactive ports
        return true;
    }

    private static List<List<string>> BuildExecutionLevels(Workflow workflow)
    {
        return BuildExecutionLevelsForNodes(workflow, workflow.Nodes.Select(n => n.Id).ToHashSet());
    }

    /// <summary>
    /// Finds all nodes reachable from a loop node's "item" output port.
    /// </summary>
    private static HashSet<string> GetItemPortSubgraph(Workflow workflow, string loopNodeId)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        // Seed with nodes directly connected to the "item" port
        foreach (var conn in workflow.Connections)
        {
            if (string.Equals(conn.SourceNodeId, loopNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(conn.SourcePort, "item", StringComparison.OrdinalIgnoreCase))
            {
                if (reachable.Add(conn.TargetNodeId))
                    queue.Enqueue(conn.TargetNodeId);
            }
        }

        // BFS to find all downstream nodes
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var conn in workflow.Connections)
            {
                if (string.Equals(conn.SourceNodeId, current, StringComparison.OrdinalIgnoreCase) &&
                    reachable.Add(conn.TargetNodeId))
                {
                    queue.Enqueue(conn.TargetNodeId);
                }
            }
        }

        return reachable;
    }

    /// <summary>
    /// Builds topological execution levels for a subgraph of nodes,
    /// treating edges from the loop node as already satisfied.
    /// </summary>
    private static List<List<string>> BuildSubgraphExecutionLevels(
        Workflow workflow, HashSet<string> subgraphNodes, string loopNodeId)
    {
        var inDegree = subgraphNodes.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (var connection in workflow.Connections)
        {
            if (subgraphNodes.Contains(connection.TargetNodeId) &&
                !string.Equals(connection.SourceNodeId, loopNodeId, StringComparison.OrdinalIgnoreCase) &&
                subgraphNodes.Contains(connection.SourceNodeId))
            {
                inDegree[connection.TargetNodeId]++;
            }
        }

        var adjacency = subgraphNodes.ToDictionary(n => n, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var connection in workflow.Connections)
        {
            if (subgraphNodes.Contains(connection.SourceNodeId) &&
                subgraphNodes.Contains(connection.TargetNodeId))
            {
                adjacency[connection.SourceNodeId].Add(connection.TargetNodeId);
            }
        }

        var levels = new List<List<string>>();
        var currentLevel = inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();

        while (currentLevel.Count > 0)
        {
            levels.Add(currentLevel);
            var nextLevel = new List<string>();

            foreach (var nodeId in currentLevel)
            {
                foreach (var neighbor in adjacency[nodeId])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                        nextLevel.Add(neighbor);
                }
            }

            currentLevel = nextLevel;
        }

        return levels;
    }

    private static List<List<string>> BuildExecutionLevelsForNodes(Workflow workflow, HashSet<string> nodeIds)
    {
        var inDegree = workflow.Nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = workflow.Nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var connection in workflow.Connections)
        {
            if (adjacency.TryGetValue(connection.SourceNodeId, out var neighbors) &&
                inDegree.ContainsKey(connection.TargetNodeId))
            {
                neighbors.Add(connection.TargetNodeId);
                inDegree[connection.TargetNodeId]++;
            }
        }

        var levels = new List<List<string>>();
        var currentLevel = inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key).ToList();
        var processedCount = 0;

        while (currentLevel.Count > 0)
        {
            levels.Add(currentLevel);
            processedCount += currentLevel.Count;

            var nextLevel = new List<string>();

            foreach (var nodeId in currentLevel)
            {
                foreach (var neighbor in adjacency[nodeId])
                {
                    inDegree[neighbor]--;
                    if (inDegree[neighbor] == 0)
                    {
                        nextLevel.Add(neighbor);
                    }
                }
            }

            currentLevel = nextLevel;
        }

        if (processedCount != workflow.Nodes.Count)
        {
            throw new InvalidOperationException("Workflow contains a cycle");
        }

        return levels;
    }

    private JsonElement EvaluateConfiguration(JsonElement configuration, IExecutionContext context, string nodeId)
    {
        if (configuration.ValueKind == JsonValueKind.Undefined)
        {
            return configuration;
        }

        try
        {
            return EvaluateJsonElement(configuration, context, nodeId, "$");
        }
        catch (ExpressionEvaluationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException(
                $"Failed to evaluate configuration: {ex.Message}",
                $"node:{nodeId}",
                ex);
        }
    }

    private JsonElement EvaluateJsonElement(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => EvaluateStringValue(element, context, nodeId, path),
            JsonValueKind.Object => EvaluateObjectValue(element, context, nodeId, path),
            JsonValueKind.Array => EvaluateArrayValue(element, context, nodeId, path),
            _ => element
        };
    }

    private JsonElement EvaluateStringValue(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue) || stringValue.IndexOf("{{", StringComparison.Ordinal) < 0)
        {
            return element;
        }

        try
        {
            var result = _expressionEvaluator.Evaluate(stringValue, context);
            return JsonSerializer.SerializeToElement(result);
        }
        catch (ExpressionEvaluationException ex)
        {
            throw new ExpressionEvaluationException(
                $"Expression evaluation failed at '{path}' in node '{nodeId}': {ex.Message}",
                ex.Expression,
                ex);
        }
    }

    private JsonElement EvaluateObjectValue(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        var resultNode = new JsonObject();

        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = $"{path}.{property.Name}";
            var evaluatedValue = EvaluateJsonElement(property.Value, context, nodeId, propertyPath);
            var valueNode = JsonNode.Parse(evaluatedValue.GetRawText());
            resultNode[property.Name] = valueNode;
        }

        return JsonElementFromNode(resultNode);
    }

    private JsonElement EvaluateArrayValue(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        var resultArray = new JsonArray();
        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            var itemPath = $"{path}[{index}]";
            var evaluatedItem = EvaluateJsonElement(item, context, nodeId, itemPath);
            var itemNode = JsonNode.Parse(evaluatedItem.GetRawText());
            resultArray.Add(itemNode);
            index++;
        }

        return JsonElementFromNode(resultArray);
    }

    private static JsonElement JsonElementFromNode(JsonNode node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }
}
