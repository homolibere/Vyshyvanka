using Bunit;
using CsCheck;
using FlowForge.Designer.Components;
using FlowForge.Designer.Models;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for ConfirmDialog component.
/// Feature: designer-plugin-management, Property 7: Workflow Reference Warning on Uninstall
/// </summary>
public class ConfirmDialogTests
{
    // Generator for alphanumeric strings safe for HTML display
    private static readonly Gen<string> AlphaNumGen = Gen.Char['a', 'z'].Array[3, 20]
        .Select(chars => new string(chars));

    // Generator for workflow names
    private static readonly Gen<string> WorkflowNameGen = Gen.Select(
        AlphaNumGen,
        Gen.Int[1, 1000],
        (name, id) => $"{name} Workflow {id}");

    // Generator for affected workflow lists (1-10 workflows)
    private static readonly Gen<string[]> AffectedWorkflowsGen = WorkflowNameGen.Array[1, 10];

    /// <summary>
    /// Feature: designer-plugin-management, Property 7: Workflow Reference Warning on Uninstall
    /// For any package uninstallation request where workflows reference nodes from the package,
    /// the Plugin_Manager_UI SHALL display a warning dialog listing the affected workflows before proceeding.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void ConfirmDialog_DisplaysAllAffectedWorkflows()
    {
        AffectedWorkflowsGen.Sample(affectedWorkflows =>
        {
            using var ctx = new TestContext();

            // Arrange & Act - Render ConfirmDialog with affected workflows
            var cut = ctx.RenderComponent<ConfirmDialog>(parameters => parameters
                .Add(p => p.IsOpen, true)
                .Add(p => p.Title, "Uninstall Package")
                .Add(p => p.Message, "This package is used by the following workflows:")
                .Add(p => p.Variant, ConfirmDialogVariant.Danger)
                .Add(p => p.AffectedItems, affectedWorkflows.ToList())
                .Add(p => p.AffectedItemsLabel, "Affected Workflows:")
                .Add(p => p.ConfirmText, "Uninstall Anyway")
                .Add(p => p.CancelText, "Cancel"));

            var markup = cut.Markup;

            // Assert - Dialog is visible
            Assert.Contains("open", markup, StringComparison.Ordinal);

            // Assert - Title is displayed
            Assert.Contains("Uninstall Package", markup, StringComparison.Ordinal);

            // Assert - Message is displayed
            Assert.Contains("This package is used by the following workflows:", markup, StringComparison.Ordinal);

            // Assert - Affected items label is displayed
            Assert.Contains("Affected Workflows:", markup, StringComparison.Ordinal);

            // Assert - All affected workflows are displayed
            foreach (var workflow in affectedWorkflows)
            {
                Assert.Contains(workflow, markup, StringComparison.Ordinal);
            }

            // Assert - Confirm and Cancel buttons are displayed
            Assert.Contains("Uninstall Anyway", markup, StringComparison.Ordinal);
            Assert.Contains("Cancel", markup, StringComparison.Ordinal);

            // Assert - Danger variant styling is applied
            Assert.Contains("header-danger", markup, StringComparison.Ordinal);
            Assert.Contains("btn-danger", markup, StringComparison.Ordinal);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 7: Workflow Reference Warning Variant
    /// For any confirmation dialog with the Danger variant, the dialog SHALL display
    /// appropriate danger styling (red header, red confirm button).
    /// Validates: Requirements 6.1, 6.2
    /// </summary>
    [Fact]
    public void ConfirmDialog_DangerVariant_DisplaysDangerStyling()
    {
        var messageGen = AlphaNumGen.Select(s => $"Are you sure you want to delete {s}?");

        messageGen.Sample(message =>
        {
            using var ctx = new TestContext();

            var cut = ctx.RenderComponent<ConfirmDialog>(parameters => parameters
                .Add(p => p.IsOpen, true)
                .Add(p => p.Title, "Confirm Delete")
                .Add(p => p.Message, message)
                .Add(p => p.Variant, ConfirmDialogVariant.Danger)
                .Add(p => p.ConfirmText, "Delete")
                .Add(p => p.CancelText, "Cancel"));

            var markup = cut.Markup;

            // Assert - Danger styling is applied
            Assert.Contains("header-danger", markup, StringComparison.Ordinal);
            Assert.Contains("btn-danger", markup, StringComparison.Ordinal);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 7: Workflow Reference Warning Variant
    /// For any confirmation dialog with the Warning variant, the dialog SHALL display
    /// appropriate warning styling (yellow/orange header, yellow/orange confirm button).
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void ConfirmDialog_WarningVariant_DisplaysWarningStyling()
    {
        var messageGen = AlphaNumGen.Select(s => $"This action may affect {s}. Continue?");

        messageGen.Sample(message =>
        {
            using var ctx = new TestContext();

            var cut = ctx.RenderComponent<ConfirmDialog>(parameters => parameters
                .Add(p => p.IsOpen, true)
                .Add(p => p.Title, "Warning")
                .Add(p => p.Message, message)
                .Add(p => p.Variant, ConfirmDialogVariant.Warning)
                .Add(p => p.ConfirmText, "Continue")
                .Add(p => p.CancelText, "Cancel"));

            var markup = cut.Markup;

            // Assert - Warning styling is applied
            Assert.Contains("header-warning", markup, StringComparison.Ordinal);
            Assert.Contains("btn-warning", markup, StringComparison.Ordinal);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 7: Dialog Buttons Disabled During Processing
    /// For any confirmation dialog in processing state, the buttons SHALL be disabled.
    /// Validates: Requirements 10.4, 10.5
    /// </summary>
    [Fact]
    public void ConfirmDialog_ProcessingState_DisablesButtons()
    {
        var messageGen = AlphaNumGen.Select(s => $"Processing {s}...");

        messageGen.Sample(message =>
        {
            using var ctx = new TestContext();

            var cut = ctx.RenderComponent<ConfirmDialog>(parameters => parameters
                .Add(p => p.IsOpen, true)
                .Add(p => p.Title, "Processing")
                .Add(p => p.Message, message)
                .Add(p => p.IsProcessing, true)
                .Add(p => p.ProcessingText, "Working..."));

            var markup = cut.Markup;

            // Assert - Processing indicator is shown
            Assert.Contains("Working...", markup, StringComparison.Ordinal);
            Assert.Contains("processing-indicator", markup, StringComparison.Ordinal);

            // Assert - Buttons are disabled (check for disabled attribute)
            // Count disabled buttons - should have at least 2 (cancel and confirm)
            var disabledCount = markup.Split("disabled").Length - 1;
            Assert.True(disabledCount >= 2, "Both buttons should be disabled during processing");
        }, iter: 100);
    }
}
