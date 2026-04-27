using FlowForge.Core.Enums;
using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class CredentialManager
{
    [Inject]
    private FlowForgeApiClient ApiClient { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    /// <summary>Whether the Credential Manager is open.</summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>Callback when the Credential Manager is closed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    private List<CredentialModel> _credentials = [];
    private bool _isLoading;
    private bool _showCreateForm;
    private bool _showEditForm;
    private Guid? _editingCredentialId;
    private Guid? _confirmDeleteId;
    private HashSet<string> _storedFields = [];

    // Create/Edit form fields
    private string _formName = "";
    private CredentialType _formType = CredentialType.ApiKey;
    private readonly Dictionary<string, string> _formData = [];
    private bool _isSaving;
    private string? _formError;

    protected override async Task OnParametersSetAsync()
    {
        if (IsOpen)
        {
            await LoadCredentialsAsync();
        }
    }

    private async Task LoadCredentialsAsync()
    {
        _isLoading = true;
        try
        {
            _credentials = await ApiClient.GetCredentialsAsync();
        }
        catch
        {
            _credentials = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowCreateForm()
    {
        _showCreateForm = true;
        _showEditForm = false;
        _editingCredentialId = null;
        _formName = "";
        _formType = CredentialType.ApiKey;
        _formData.Clear();
        _formError = null;
    }

    private async Task ShowEditForm(CredentialModel credential)
    {
        _showEditForm = true;
        _showCreateForm = false;
        _editingCredentialId = credential.Id;
        _formName = credential.Name;
        _formType = credential.Type;
        _formData.Clear();
        _formError = null;
        _storedFields = [];

        // Fetch detail to get stored field keys
        try
        {
            var detail = await ApiClient.GetCredentialAsync(credential.Id);
            if (detail?.StoredFields is not null)
            {
                _storedFields = [..detail.StoredFields];
            }
        }
        catch
        {
            // Proceed without stored field info
        }
    }

    private void CloseForm()
    {
        _showCreateForm = false;
        _showEditForm = false;
        _editingCredentialId = null;
        _formError = null;
        _storedFields = [];
    }

    private async Task HandleCreate()
    {
        if (string.IsNullOrWhiteSpace(_formName))
        {
            _formError = "Name is required.";
            return;
        }

        if (_formData.Count == 0 || _formData.Values.All(string.IsNullOrWhiteSpace))
        {
            _formError = "At least one field value is required.";
            return;
        }

        _isSaving = true;
        _formError = null;

        try
        {
            var model = new CreateCredentialModel
            {
                Name = _formName,
                Type = _formType,
                Data = new Dictionary<string, string>(_formData)
            };

            var created = await ApiClient.CreateCredentialAsync(model);
            if (created is not null)
            {
                await LoadCredentialsAsync();
                CloseForm();
                ToastService.ShowSuccess($"Credential '{created.Name}' created");
            }
        }
        catch (ApiException ex)
        {
            _formError = ex.Message;
        }
        catch (Exception ex)
        {
            _formError = $"Failed to create credential: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task HandleUpdate()
    {
        if (_editingCredentialId is null) return;

        if (string.IsNullOrWhiteSpace(_formName))
        {
            _formError = "Name is required.";
            return;
        }

        _isSaving = true;
        _formError = null;

        try
        {
            var model = new UpdateCredentialModel
            {
                Name = _formName,
                Data = _formData.Count > 0 && _formData.Values.Any(v => !string.IsNullOrWhiteSpace(v))
                    ? new Dictionary<string, string>(_formData)
                    : null
            };

            var updated = await ApiClient.UpdateCredentialAsync(_editingCredentialId.Value, model);
            if (updated is not null)
            {
                await LoadCredentialsAsync();
                CloseForm();
                ToastService.ShowSuccess($"Credential '{updated.Name}' updated");
            }
        }
        catch (ApiException ex)
        {
            _formError = ex.Message;
        }
        catch (Exception ex)
        {
            _formError = $"Failed to update credential: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void RequestDelete(Guid id) => _confirmDeleteId = id;

    private void CancelDelete() => _confirmDeleteId = null;

    private async Task ConfirmDelete()
    {
        if (_confirmDeleteId is null) return;

        var id = _confirmDeleteId.Value;
        var name = _credentials.FirstOrDefault(c => c.Id == id)?.Name ?? "credential";
        _confirmDeleteId = null;

        try
        {
            await ApiClient.DeleteCredentialAsync(id);
            await LoadCredentialsAsync();
            ToastService.ShowSuccess($"Credential '{name}' deleted");

            if (_editingCredentialId == id)
                CloseForm();
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to delete: {ex.Message}");
        }
    }

    private async Task Close()
    {
        if (!_isSaving)
        {
            CloseForm();
            _confirmDeleteId = null;
            await OnClose.InvokeAsync();
        }
    }

    private async Task HandleOverlayClick()
    {
        if (!_showCreateForm && !_showEditForm && _confirmDeleteId is null)
        {
            await Close();
        }
    }

    private string GetFieldValue(string key) => _formData.GetValueOrDefault(key, "");

    private void SetFieldValue(string key, string value) => _formData[key] = value;

    private bool IsFieldStored(string key) => _storedFields.Contains(key);

    private string GetEditPlaceholder(CredentialField field) =>
        IsFieldStored(field.Key) ? "●●●●●●●● (stored)" : field.Label;

    private static string GetTypeIcon(CredentialType type) => type switch
    {
        CredentialType.ApiKey => "🔑",
        CredentialType.OAuth2 => "🔐",
        CredentialType.BasicAuth => "👤",
        CredentialType.CustomHeaders => "📋",
        _ => "🔒"
    };

    private static CredentialField[] GetFieldsForType(CredentialType type) => type switch
    {
        CredentialType.ApiKey =>
        [
            new("apiKey", "API Key / Access Token", true),
            new("baseUrl", "Base URL (optional)", false),
            new("headerName", "Header Name (optional)", false),
            new("prefix", "Prefix (optional, e.g. Bearer)", false)
        ],
        CredentialType.BasicAuth =>
        [
            new("username", "Username / Email", false),
            new("password", "Password / API Token", true),
            new("domain", "Domain / Base URL (optional)", false)
        ],
        CredentialType.OAuth2 =>
        [
            new("accessToken", "Access Token", true),
            new("refreshToken", "Refresh Token (optional)", true),
            new("baseUrl", "Base URL (optional)", false)
        ],
        CredentialType.CustomHeaders =>
        [
            new("header1Name", "Header 1 Name", false),
            new("header1Value", "Header 1 Value", true),
            new("header2Name", "Header 2 Name (optional)", false),
            new("header2Value", "Header 2 Value (optional)", true)
        ],
        _ => []
    };

    private record CredentialField(string Key, string Label, bool IsSecret);
}
