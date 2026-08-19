using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Vyshyvanka.Designer.Services;

namespace Vyshyvanka.Designer.Components;

public partial class SettingsPanel : ComponentBase, IDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private ThemeService ThemeService { get; set; } = null!;

    private string? _uploadError;
    private string? _uploadSuccess;

    protected override void OnInitialized()
    {
        ThemeService.OnThemeChanged += StateHasChanged;
    }

    private async Task ApplyTheme(string themeId)
    {
        await ThemeService.SetThemeAsync(themeId);
    }

    private async Task ExportTheme(string themeId)
    {
        var json = ThemeService.ExportThemeJson(themeId);
        if (string.IsNullOrEmpty(json)) return;
        await Js.InvokeVoidAsync("downloadFile", $"{themeId}.json", json, "application/json");
    }

    private async Task DeleteTheme(string themeId)
    {
        await ThemeService.RemoveThemeAsync(themeId);
    }

    private async Task OnThemeFileSelected(InputFileChangeEventArgs e)
    {
        _uploadError = null;
        _uploadSuccess = null;

        var file = e.File;
        if (file is null) return;

        if (file.Size > 100 * 1024)
        {
            _uploadError = "Theme file too large (max 100KB).";
            return;
        }

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var id = await ThemeService.ImportThemeAsync(json);
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
        ThemeService.OnThemeChanged -= StateHasChanged;
    }
}
