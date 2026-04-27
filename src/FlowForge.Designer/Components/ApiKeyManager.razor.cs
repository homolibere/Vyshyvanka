using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace FlowForge.Designer.Components;

public partial class ApiKeyManager
{
    [Inject]
    private FlowForgeApiClient ApiClient { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private List<ApiKeyModel> _keys = [];
    private bool _isLoading;

    // Create form
    private bool _showCreateForm;
    private string _formName = "";
    private string _formExpiry = "";
    private bool _isSaving;
    private string? _formError;

    // Created key reveal
    private string? _createdKey;
    private bool _copied;

    // Confirm actions
    private Guid? _confirmRevokeId;
    private Guid? _confirmDeleteId;

    protected override async Task OnInitializedAsync()
    {
        await LoadKeysAsync();
    }

    private async Task LoadKeysAsync()
    {
        _isLoading = true;
        try
        {
            _keys = await ApiClient.GetApiKeysAsync();
        }
        catch
        {
            _keys = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowCreateForm()
    {
        _showCreateForm = true;
        _formName = "";
        _formExpiry = "";
        _formError = null;
    }

    private void CloseForm()
    {
        _showCreateForm = false;
        _formError = null;
    }

    private async Task HandleCreate()
    {
        if (string.IsNullOrWhiteSpace(_formName))
        {
            _formError = "Name is required.";
            return;
        }

        _isSaving = true;
        _formError = null;

        try
        {
            DateTime? expiresAt = !string.IsNullOrEmpty(_formExpiry) && int.TryParse(_formExpiry, out var days)
                ? DateTime.UtcNow.AddDays(days)
                : null;

            var model = new CreateApiKeyModel
            {
                Name = _formName.Trim(),
                ExpiresAt = expiresAt
            };

            var result = await ApiClient.CreateApiKeyAsync(model);
            if (result is not null)
            {
                _createdKey = result.Key;
                _copied = false;
                await LoadKeysAsync();
                CloseForm();
                ToastService.ShowSuccess($"API key '{result.Name}' created");
            }
        }
        catch (ApiException ex)
        {
            _formError = ex.Message;
        }
        catch (Exception ex)
        {
            _formError = $"Failed to create key: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task CopyKey()
    {
        if (_createdKey is null) return;

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", _createdKey);
            _copied = true;
        }
        catch
        {
            ToastService.ShowError("Failed to copy to clipboard");
        }
    }

    private void DismissCreatedKey()
    {
        _createdKey = null;
        _copied = false;
    }

    private void RequestRevoke(Guid id)
    {
        _confirmRevokeId = id;
        _confirmDeleteId = null;
    }

    private void RequestDelete(Guid id)
    {
        _confirmDeleteId = id;
        _confirmRevokeId = null;
    }

    private void CancelAction()
    {
        _confirmRevokeId = null;
        _confirmDeleteId = null;
    }

    private async Task ConfirmRevoke()
    {
        if (_confirmRevokeId is null) return;

        var id = _confirmRevokeId.Value;
        var name = _keys.FirstOrDefault(k => k.Id == id)?.Name ?? "key";
        _confirmRevokeId = null;

        try
        {
            await ApiClient.RevokeApiKeyAsync(id);
            await LoadKeysAsync();
            ToastService.ShowSuccess($"API key '{name}' revoked");
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to revoke: {ex.Message}");
        }
    }

    private async Task ConfirmDelete()
    {
        if (_confirmDeleteId is null) return;

        var id = _confirmDeleteId.Value;
        var name = _keys.FirstOrDefault(k => k.Id == id)?.Name ?? "key";
        _confirmDeleteId = null;

        try
        {
            await ApiClient.DeleteApiKeyAsync(id);
            await LoadKeysAsync();
            ToastService.ShowSuccess($"API key '{name}' deleted");
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to delete: {ex.Message}");
        }
    }
}
