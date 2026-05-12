using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Modal dialog for editing node configuration with a three-panel layout.
/// Displays input data, configuration form, and output data side by side.
/// </summary>
public partial class NodeEditorModal : ComponentBase, IDisposable
{
    [Inject] private WorkflowStateService StateService { get; set; } = null!;

    [Inject] private VyshyvankaApiClient ApiClient { get; set; } = null!;

    [Inject] private ToastService ToastService { get; set; } = null!;

    /// <summary>
    /// ID of the node to edit. Set this to open the modal.
    /// </summary>
    [Parameter]
    public string? NodeId { get; set; }

    /// <summary>
    /// Whether the modal is currently open.
    /// </summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>
    /// Callback invoked when the modal is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>
    /// Callback invoked when configuration is saved.
    /// </summary>
    [Parameter]
    public EventCallback OnSave { get; set; }

    private WorkflowNode? _node;
    private NodeDefinition? _definition;
    private NodeExecutionState? _executionState;
    private int _currentIteration;
    private List<ConfigurationProperty> _properties = [];
    private Dictionary<string, object?> _configValues = new();
    private bool _isJsonMode;
    private bool _showValidationErrors;
    private string? _errorMessage;
    private bool _isDirty;
    private bool _hasSchema;
    private string? _initialRawJson;
    private NodeEditorConfigPanel? _configPanel;
    private bool _isRunningToNode;
    private bool _isRunningThisNode;

    /// <summary>
    /// Indicates whether the node has a configuration schema.
    /// </summary>
    private bool HasSchema => _hasSchema;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (IsOpen && !string.IsNullOrEmpty(NodeId))
        {
            LoadNodeData();
        }
        else if (!IsOpen)
        {
            ResetState();
        }
    }

    /// <summary>
    /// Opens the modal for the specified node.
    /// </summary>
    public void Open(string nodeId)
    {
        NodeId = nodeId;
        LoadNodeData();
    }

    /// <summary>
    /// Closes the modal, saving any pending changes.
    /// </summary>
    public async Task CloseAsync()
    {
        if (_isDirty)
        {
            SaveConfiguration();
        }

        await OnClose.InvokeAsync();
    }

    private void LoadNodeData()
    {
        _errorMessage = null;
        _showValidationErrors = false;
        _isDirty = false;

        if (string.IsNullOrEmpty(NodeId))
        {
            _errorMessage = "No node ID provided";
            return;
        }

        _node = StateService.GetNode(NodeId);
        if (_node is null)
        {
            _errorMessage = $"Node '{NodeId}' not found";
            return;
        }

        _definition = StateService.GetNodeDefinition(_node.Type);
        _executionState = StateService.GetNodeExecutionState(NodeId);
        _currentIteration = _executionState?.HasMultipleIterations == true
            ? _executionState.Iterations.Count - 1
            : 0;

        // Parse configuration schema
        if (_definition?.ConfigurationSchema is not null)
        {
            _properties = ConfigurationSchemaParser.Parse(_definition.ConfigurationSchema);
            _configValues = ConfigurationSchemaParser.ExtractValues(
                _node.Configuration,
                _properties);
            _isJsonMode = false;
            _hasSchema = true;
            _initialRawJson = null;
        }
        else
        {
            // No schema - show JSON editor only (requirement 9.1)
            _properties = [];
            _configValues = new Dictionary<string, object?>();
            _isJsonMode = true;
            _hasSchema = false;
            // Preserve existing configuration as raw JSON
            _initialRawJson = SerializeConfiguration(_node.Configuration);
        }
    }

    private void ResetState()
    {
        _node = null;
        _definition = null;
        _executionState = null;
        _properties = [];
        _configValues = new();
        _isJsonMode = false;
        _showValidationErrors = false;
        _errorMessage = null;
        _isDirty = false;
        _hasSchema = false;
        _initialRawJson = null;
    }

    private static string SerializeConfiguration(JsonElement? config)
    {
        if (!config.HasValue || config.Value.ValueKind == JsonValueKind.Undefined)
            return "{}";

        try
        {
            return JsonSerializer.Serialize(config.Value, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return "{}";
        }
    }

    private void HandleValueChanged((string PropertyName, object? Value) change)
    {
        _configValues[change.PropertyName] = change.Value;
        _isDirty = true;
    }

    private void HandleValuesChanged(Dictionary<string, object?> values)
    {
        _configValues = values;
        _isDirty = true;
    }

    private void HandleJsonModeChanged(bool isJsonMode)
    {
        _isJsonMode = isJsonMode;
    }

    private void HandleCredentialChanged(Guid? credentialId)
    {
        if (_node is null || string.IsNullOrEmpty(NodeId)) return;
        StateService.UpdateNodeCredential(NodeId, credentialId);
        _node = StateService.GetNode(NodeId);
        _isDirty = true;
    }

    private async Task HandleSave()
    {
        // Validate required fields
        var missingRequired = _properties
            .Where(p => p.IsRequired && IsValueEmpty(_configValues.GetValueOrDefault(p.Name)))
            .ToList();

        if (missingRequired.Count > 0)
        {
            _showValidationErrors = true;
            return;
        }

        SaveConfiguration();
        await OnSave.InvokeAsync();
        await OnClose.InvokeAsync();
    }

    private async Task HandleCancel()
    {
        // Discard changes
        _isDirty = false;
        await OnClose.InvokeAsync();
    }

    private async Task HandleClose()
    {
        // Save changes on close (per requirement 1.5)
        if (_isDirty)
        {
            SaveConfiguration();
        }

        await OnClose.InvokeAsync();
    }

    private void HandleOverlayClick()
    {
        // Close on outside click (per requirement 1.4)
        _ = HandleClose();
    }

    private void HandleKeyDown(KeyboardEventArgs e)
    {
        // Close on Escape key (per requirement 1.4)
        if (e.Key == "Escape")
        {
            _ = HandleClose();
        }
    }

    private void SaveConfiguration()
    {
        if (_node is null || string.IsNullOrEmpty(NodeId))
            return;

        JsonElement config;

        if (!_hasSchema || _isJsonMode)
        {
            // For schema-less nodes or JSON mode, parse the raw JSON from the config panel
            var rawJson = _configPanel?.GetRawJson() ?? "{}";
            try
            {
                config = JsonDocument.Parse(rawJson).RootElement.Clone();
            }
            catch (JsonException)
            {
                // If JSON is invalid, don't save
                return;
            }
        }
        else
        {
            // For schema-based nodes in form mode, build from values
            config = ConfigurationSchemaParser.BuildConfiguration(_configValues);
        }

        StateService.UpdateNodeConfiguration(NodeId, config);
        _isDirty = false;
    }

    private static bool IsValueEmpty(object? value)
    {
        if (value is null)
            return true;

        if (value is string s)
            return string.IsNullOrWhiteSpace(s);

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
                _ => false
            };
        }

        return false;
    }

    private async Task RunBeforeNode()
    {
        if (string.IsNullOrEmpty(NodeId) || _isRunningToNode) return;

        // Save pending config changes before running
        if (_isDirty)
        {
            SaveConfiguration();
        }

        _isRunningToNode = true;
        StateHasChanged();

        try
        {
            await SaveWorkflowIfDirtyAsync();

            // Execute predecessors only — API computes and returns the target node's expected input
            var result = await ApiClient.ExecuteUpToNodeAsync(
                StateService.Workflow.Id, NodeId, includeTargetNode: false);

            if (result is not null)
            {
                StateService.SetCurrentExecution(result);
                _executionState = StateService.GetNodeExecutionState(NodeId);
                _currentIteration = 0;

                if (result.Status == Core.Enums.ExecutionStatus.Failed)
                {
                    ToastService.ShowError(result.ErrorMessage ?? "Execution failed");
                }
                else
                {
                    var predecessorCount =
                        result.NodeExecutions.Count(ne => ne.Status != Core.Enums.ExecutionStatus.Pending);
                    ToastService.ShowSuccess($"Executed predecessors — {predecessorCount} node(s) ran");
                }
            }
        }
        catch (ApiException ex)
        {
            ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Execution failed: {ex.Message}");
        }
        finally
        {
            _isRunningToNode = false;
            StateHasChanged();
        }
    }

    private async Task RunThisNode()
    {
        if (string.IsNullOrEmpty(NodeId) || _isRunningThisNode) return;

        // Save pending config changes before running
        if (_isDirty)
        {
            SaveConfiguration();
        }

        _isRunningThisNode = true;
        StateHasChanged();

        try
        {
            await SaveWorkflowIfDirtyAsync();

            // If input data exists and config has no expressions, execute just this node
            var canRunSingle = CurrentInputData.HasValue &&
                               CurrentInputData.Value.ValueKind != JsonValueKind.Undefined &&
                               !NodeConfigHasExpressions();

            if (canRunSingle)
            {
                var nodeResult = await ApiClient.ExecuteSingleNodeAsync(
                    StateService.Workflow.Id, NodeId, CurrentInputData!.Value);

                if (nodeResult is not null)
                {
                    // Update the execution state for this node with the single-node result
                    StateService.SetNodeExecutionResult(NodeId, nodeResult);
                    _executionState = StateService.GetNodeExecutionState(NodeId);
                    _currentIteration = 0;

                    if (nodeResult.Status == Core.Enums.ExecutionStatus.Failed)
                    {
                        ToastService.ShowError(nodeResult.ErrorMessage ?? "Node execution failed");
                    }
                    else
                    {
                        ToastService.ShowSuccess("Executed node (single)");
                    }
                }
            }
            else
            {
                // Full workflow execution up to and including this node
                var result = await ApiClient.ExecuteUpToNodeAsync(
                    StateService.Workflow.Id, NodeId, includeTargetNode: true);

                if (result is not null)
                {
                    StateService.SetCurrentExecution(result);
                    _executionState = StateService.GetNodeExecutionState(NodeId);
                    _currentIteration = _executionState?.HasMultipleIterations == true
                        ? _executionState.Iterations.Count - 1
                        : 0;

                    if (result.Status == Core.Enums.ExecutionStatus.Failed)
                    {
                        ToastService.ShowError(result.ErrorMessage ?? "Execution failed");
                    }
                    else
                    {
                        ToastService.ShowSuccess($"Executed node — {result.NodeExecutions.Count} node(s) ran");
                    }
                }
            }
        }
        catch (ApiException ex)
        {
            ToastService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Execution failed: {ex.Message}");
        }
        finally
        {
            _isRunningThisNode = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Checks if the current node's configuration contains expression references ({{...}}).
    /// </summary>
    private bool NodeConfigHasExpressions()
    {
        if (_node?.Configuration is not { ValueKind: not JsonValueKind.Undefined } config)
            return false;

        var configJson = config.GetRawText();
        return configJson.Contains("{{");
    }

    private async Task SaveWorkflowIfDirtyAsync()
    {
        if (StateService.IsDirty)
        {
            var saved = await ApiClient.UpdateWorkflowAsync(StateService.Workflow);
            if (saved is not null)
            {
                StateService.LoadWorkflow(saved);
                StateService.MarkAsSaved();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Clean up if needed
        GC.SuppressFinalize(this);
    }

    // ── Iteration navigation ────────────────────────────────────────────

    private bool HasIterations => _executionState?.HasMultipleIterations == true;

    private int TotalIterations => _executionState?.Iterations.Count ?? 0;

    private JsonElement? CurrentInputData => HasIterations
        ? _executionState!.Iterations[_currentIteration].InputData
        : _executionState?.InputData;

    private JsonElement? CurrentOutputData => HasIterations
        ? _executionState!.Iterations[_currentIteration].OutputData
        : _executionState?.OutputData;

    private void PreviousIteration()
    {
        if (_currentIteration > 0)
            _currentIteration--;
    }

    private void NextIteration()
    {
        if (_currentIteration < TotalIterations - 1)
            _currentIteration++;
    }

    private string? CurrentIterationPort => HasIterations
        ? _executionState!.Iterations[_currentIteration].OutputPort
        : null;

    private string IterationLabel
    {
        get
        {
            if (!HasIterations) return "";
            var iter = _executionState!.Iterations[_currentIteration];
            var port = iter.OutputPort;
            return port is not null
                ? $"Iteration {_currentIteration + 1}/{TotalIterations} → {port}"
                : $"Iteration {_currentIteration + 1}/{TotalIterations}";
        }
    }
}
