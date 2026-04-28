using Microsoft.JSInterop;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Service for accessing browser localStorage via JS interop.
/// </summary>
public class BrowserStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask SetItemAsync(string key, string value)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
    }

    public async ValueTask<string?> GetItemAsync(string key)
    {
        return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
    }

    public async ValueTask RemoveItemAsync(string key)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
    }
}
