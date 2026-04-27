using System.Text.Json;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FlowForge.Designer.Components;

/// <summary>
/// Modal dialog for editing node configuration with a three-panel layout.
/// Displays input data, configuration form, and output data side by side.
/// </summary>
public partial class NodeEditorModal : ComponentBase, IDisposable
{
    [Inject]
    private WorkflowStateService StateService { get; set; } = null!;

    [Inject]
    private FlowForgeApiClient ApiClient { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

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

    private async Task RunUpToNode()
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
            var result = await ApiClient.ExecuteUpToNodeAsync(
                StateService.Workflow.Id, NodeId);

            if (result is not null)
            {
                StateService.SetCurrentExecution(result);
                _executionState = StateService.GetNodeExecutionState(NodeId);

                if (result.Status == Core.Enums.ExecutionStatus.Failed)
                {
                    ToastService.ShowError(result.ErrorMessage ?? "Execution failed");
                }
                else
                {
                    ToastService.ShowSuccess($"Executed up to node — {result.NodeExecutions.Count} node(s) ran");
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

    /// <inheritdoc />
    public void Dispose()
    {
        // Clean up if needed
        GC.SuppressFinalize(this);
    }
}
