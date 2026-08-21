using Microsoft.AspNetCore.Components;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Layout;

public partial class DesignerToolbar : IDisposable
{
    [Inject] private WorkflowStore Store { get; set; } = null!;

    [Inject] private CanvasStateService CanvasState { get; set; } = null!;

    [Inject] private WorkflowValidationService ValidationService { get; set; } = null!;

    [Inject] private ExecutionStateService ExecutionState { get; set; } = null!;

    [Inject] private WorkflowEditService EditService { get; set; } = null!;

    [Parameter] public EventCallback OnSave { get; set; }

    [Parameter] public EventCallback OnExecute { get; set; }

    [Parameter] public EventCallback OnStop { get; set; }

    protected override void OnInitialized()
    {
        // Re-render when the loaded workflow changes (e.g. status, or navigating to another workflow).
        Store.OnStateChanged += OnStoreChanged;
    }

    private void OnStoreChanged() => StateHasChanged();

    private bool IsValid => ValidationService.ValidationResult.IsValid;

    // Designer "Run" issues an Api-mode execution, which is allowed for any status.
    // Only validity and not-already-running gate it.
    private bool CanExecute =>
        ValidationService.ValidationResult.IsValid
        && !ExecutionState.IsExecutionActive;

    private string ZoomPercent =>
        FormattableString.Invariant($"{CanvasState.CanvasState.Zoom * 100:0}%");

    private string SaveTitle => IsValid
        ? "Save workflow"
        : "Fix validation errors before saving";

    private string RunTitle => IsValid
        ? "Execute workflow"
        : "Fix validation errors before running";

    private string ActiveTitle => Store.Workflow.Status == Core.Enums.WorkflowStatus.Active
        ? "Workflow is active — automatic triggers armed (click to pause)"
        : "Workflow is not active — automatic triggers disarmed (click to activate)";

    private void Undo() => CanvasState.Undo();

    private void Redo() => CanvasState.Redo();

    private void ZoomIn() => CanvasState.Zoom(CanvasState.CanvasState.Zoom + 0.1);

    private void ZoomOut() => CanvasState.Zoom(CanvasState.CanvasState.Zoom - 0.1);

    private void ResetView() => CanvasState.ResetView();

    private void OnToggleActive() => EditService.ToggleWorkflowActive();

    public void Dispose()
    {
        Store.OnStateChanged -= OnStoreChanged;
    }
}
