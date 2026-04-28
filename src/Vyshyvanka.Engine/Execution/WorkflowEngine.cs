using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        IPluginHost? pluginHost = null)
    {
        _nodeRegistry = nodeRegistry ?? throw new ArgumentNullException(nameof(nodeRegistry));
        _expressionEvaluator = expressionEvaluator ?? throw new ArgumentNullException(nameof(expressionEvaluator));
        _pluginHost = pluginHost;
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
            var executionLevels = BuildExecutionLevels(workflow);
            var maxParallelism = GetEffectiveMaxParallelism(workflow.Settings.MaxDegreeOfParallelism);

            foreach (var level in executionLevels)
            {
                cts.Token.ThrowIfCancellationRequested();

                var levelResults = await ExecuteLevelWithThrottlingAsync(
                    workflow, level, context, nodeResults, maxParallelism, cts.Token);

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

            var evaluatedConfig = EvaluateConfiguration(node.Configuration, context, nodeId);
            var nodeInstance = _nodeRegistry.CreateNode(node.Type, evaluatedConfig);
            var inputData = GatherInputData(workflow, nodeId, context);

            var input = new NodeInput
            {
                Data = inputData,
                Configuration = evaluatedConfig,
                CredentialId = node.CredentialId
            };

            var output = await ExecuteNodeInstanceAsync(nodeInstance, input, context, timeout);

            if (output.Success)
            {
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
            var output = context.NodeOutputs.Get(connection.SourceNodeId, connection.SourcePort);
            return output ?? EmptyObjectElement;
        }

        var mergedNode = new JsonObject();

        foreach (var connection in incomingConnections)
        {
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

    private static List<List<string>> BuildExecutionLevels(Workflow workflow)
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
