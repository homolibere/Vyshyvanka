using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Manages execution visualization state: current execution tracking,
/// node execution states, and iteration data.
/// </summary>
public class ExecutionStateService(WorkflowStore store)
{
    private ExecutionResponse? _currentExecution;
    private readonly Dictionary<string, NodeExecutionState> _nodeExecutionStates = new();

    /// <summary>Event raised when execution state changes.</summary>
    public event Action<ExecutionResponse?>? OnExecutionChanged;

    /// <summary>Gets the current execution being visualized.</summary>
    public ExecutionResponse? CurrentExecution => _currentExecution;

    /// <summary>Gets whether an execution is currently being visualized.</summary>
    public bool IsExecutionActive => _currentExecution is not null &&
                                     (_currentExecution.Status == ExecutionStatus.Pending ||
                                      _currentExecution.Status == ExecutionStatus.Running);

    /// <summary>Gets the execution state for a specific node.</summary>
    public NodeExecutionState? GetNodeExecutionState(string nodeId) =>
        _nodeExecutionStates.TryGetValue(nodeId, out var state) ? state : null;

    /// <summary>Gets all node execution states.</summary>
    public IReadOnlyDictionary<string, NodeExecutionState> GetAllNodeExecutionStates() => _nodeExecutionStates;

    /// <summary>Sets the current execution for visualization.</summary>
    public void SetCurrentExecution(ExecutionResponse? execution)
    {
        _currentExecution = execution;
        UpdateNodeExecutionStates();
        OnExecutionChanged?.Invoke(execution);
        store.NotifyStateChanged();
    }

    /// <summary>Updates the current execution state (for polling).</summary>
    public void UpdateExecution(ExecutionResponse execution)
    {
        if (_currentExecution?.Id != execution.Id)
            return;

        _currentExecution = execution;
        UpdateNodeExecutionStates();
        OnExecutionChanged?.Invoke(execution);
        store.NotifyStateChanged();
    }

    /// <summary>Clears the current execution visualization.</summary>
    public void ClearExecutionState()
    {
        _currentExecution = null;
        _nodeExecutionStates.Clear();
        OnExecutionChanged?.Invoke(null);
        store.NotifyStateChanged();
    }

    /// <summary>Updates a single node's execution state from a single-node execution result.</summary>
    public void SetNodeExecutionResult(string nodeId, NodeExecutionResponse result)
    {
        _nodeExecutionStates[nodeId] = new NodeExecutionState
        {
            NodeId = nodeId,
            Status = result.Status,
            StartedAt = result.StartedAt,
            CompletedAt = result.CompletedAt,
            InputData = result.InputData,
            OutputData = result.OutputData,
            ErrorMessage = result.ErrorMessage,
            Iterations = [],
            RoutingSummary = new Dictionary<string, int>()
        };
        store.NotifyStateChanged();
    }

    /// <summary>Updates node execution states from the current execution.</summary>
    private void UpdateNodeExecutionStates()
    {
        _nodeExecutionStates.Clear();

        if (_currentExecution is null)
            return;

        // Group executions by node ID to detect loop iterations
        var grouped = _currentExecution.NodeExecutions
            .GroupBy(ne => ne.NodeId)
            .ToList();

        foreach (var group in grouped)
        {
            var executions = group.ToList();
            var last = executions[^1];

            var iterations = new List<NodeIterationData>();
            var routingSummary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (executions.Count > 1)
            {
                for (var i = 0; i < executions.Count; i++)
                {
                    var ne = executions[i];
                    string? outputPort = null;

                    if (ne.OutputData.HasValue &&
                        ne.OutputData.Value.ValueKind == JsonValueKind.Object &&
                        ne.OutputData.Value.TryGetProperty("outputPort", out var portEl) &&
                        portEl.ValueKind == JsonValueKind.String)
                    {
                        outputPort = portEl.GetString();
                    }

                    if (outputPort is not null)
                    {
                        routingSummary.TryGetValue(outputPort, out var count);
                        routingSummary[outputPort] = count + 1;
                    }

                    iterations.Add(new NodeIterationData
                    {
                        Index = i,
                        InputData = ne.InputData,
                        OutputData = ne.OutputData,
                        OutputPort = outputPort,
                        Success = ne.Status == ExecutionStatus.Completed,
                        ErrorMessage = ne.ErrorMessage
                    });
                }
            }

            _nodeExecutionStates[group.Key] = new NodeExecutionState
            {
                NodeId = last.NodeId,
                Status = last.Status,
                StartedAt = executions[0].StartedAt,
                CompletedAt = last.CompletedAt,
                InputData = last.InputData,
                OutputData = last.OutputData,
                ErrorMessage = last.ErrorMessage,
                Iterations = iterations,
                RoutingSummary = routingSummary
            };
        }
    }
}
