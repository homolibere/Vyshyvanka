using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Pages;

public partial class Settings : ComponentBase, IDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private ThemeService _themeService { get; set; } = null!;
    [Inject] private AuthStateService AuthState { get; set; } = null!;

    private string? _uploadError;
    private string? _uploadSuccess;
    private bool _isAdmin;

    protected override void OnInitialized()
    {
        _themeService.OnThemeChanged += StateHasChanged;
        _isAdmin = string.Equals(AuthState.CurrentUser?.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private async Task GoBack()
    {
        await Js.InvokeVoidAsync("history.back");
    }

    private async Task ApplyTheme(string themeId)
    {
        await _themeService.SetThemeAsync(themeId);
    }

    private async Task ExportTheme(string themeId)
    {
        var json = _themeService.ExportThemeJson(themeId);
        if (string.IsNullOrEmpty(json)) return;
        await Js.InvokeVoidAsync("downloadFile", $"{themeId}.json", json, "application/json");
    }

    private async Task DeleteTheme(string themeId)
    {
        await _themeService.RemoveThemeAsync(themeId);
    }

    private async Task OnThemeFileSelected(InputFileChangeEventArgs e)
    {
        _uploadError = null;
        _uploadSuccess = null;

        var file = e.File;
        if (file is null) return;

        if (file.Size > 100 * 1024) // 100KB max
        {
            _uploadError = "Theme file too large (max 100KB).";
            return;
        }

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var id = await _themeService.ImportThemeAsync(json);
            if (id is null)
            {
                _uploadError = "Invalid theme JSON. Ensure it has id, name, baseMode, and colors.";
            }
            else
            {
                _uploadSuccess = $"Theme imported. Switch to \"{id}\" from the selector.";
            }
        }
        catch
        {
            _uploadError = "Failed to read the theme file.";
        }
    }

    public void Dispose()
    {
        _themeService.OnThemeChanged -= StateHasChanged;
    }
}
