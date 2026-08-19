using Bunit;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Tests.Unit.Components;

public class ConfirmDialogTests : BunitContext
{
    [Fact]
    public void WhenOpenThenRendersWithOpenClass()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Delete?")
            .Add(p => p.Message, "Are you sure?"));

        cut.Find(".v-overlay").ClassList.Should().Contain("v-overlay--open");
    }

    [Fact]
    public void WhenClosedThenOverlayLacksOpenClass()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, false)
            .Add(p => p.Title, "Delete?"));

        cut.Find(".v-overlay").ClassList.Should().NotContain("v-overlay--open");
    }

    [Fact]
    public void WhenRenderedThenDisplaysTitleAndMessage()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Confirm Action")
            .Add(p => p.Message, "This cannot be undone."));

        cut.Find(".v-dialog__title").TextContent.Should().Be("Confirm Action");
        cut.Find(".dialog-message").TextContent.Should().Be("This cannot be undone.");
    }

    [Fact]
    public void WhenCustomButtonTextThenRendersCustomLabels()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.ConfirmText, "Yes, delete")
            .Add(p => p.CancelText, "No, keep"));

        var buttons = cut.FindAll(".v-dialog__actions button");
        buttons[0].TextContent.Trim().Should().Be("No, keep");
        buttons[1].TextContent.Trim().Should().Be("Yes, delete");
    }

    [Fact]
    public void WhenConfirmClickedThenInvokesOnConfirm()
    {
        var confirmed = false;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.OnConfirm, () => { confirmed = true; }));

        cut.FindAll(".v-dialog__actions button")[1].Click();

        confirmed.Should().BeTrue();
    }

    [Fact]
    public void WhenCancelClickedThenInvokesOnCancel()
    {
        var cancelled = false;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.OnCancel, () => { cancelled = true; }));

        cut.FindAll(".v-dialog__actions button")[0].Click();

        cancelled.Should().BeTrue();
    }

    [Fact]
    public void WhenCloseButtonClickedThenInvokesOnCancel()
    {
        var cancelled = false;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.OnCancel, () => { cancelled = true; }));

        cut.Find(".dialog-header button").Click();

        cancelled.Should().BeTrue();
    }

    [Fact]
    public void WhenOverlayClickedThenInvokesOnCancel()
    {
        var cancelled = false;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.OnCancel, () => { cancelled = true; }));

        cut.Find(".v-overlay").Click();

        cancelled.Should().BeTrue();
    }

    [Fact]
    public void WhenProcessingThenButtonsAreDisabled()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.IsProcessing, true)
            .Add(p => p.ProcessingText, "Deleting..."));

        var buttons = cut.FindAll(".v-dialog__actions button");
        buttons[0].HasAttribute("disabled").Should().BeTrue();
        buttons[1].HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void WhenProcessingThenShowsProcessingText()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Test")
            .Add(p => p.IsProcessing, true)
            .Add(p => p.ProcessingText, "Deleting..."));

        cut.Find(".v-dialog__actions button:last-child").TextContent.Should().Contain("Deleting...");
    }

    [Fact]
    public void WhenDangerVariantThenAppliesDangerStyling()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Delete")
            .Add(p => p.Variant, ConfirmDialogVariant.Danger));

        cut.Find(".v-dialog").ClassList.Should().Contain("v-dialog--danger");
    }

    [Fact]
    public void WhenWarningVariantThenAppliesWarningStyling()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Warning")
            .Add(p => p.Variant, ConfirmDialogVariant.Warning));

        cut.Find(".v-dialog").ClassList.Should().Contain("v-dialog--warning");
    }

    [Fact]
    public void WhenAffectedItemsProvidedThenRendersItemList()
    {
        var items = new List<string> { "Workflow A", "Workflow B" };
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Delete")
            .Add(p => p.AffectedItems, items)
            .Add(p => p.AffectedItemsLabel, "Affected workflows:"));

        cut.Find(".affected-items-label").TextContent.Should().Be("Affected workflows:");
        var listItems = cut.FindAll(".affected-items-list li");
        listItems.Should().HaveCount(2);
        listItems[0].TextContent.Should().Be("Workflow A");
    }

    [Fact]
    public void WhenIconProvidedThenRendersIcon()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Delete")
            .Add(p => p.Icon, "fa-trash"));

        cut.Find(".dialog-header-icon").Should().NotBeNull();
    }

    [Fact]
    public void WhenChildContentProvidedThenRendersIt()
    {
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.IsOpen, true)
            .Add(p => p.Title, "Custom")
            .AddChildContent("<div class='custom-content'>Custom body</div>"));

        cut.Find(".custom-content").TextContent.Should().Be("Custom body");
    }
}
