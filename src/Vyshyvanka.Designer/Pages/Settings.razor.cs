using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Pages;

public partial class Settings : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private ThemeService _themeService { get; set; } = null!;

    private async Task GoBack()
    {
        await Js.InvokeVoidAsync("history.back");
    }

    private async Task SetPattern(bool vyshyvanka)
    {
        await _themeService.SetCanvasPatternAsync(vyshyvanka ? "vyshyvanka" : "dots");
    }
}
