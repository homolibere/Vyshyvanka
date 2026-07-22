using Microsoft.AspNetCore.Components;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Legacy theme toggle (light/dark). Replaced by ThemeSelector in layouts.
/// Kept for backward compatibility but no longer used in production UI.
/// </summary>
public partial class ThemeToggle : ComponentBase, IDisposable
{
    [Inject]
    private ThemeService ThemeService { get; set; } = default!;

    protected override void OnInitialized()
    {
        ThemeService.OnThemeChanged += StateHasChanged;
    }

    private async Task Toggle()
    {
        // Toggle between light/dark by switching to the opposite base mode theme
        if (ThemeService.IsDark)
        {
            await ThemeService.SetThemeAsync("vyshyvanka-light");
        }
        else
        {
            await ThemeService.SetThemeAsync("vyshyvanka-dark");
        }
    }

    public void Dispose()
    {
        ThemeService.OnThemeChanged -= StateHasChanged;
    }
}
