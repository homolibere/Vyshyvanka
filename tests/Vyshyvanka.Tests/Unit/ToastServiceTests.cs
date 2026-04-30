using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Tests.Unit;

public class ToastServiceTests
{
    private readonly ToastService _sut = new();

    [Fact]
    public void WhenShowSuccessThenAddsSuccessToast()
    {
        _sut.ShowSuccess("Done!");

        _sut.Toasts.Should().HaveCount(1);
        _sut.Toasts[0].Type.Should().Be(ToastType.Success);
        _sut.Toasts[0].Message.Should().Be("Done!");
    }

    [Fact]
    public void WhenShowErrorThenAddsErrorToast()
    {
        _sut.ShowError("Failed!", "Error Title");

        _sut.Toasts.Should().HaveCount(1);
        _sut.Toasts[0].Type.Should().Be(ToastType.Error);
        _sut.Toasts[0].Title.Should().Be("Error Title");
        _sut.Toasts[0].Message.Should().Be("Failed!");
    }

    [Fact]
    public void WhenShowWarningThenAddsWarningToast()
    {
        _sut.ShowWarning("Careful!");

        _sut.Toasts[0].Type.Should().Be(ToastType.Warning);
    }

    [Fact]
    public void WhenShowInfoThenAddsInfoToast()
    {
        _sut.ShowInfo("FYI");

        _sut.Toasts[0].Type.Should().Be(ToastType.Info);
    }

    [Fact]
    public void WhenShowWithCustomTimeoutThenSetsTimeout()
    {
        _sut.Show(ToastType.Success, "Quick", dismissTimeout: 2000);

        _sut.Toasts[0].DismissTimeout.Should().Be(2000);
    }

    [Fact]
    public void WhenRemoveByIdThenRemovesToast()
    {
        _sut.ShowSuccess("One");
        _sut.ShowError("Two");
        var idToRemove = _sut.Toasts[0].Id;

        _sut.Remove(idToRemove);

        _sut.Toasts.Should().HaveCount(1);
        _sut.Toasts[0].Message.Should().Be("Two");
    }

    [Fact]
    public void WhenRemoveNonexistentIdThenNoChange()
    {
        _sut.ShowSuccess("One");

        _sut.Remove("nonexistent-id");

        _sut.Toasts.Should().HaveCount(1);
    }

    [Fact]
    public void WhenClearThenRemovesAllToasts()
    {
        _sut.ShowSuccess("One");
        _sut.ShowError("Two");
        _sut.ShowWarning("Three");

        _sut.Clear();

        _sut.Toasts.Should().BeEmpty();
    }

    [Fact]
    public void WhenToastAddedThenOnChangeIsFired()
    {
        var changeCount = 0;
        _sut.OnChange += () => changeCount++;

        _sut.ShowSuccess("Test");

        changeCount.Should().Be(1);
    }

    [Fact]
    public void WhenToastRemovedThenOnChangeIsFired()
    {
        var changeCount = 0;
        _sut.ShowSuccess("Test");
        _sut.OnChange += () => changeCount++;

        _sut.Remove(_sut.Toasts[0].Id);

        changeCount.Should().Be(1);
    }

    [Fact]
    public void WhenClearedThenOnChangeIsFired()
    {
        var changeCount = 0;
        _sut.ShowSuccess("Test");
        _sut.OnChange += () => changeCount++;

        _sut.Clear();

        changeCount.Should().Be(1);
    }

    [Fact]
    public void WhenMultipleToastsAddedThenEachHasUniqueId()
    {
        _sut.ShowSuccess("One");
        _sut.ShowSuccess("Two");
        _sut.ShowSuccess("Three");

        var ids = _sut.Toasts.Select(t => t.Id).ToList();
        ids.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public void WhenErrorToastThenDefaultTimeoutIsLonger()
    {
        _sut.ShowError("Error");

        _sut.Toasts[0].DismissTimeout.Should().Be(8000);
    }

    [Fact]
    public void WhenSuccessToastThenDefaultTimeoutIs5000()
    {
        _sut.ShowSuccess("Success");

        _sut.Toasts[0].DismissTimeout.Should().Be(5000);
    }
}
