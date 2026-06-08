using System.Text.Json;
using Microsoft.JSInterop;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Manages theme lifecycle: loading built-in and custom themes,
/// applying CSS variables via JS interop, and persisting selection.
/// </summary>
public class ThemeService
{
    private static readonly string[] BuiltInThemeFiles =
    [
        "themes/vyshyvanka-light.json",
        "themes/vyshyvanka-dark.json",
        "themes/slate.json",
        "themes/ocean-dark.json",
        "themes/minimal.json"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _jsRuntime;

    private List<ThemeDefinition> _themes = [];
    private ThemeDefinition? _activeTheme;

    public event Action? OnThemeChanged;

    public IReadOnlyList<ThemeDefinition> AvailableThemes => _themes;
    public ThemeDefinition? ActiveTheme => _activeTheme;
    public string CurrentThemeId => _activeTheme?.Id ?? "vyshyvanka-light";
    public bool IsDark => _activeTheme?.BaseMode == "dark";
    public string CanvasPattern => _activeTheme?.Canvas.Pattern ?? "dots";
    public bool IsVyshyvankaPattern => CanvasPattern == "vyshyvanka";

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Loads all built-in themes via browser fetch and custom themes from localStorage.
    /// Applies the previously saved active theme or defaults to vyshyvanka-light.
    /// </summary>
    public async Task InitializeAsync()
    {
        _themes = [];

        // Load built-in themes using browser's native fetch (resolves relative to app origin)
        foreach (var file in BuiltInThemeFiles)
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string?>("vyshyvankaTheme.fetchThemeJson", file);
                if (string.IsNullOrEmpty(json))
                {
                    await _jsRuntime.InvokeVoidAsync("console.warn", $"[ThemeService] fetchThemeJson returned null/empty for: {file}");
                    continue;
                }

                var theme = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOptions);
                if (theme is not null)
                {
                    _themes.Add(theme with { IsBuiltIn = true });
                }
                else
                {
                    await _jsRuntime.InvokeVoidAsync("console.warn", $"[ThemeService] Deserialization returned null for: {file}");
                }
            }
            catch (Exception ex)
            {
                await _jsRuntime.InvokeVoidAsync("console.error", $"[ThemeService] Failed to load {file}: {ex.Message}");
            }
        }

        // Load custom themes from localStorage
        await LoadCustomThemesAsync();

        // Determine active theme
        var savedId = await _jsRuntime.InvokeAsync<string?>("vyshyvankaTheme.getActiveThemeId");
        _activeTheme = _themes.FirstOrDefault(t => t.Id == savedId)
                       ?? _themes.FirstOrDefault(t => t.Id == "vyshyvanka-light")
                       ?? _themes.FirstOrDefault();

        if (_activeTheme is not null)
        {
            await ApplyThemeAsync(_activeTheme);
        }

        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Switch to a theme by its ID.
    /// </summary>
    public async Task SetThemeAsync(string themeId)
    {
        var theme = _themes.FirstOrDefault(t => t.Id == themeId);
        if (theme is null) return;

        _activeTheme = theme;
        await ApplyThemeAsync(theme);
        await _jsRuntime.InvokeVoidAsync("vyshyvankaTheme.setActiveThemeId", themeId);
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Import a custom theme from JSON string. Returns the theme ID if successful.
    /// </summary>
    public async Task<string?> ImportThemeAsync(string json)
    {
        ThemeDefinition? theme;
        try
        {
            theme = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOptions);
        }
        catch
        {
            return null;
        }

        if (theme is null || string.IsNullOrWhiteSpace(theme.Id) || string.IsNullOrWhiteSpace(theme.Name))
            return null;

        // Prevent overwriting built-in themes
        if (_themes.Any(t => t.Id == theme.Id && t.IsBuiltIn))
            return null;

        var custom = theme with { IsBuiltIn = false };

        // Replace existing custom or add new
        var existingIndex = _themes.FindIndex(t => t.Id == custom.Id && !t.IsBuiltIn);
        if (existingIndex >= 0)
            _themes[existingIndex] = custom;
        else
            _themes.Add(custom);

        await _jsRuntime.InvokeVoidAsync("vyshyvankaTheme.saveCustomTheme", json);
        OnThemeChanged?.Invoke();
        return custom.Id;
    }

    /// <summary>
    /// Remove a custom theme. Cannot remove built-in themes.
    /// If the active theme is removed, falls back to vyshyvanka-light.
    /// </summary>
    public async Task<bool> RemoveThemeAsync(string themeId)
    {
        var theme = _themes.FirstOrDefault(t => t.Id == themeId);
        if (theme is null || theme.IsBuiltIn) return false;

        _themes.Remove(theme);
        await _jsRuntime.InvokeVoidAsync("vyshyvankaTheme.removeCustomTheme", themeId);

        if (_activeTheme?.Id == themeId)
        {
            await SetThemeAsync("vyshyvanka-light");
        }
        else
        {
            OnThemeChanged?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Export the current active theme as a JSON string for download.
    /// </summary>
    public string ExportThemeJson(string themeId)
    {
        var theme = _themes.FirstOrDefault(t => t.Id == themeId);
        if (theme is null) return string.Empty;
        return JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get an icon class by key from the active theme.
    /// Falls back to a sensible default if the key is missing.
    /// </summary>
    public string GetIcon(string key)
    {
        if (_activeTheme?.Icons.TryGetValue(key, out var icon) == true)
            return icon;

        return key switch
        {
            "trigger" => "fa-solid fa-bolt",
            "action" => "fa-solid fa-cog",
            "logic" => "fa-solid fa-code-branch",
            "transform" => "fa-solid fa-shuffle",
            _ => "fa-solid fa-cube"
        };
    }

    private async Task ApplyThemeAsync(ThemeDefinition theme)
    {
        await _jsRuntime.InvokeVoidAsync(
            "vyshyvankaTheme.applyColors",
            theme.Colors,
            theme.BaseMode);
    }

    private async Task LoadCustomThemesAsync()
    {
        try
        {
            var customJson = await _jsRuntime.InvokeAsync<JsonElement[]>("vyshyvankaTheme.getCustomThemes");
            foreach (var element in customJson)
            {
                var raw = element.GetRawText();
                var theme = JsonSerializer.Deserialize<ThemeDefinition>(raw, JsonOptions);
                if (theme is not null && !string.IsNullOrWhiteSpace(theme.Id))
                {
                    _themes.Add(theme with { IsBuiltIn = false });
                }
            }
        }
        catch
        {
            // No custom themes or parsing error — fine
        }
    }
}
