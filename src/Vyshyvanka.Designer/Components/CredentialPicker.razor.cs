using Vyshyvanka.Core.Enums;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class CredentialPicker : ComponentBase
{
    [Inject]
    private VyshyvankaApiClient ApiClient { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    /// <summary>Currently selected credential ID.</summary>
    [Parameter]
    public Guid? SelectedCredentialId { get; set; }

    /// <summary>Callback when the selected credential changes.</summary>
    [Parameter]
    public EventCallback<Guid?> SelectedCredentialIdChanged { get; set; }

    /// <summary>Optional filter to only show credentials of a specific type.</summary>
    [Parameter]
    public CredentialType? FilterType { get; set; }

    private List<CredentialModel> _credentials = [];
    private string _selectedId = "";
    private bool _showCreateForm;
    private bool _isSaving;
    private string? _error;

    // Create form fields
    private string _newName = "";
    private CredentialType _newType = CredentialType.ApiKey;
    private readonly Dictionary<string, string> _newData = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadCredentialsAsync();
    }

    protected override void OnParametersSet()
    {
        _selectedId = SelectedCredentialId?.ToString() ?? "";
        if (FilterType.HasValue)
            _newType = FilterType.Value;
    }

    private async Task LoadCredentialsAsync()
    {
        try
        {
            var all = await ApiClient.GetCredentialsAsync();
            _credentials = FilterType.HasValue
                ? all.Where(c => c.Type == FilterType.Value).ToList()
                : all;
        }
        catch
        {
            _credentials = [];
        }
    }

    private async Task HandleSelectionChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        Guid? credentialId = Guid.TryParse(value, out var id) ? id : null;
        _selectedId = value ?? "";
        await SelectedCredentialIdChanged.InvokeAsync(credentialId);
    }

    private void ToggleCreateForm()
    {
        _showCreateForm = !_showCreateForm;
        _error = null;
        if (_showCreateForm)
        {
            _newName = "";
            _newData.Clear();
        }
    }

    private async Task HandleCreate()
    {
        if (string.IsNullOrWhiteSpace(_newName))
        {
            _error = "Name is required.";
            return;
        }

        if (_newData.Count == 0 || _newData.Values.All(string.IsNullOrWhiteSpace))
        {
            _error = "At least one field value is required.";
            return;
        }

        _isSaving = true;
        _error = null;

        try
        {
            var model = new CreateCredentialModel
            {
                Name = _newName,
                Type = _newType,
                Data = new Dictionary<string, string>(_newData)
            };

            var created = await ApiClient.CreateCredentialAsync(model);
            if (created is not null)
            {
                await LoadCredentialsAsync();
                _selectedId = created.Id.ToString();
                await SelectedCredentialIdChanged.InvokeAsync(created.Id);
                _showCreateForm = false;
                ToastService.ShowSuccess($"Credential '{created.Name}' created");
            }
        }
        catch (ApiException ex)
        {
            _error = ex.Message;
        }
        catch (Exception ex)
        {
            _error = $"Failed to create credential: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private string GetFieldValue(string key) => _newData.GetValueOrDefault(key, "");

    private void SetFieldValue(string key, string value) => _newData[key] = value;

    /// <summary>
    /// Returns the expected fields for each credential type.
    /// </summary>
    private static CredentialField[] GetFieldsForType(CredentialType type) => type switch
    {
        CredentialType.ApiKey =>
        [
            new("apiKey", "API Key / Access Token", true),
            new("baseUrl", "Base URL (e.g. https://gitlab.mycompany.com, leave empty for default)", false),
            new("headerName", "Header Name (optional)", false),
            new("prefix", "Prefix (optional, e.g. Bearer)", false)
        ],
        CredentialType.BasicAuth =>
        [
            new("username", "Username / Email", false),
            new("password", "Password / API Token", true),
            new("domain", "Domain / Base URL (e.g. yourcompany.atlassian.net or https://jira.mycompany.com)", false)
        ],
        CredentialType.OAuth2 =>
        [
            new("accessToken", "Access Token", true),
            new("refreshToken", "Refresh Token (optional)", true),
            new("baseUrl", "Base URL (e.g. https://gitlab.mycompany.com, leave empty for default)", false)
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
