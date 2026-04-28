using Microsoft.AspNetCore.Components;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Components;

public partial class ThemeToggle : ComponentBase, IDisposable
{
    [Inject]
    private ThemeService _themeService { get; set; } = default!;

    protected override void OnInitialized()
    {
        _themeService.OnThemeChanged += StateHasChanged;
    }

    private async Task Toggle()
    {
        await _themeService.ToggleAsync();
    }

    public void Dispose()
    {
        _themeService.OnThemeChanged -= StateHasChanged;
    }
}
