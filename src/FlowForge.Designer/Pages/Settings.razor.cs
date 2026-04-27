using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FlowForge.Designer.Pages;

public partial class Settings : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = null!;

    private async Task GoBack()
    {
        await Js.InvokeVoidAsync("history.back");
    }
}
