using Microsoft.AspNetCore.Components;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Layout;

public partial class DesignerToolbar
{
    [Inject] private WorkflowStore Store { get; set; } = null!;

    [Inject] private CanvasStateService CanvasState { get; set; } = null!;

    [Inject] private WorkflowValidationService ValidationService { get; set; } = null!;

    [Inject] private ExecutionStateService ExecutionState { get; set; } = null!;

    [Inject] private WorkflowEditService EditService { get; set; } = null!;

    [Parameter] public bool IsHistoryCollapsed { get; set; } = true;

    [Parameter] public EventCallback OnOpen { get; set; }

    [Parameter] public EventCallback OnSave { get; set; }

    [Parameter] public EventCallback OnExecute { get; set; }

    [Parameter] public EventCallback OnStop { get; set; }

    [Parameter] public EventCallback OnOpenPlugins { get; set; }

    [Parameter] public EventCallback OnOpenCredentials { get; set; }

    [Parameter] public EventCallback OnToggleHistory { get; set; }

    private bool IsValid => ValidationService.ValidationResult.IsValid;

    private bool CanExecute =>
        ValidationService.ValidationResult.IsValid
        && !ExecutionState.IsExecutionActive
        && Store.Workflow.IsActive;

    private string ZoomPercent =>
        FormattableString.Invariant($"{CanvasState.CanvasState.Zoom * 100:0}%");

    private string SaveTitle => IsValid
        ? "Save workflow"
        : "Fix validation errors before saving";

    private string RunTitle
    {
        get
        {
            if (!IsValid) return "Fix validation errors before running";
            if (!Store.Workflow.IsActive) return "Activate workflow before running";
            return "Execute workflow";
        }
    }

    private string ActiveTitle => Store.Workflow.IsActive
        ? "Workflow is active (click to deactivate)"
        : "Workflow is inactive (click to activate)";

    private void Undo() => CanvasState.Undo();

    private void Redo() => CanvasState.Redo();

    private void ZoomIn() => CanvasState.Zoom(CanvasState.CanvasState.Zoom + 0.1);

    private void ZoomOut() => CanvasState.Zoom(CanvasState.CanvasState.Zoom - 0.1);

    private void ResetView() => CanvasState.ResetView();

    private void OnToggleActive() => EditService.ToggleWorkflowActive();
}
