using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Expressions;

namespace FlowForge.Engine.Execution;

/// <summary>
/// Default implementation of the workflow execution engine.
/// Supports topological execution order, data flow between nodes, and parallel branch execution.
/// </summary>
public class WorkflowEngine : IWorkflowEngine
{
    private readonly INodeRegistry _nodeRegistry;
    private readonly IExpressionEvaluator _expressionEvaluator;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeExecutions = new();

    /// <summary>
    /// Creates a new workflow engine instance.
    /// </summary>
    public WorkflowEngine(INodeRegistry nodeRegistry, IExpressionEvaluator expressionEvaluator)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
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
            // Build execution levels for parallel execution
            var executionLevels = BuildExecutionLevels(workflow);

            foreach (var level in executionLevels)
            {
                cts.Token.ThrowIfCancellationRequested();

                // Execute all nodes in this level in parallel
                // Use ToList() to ensure all tasks are started immediately before awaiting
                var levelTasks = level.Select(nodeId =>
                        ExecuteNodeInWorkflowAsync(workflow, nodeId, context, nodeResults, cts.Token))
                    .ToList();

                var levelResults = await Task.WhenAll(levelTasks);

                // Check for failures if error handling mode is StopOnFirstError
                if (workflow.Settings.ErrorHandling == ErrorHandlingMode.StopOnFirstError)
                {
                    var failedResult = levelResults.FirstOrDefault(r => !r.Success);
                    if (failedResult is not null)
                    {
                        return new ExecutionResult
                        {
                            ExecutionId = context.ExecutionId,
                            Success = false,
                            ErrorMessage = failedResult.ErrorMessage,
                            Duration = DateTime.UtcNow - startTime,
                            NodeResults = nodeResults.ToList()
                        };
                    }
                }
            }

            return new ExecutionResult
            {
                ExecutionId = context.ExecutionId,
                Success = nodeResults.All(r => r.Success),
                Duration = DateTime.UtcNow - startTime,
                NodeResults = nodeResults.ToList()
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                ExecutionId = context.ExecutionId,
                Success = false,
                ErrorMessage = "Execution was cancelled",
                Duration = DateTime.UtcNow - startTime,
                NodeResults = nodeResults.ToList()
            };
        }
        finally
        {
            _activeExecutions.TryRemove(context.ExecutionId, out _);
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

            // Evaluate expressions in node configuration
            var evaluatedConfig = EvaluateConfiguration(node.Configuration, context, node.Id);

            var nodeInstance = _nodeRegistry.CreateNode(node.Type, evaluatedConfig);

            // For standalone node execution, use empty input data
            var input = new NodeInput
            {
                Data = default,
                Configuration = evaluatedConfig,
                CredentialId = node.CredentialId
            };

            var output = await nodeInstance.ExecuteAsync(input, context);

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

    /// <summary>
    /// Executes a node within a workflow context, gathering input from upstream nodes.
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteNodeInWorkflowAsync(
        Workflow workflow,
        string nodeId,
        IExecutionContext context,
        ConcurrentBag<NodeExecutionResult> nodeResults,
        CancellationToken cancellationToken)
    {
        var node = workflow.Nodes.First(n => n.Id == nodeId);
        var startTime = DateTime.UtcNow;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Evaluate expressions in node configuration
            var evaluatedConfig = EvaluateConfiguration(node.Configuration, context, nodeId);

            var nodeInstance = _nodeRegistry.CreateNode(node.Type, evaluatedConfig);

            // Gather input data from upstream nodes
            var inputData = GatherInputData(workflow, nodeId, context);

            var input = new NodeInput
            {
                Data = inputData,
                Configuration = evaluatedConfig,
                CredentialId = node.CredentialId
            };

            var output = await nodeInstance.ExecuteAsync(input, context);

            if (output.Success)
            {
                // Store output with port information if available
                StoreNodeOutput(context, node.Id, output);
            }

            var result = new NodeExecutionResult
            {
                NodeId = nodeId,
                Success = output.Success,
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
    /// Gathers input data from all upstream nodes connected to the specified node.
    /// </summary>
    private static JsonElement GatherInputData(Workflow workflow, string nodeId, IExecutionContext context)
    {
        // Find all connections where this node is the target
        var incomingConnections = workflow.Connections
            .Where(c => c.TargetNodeId == nodeId)
            .ToList();

        if (incomingConnections.Count == 0)
        {
            // No upstream connections - return empty object
            return JsonSerializer.SerializeToElement(new { });
        }

        if (incomingConnections.Count == 1)
        {
            // Single upstream connection - return that node's output directly
            var connection = incomingConnections[0];
            var output = context.NodeOutputs.Get(connection.SourceNodeId, connection.SourcePort);
            return output ?? JsonSerializer.SerializeToElement(new { });
        }

        // Multiple upstream connections - merge outputs into a dictionary keyed by source node ID
        var mergedData = new Dictionary<string, object?>();

        foreach (var connection in incomingConnections)
        {
            var output = context.NodeOutputs.Get(connection.SourceNodeId, connection.SourcePort);
            if (output.HasValue)
            {
                var key = $"{connection.SourceNodeId}_{connection.SourcePort}";
                mergedData[key] = JsonSerializer.Deserialize<object>(output.Value.GetRawText());
            }
        }

        return JsonSerializer.SerializeToElement(mergedData);
    }

    /// <summary>
    /// Stores node output, handling port-specific outputs if the output contains routing information.
    /// </summary>
    private static void StoreNodeOutput(IExecutionContext context, string nodeId, NodeOutput output)
    {
        // Check if output contains port routing information (e.g., from If node)
        if (output.Data.ValueKind == JsonValueKind.Object &&
            output.Data.TryGetProperty("outputPort", out var portElement) &&
            portElement.ValueKind == JsonValueKind.String)
        {
            var portName = portElement.GetString() ?? "output";
            context.NodeOutputs.Set(nodeId, portName, output.Data);
        }

        // Always store on default output port as well
        context.NodeOutputs.Set(nodeId, output.Data);
    }

    /// <summary>
    /// Builds execution levels for parallel execution using modified Kahn's algorithm.
    /// Each level contains nodes that can be executed in parallel (no dependencies on each other).
    /// </summary>
    private static List<List<string>> BuildExecutionLevels(Workflow workflow)
    {
        // Build adjacency list and in-degree map
        var inDegree = workflow.Nodes.ToDictionary(n => n.Id, _ => 0);
        var adjacency = workflow.Nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var connection in workflow.Connections)
        {
            if (adjacency.ContainsKey(connection.SourceNodeId) && inDegree.ContainsKey(connection.TargetNodeId))
            {
                adjacency[connection.SourceNodeId].Add(connection.TargetNodeId);
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

    /// <summary>
    /// Evaluates expressions in a node configuration, replacing {{ expression }} patterns with their values.
    /// </summary>
    private JsonElement EvaluateConfiguration(JsonElement configuration, IExecutionContext context, string nodeId)
    {
        if (configuration.ValueKind == JsonValueKind.Undefined)
        {
            return configuration;
        }

        try
        {
            var evaluated = EvaluateJsonElement(configuration, context, nodeId, "$");
            return evaluated;
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

    /// <summary>
    /// Recursively evaluates expressions in a JsonElement.
    /// </summary>
    private JsonElement EvaluateJsonElement(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => EvaluateStringValue(element, context, nodeId, path),
            JsonValueKind.Object => EvaluateObjectValue(element, context, nodeId, path),
            JsonValueKind.Array => EvaluateArrayValue(element, context, nodeId, path),
            _ => element // Numbers, booleans, null - return as-is
        };
    }

    /// <summary>
    /// Evaluates expressions in a string value.
    /// </summary>
    private JsonElement EvaluateStringValue(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        var stringValue = element.GetString();
        if (string.IsNullOrEmpty(stringValue) || !stringValue.Contains("{{"))
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

    /// <summary>
    /// Evaluates expressions in an object value.
    /// </summary>
    private JsonElement EvaluateObjectValue(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        var result = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = $"{path}.{property.Name}";
            var evaluatedValue = EvaluateJsonElement(property.Value, context, nodeId, propertyPath);
            result[property.Name] = JsonSerializer.Deserialize<object>(evaluatedValue.GetRawText());
        }

        return JsonSerializer.SerializeToElement(result);
    }

    /// <summary>
    /// Evaluates expressions in an array value.
    /// </summary>
    private JsonElement EvaluateArrayValue(JsonElement element, IExecutionContext context, string nodeId, string path)
    {
        var result = new List<object?>();
        var index = 0;

        foreach (var item in element.EnumerateArray())
        {
            var itemPath = $"{path}[{index}]";
            var evaluatedItem = EvaluateJsonElement(item, context, nodeId, itemPath);
            result.Add(JsonSerializer.Deserialize<object>(evaluatedItem.GetRawText()));
            index++;
        }

        return JsonSerializer.SerializeToElement(result);
    }
}
