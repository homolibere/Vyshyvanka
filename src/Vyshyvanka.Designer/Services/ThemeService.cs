using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Manages light/dark theme state and persists selection to localStorage.
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private string _currentTheme = "light";
    private string _canvasPattern = "vyshyvanka";

    public event Action? OnThemeChanged;

    public string CurrentTheme => _currentTheme;
    public bool IsDark => _currentTheme == "dark";
    public string CanvasPattern => _canvasPattern;
    public bool IsVyshyvankaPattern => _canvasPattern == "vyshyvanka";

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task InitializeAsync()
    {
        var saved = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "vyshyvanka-theme");
        _currentTheme = saved ?? "light";

        var pattern = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "vyshyvanka-canvas-pattern");
        _canvasPattern = pattern ?? "vyshyvanka";

        await ApplyThemeAsync();
    }

    public async Task ToggleAsync()
    {
        _currentTheme = IsDark ? "light" : "dark";
        await ApplyThemeAsync();
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "vyshyvanka-theme", _currentTheme);
        OnThemeChanged?.Invoke();
    }

    public async Task SetThemeAsync(string theme)
    {
        _currentTheme = theme;
        await ApplyThemeAsync();
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "vyshyvanka-theme", _currentTheme);
        OnThemeChanged?.Invoke();
    }

    public async Task SetCanvasPatternAsync(string pattern)
    {
        _canvasPattern = pattern;
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "vyshyvanka-canvas-pattern", _canvasPattern);
        OnThemeChanged?.Invoke();
    }

    private async Task ApplyThemeAsync()
    {
        await _jsRuntime.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", _currentTheme);
    }
}
