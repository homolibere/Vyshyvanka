using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class ToastContainer : IDisposable
{
    [Inject]
    private ToastService ToastService { get; set; } = null!;

    protected override void OnInitialized()
    {
        ToastService.OnChange += StateHasChanged;
    }

    private void HandleDismiss(string id)
    {
        ToastService.Remove(id);
    }

    public void Dispose()
    {
        ToastService.OnChange -= StateHasChanged;
    }
}
