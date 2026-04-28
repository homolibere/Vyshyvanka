using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class SourceManager : IDisposable
{
    [Inject]
    private PluginStateService PluginState { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private Dictionary<string, SourceTestResultModel> _testResults = new();

    /// <summary>Callback when Add Source is clicked.</summary>
    [Parameter]
    public EventCallback OnAddSource { get; set; }

    /// <summary>Callback when Edit Source is clicked.</summary>
    [Parameter]
    public EventCallback<PackageSourceModel> OnEditSource { get; set; }

    protected override void OnInitialized()
    {
        PluginState.OnStateChanged += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        if (!PluginState.Sources.Any())
        {
            await PluginState.LoadSourcesAsync();
        }
    }

    public void Dispose()
    {
        PluginState.OnStateChanged -= StateHasChanged;
    }

    private async Task AddSourceAsync()
    {
        await OnAddSource.InvokeAsync();
    }

    private async Task EditSourceAsync(PackageSourceModel source)
    {
        await OnEditSource.InvokeAsync(source);
    }

    private async Task RemoveSourceAsync(string name)
    {
        // Note: In a full implementation, this would show a confirmation dialog first
        // For now, we directly call remove - the confirmation dialog will be added in task 12.1
        var success = await PluginState.RemoveSourceAsync(name);
        if (success)
        {
            // Clear test result for removed source
            _testResults.Remove(name);
            ToastService.ShowSuccess($"Successfully removed source \"{name}\"", "Source Removed");
        }
        else
        {
            ToastService.ShowError(
                PluginState.ErrorMessage ?? $"Failed to remove source \"{name}\"",
                "Remove Failed");
        }
    }

    private async Task TestSourceAsync(string name)
    {
        var result = await PluginState.TestSourceAsync(name);
        _testResults[name] = result;

        if (result.Success)
        {
            ToastService.ShowSuccess(
                $"Source \"{name}\" is reachable ({result.ResponseTimeMs}ms)",
                "Connection Successful");
        }
        else
        {
            ToastService.ShowError(
                result.ErrorMessage ?? $"Failed to connect to source \"{name}\"",
                "Connection Failed");
        }
    }

    private async Task RetryLoadSourcesAsync()
    {
        PluginState.DismissError();
        await PluginState.LoadSourcesAsync();
    }

    private void DismissError()
    {
        PluginState.DismissError();
    }

    private void DismissStatus()
    {
        PluginState.DismissStatus();
    }
}
