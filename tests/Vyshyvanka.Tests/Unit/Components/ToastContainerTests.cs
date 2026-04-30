using Bunit;
using Bunit.TestDoubles;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Vyshyvanka.Tests.Unit.Components;

public class ToastContainerTests : BunitContext
{
    private readonly ToastService _toastService = new();

    public ToastContainerTests()
    {
        Services.AddSingleton(_toastService);
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
    }

    [Fact]
    public void WhenNoToastsThenContainerIsEmpty()
    {
        var cut = Render<ToastContainer>();

        cut.FindAll(".toast").Should().BeEmpty();
    }

    [Fact]
    public void WhenToastAddedThenRendersToast()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowSuccess("Operation completed"));

        cut.FindAll(".toast").Should().HaveCount(1);
        cut.Find(".toast-message").TextContent.Should().Be("Operation completed");
    }

    [Fact]
    public void WhenMultipleToastsAddedThenRendersAll()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() =>
        {
            _toastService.ShowSuccess("Success!");
            _toastService.ShowError("Error!");
            _toastService.ShowWarning("Warning!");
        });

        cut.FindAll(".toast").Should().HaveCount(3);
    }

    [Fact]
    public void WhenSuccessToastThenHasSuccessClass()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowSuccess("Done"));

        cut.Find(".toast").ClassList.Should().Contain("toast-success");
    }

    [Fact]
    public void WhenErrorToastThenHasErrorClass()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowError("Failed"));

        cut.Find(".toast").ClassList.Should().Contain("toast-error");
    }

    [Fact]
    public void WhenWarningToastThenHasWarningClass()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowWarning("Careful"));

        cut.Find(".toast").ClassList.Should().Contain("toast-warning");
    }

    [Fact]
    public void WhenInfoToastThenHasInfoClass()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowInfo("FYI"));

        cut.Find(".toast").ClassList.Should().Contain("toast-info");
    }

    [Fact]
    public void WhenToastHasTitleThenRendersTitle()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowSuccess("Details here", "Success Title"));

        cut.Find(".toast-title").TextContent.Should().Be("Success Title");
    }

    [Fact]
    public void WhenToastDismissedThenRemovedFromContainer()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() => _toastService.ShowSuccess("Temp", dismissTimeout: 0));
        cut.FindAll(".toast").Should().HaveCount(1);

        // Dismiss via the close button triggers HandleDismiss -> ToastService.Remove
        cut.Find(".toast-close").Click();

        cut.WaitForState(() => cut.FindAll(".toast").Count == 0, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void WhenToastsClearedThenContainerIsEmpty()
    {
        var cut = Render<ToastContainer>();

        cut.InvokeAsync(() =>
        {
            _toastService.ShowSuccess("One");
            _toastService.ShowError("Two");
        });
        cut.FindAll(".toast").Should().HaveCount(2);

        cut.InvokeAsync(() => _toastService.Clear());

        cut.FindAll(".toast").Should().BeEmpty();
    }
}
