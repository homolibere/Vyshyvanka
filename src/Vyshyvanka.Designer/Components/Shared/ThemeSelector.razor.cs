using Microsoft.AspNetCore.Components;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Components;

public partial class ThemeSelector : ComponentBase, IDisposable
{
    [Inject]
    private ThemeService _themeService { get; set; } = default!;

    private bool _isOpen;

    protected override void OnInitialized()
    {
        _themeService.OnThemeChanged += StateHasChanged;
    }

    private void ToggleDropdown()
    {
        _isOpen = !_isOpen;
    }

    private async Task SelectTheme(string themeId)
    {
        await _themeService.SetThemeAsync(themeId);
        _isOpen = false;
    }

    private void OnFocusOut()
    {
        // Close dropdown when focus leaves the component
        _ = Task.Delay(150).ContinueWith(_ =>
        {
            _isOpen = false;
            InvokeAsync(StateHasChanged);
        });
    }

    public void Dispose()
    {
        _themeService.OnThemeChanged -= StateHasChanged;
    }
}
