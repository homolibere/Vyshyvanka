using FlowForge.Designer.Models;
using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class SourceEditModal
{
    [Inject]
    private PluginStateService PluginState { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private string _name = string.Empty;
    private string _url = string.Empty;
    private bool _isEnabled = true;
    private bool _isTrusted;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _apiKey = string.Empty;
    private bool _showCredentials;
    private bool _isSaving;

    private string? _nameError;
    private string? _urlError;
    private string? _generalError;

    /// <summary>Whether the modal is open.</summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>The source being edited, or null for add mode.</summary>
    [Parameter]
    public PackageSourceModel? Source { get; set; }

    /// <summary>Callback when the modal is closed.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Callback when a source is saved successfully.</summary>
    [Parameter]
    public EventCallback<PackageSourceModel> OnSaved { get; set; }

    /// <summary>Whether we're in edit mode (vs add mode).</summary>
    private bool IsEditMode => Source is not null;

    /// <summary>Whether the name field has an error.</summary>
    private bool HasNameError => !string.IsNullOrEmpty(_nameError);

    /// <summary>Whether the URL field has an error.</summary>
    private bool HasUrlError => !string.IsNullOrEmpty(_urlError);

    /// <summary>Whether the form is valid for submission.</summary>
    private bool IsValid => !string.IsNullOrWhiteSpace(_name) &&
                            !string.IsNullOrWhiteSpace(_url) &&
                            !HasNameError &&
                            !HasUrlError;

    protected override void OnParametersSet()
    {
        if (IsOpen)
        {
            if (Source is not null)
            {
                // Edit mode - populate from source
                _name = Source.Name;
                _url = Source.Url;
                _isEnabled = Source.IsEnabled;
                _isTrusted = Source.IsTrusted;
                _username = string.Empty; // Don't populate credentials for security
                _password = string.Empty;
                _apiKey = string.Empty;
                _showCredentials = Source.HasCredentials;
            }
            else
            {
                // Add mode - reset to defaults
                ResetForm();
            }

            ClearErrors();
        }
    }

    private void ResetForm()
    {
        _name = string.Empty;
        _url = string.Empty;
        _isEnabled = true;
        _isTrusted = false;
        _username = string.Empty;
        _password = string.Empty;
        _apiKey = string.Empty;
        _showCredentials = false;
    }

    private void ClearErrors()
    {
        _nameError = null;
        _urlError = null;
        _generalError = null;
    }

    private void ToggleCredentials()
    {
        _showCredentials = !_showCredentials;
    }

    private bool ValidateForm()
    {
        ClearErrors();
        var isValid = true;

        // Validate name
        if (string.IsNullOrWhiteSpace(_name))
        {
            _nameError = "Source name is required";
            isValid = false;
        }
        else if (_name.Length > 100)
        {
            _nameError = "Source name must be 100 characters or less";
            isValid = false;
        }
        else if (!IsEditMode && PluginState.Sources.Any(s =>
                     s.Name.Equals(_name, StringComparison.OrdinalIgnoreCase)))
        {
            _nameError = "A source with this name already exists";
            isValid = false;
        }

        // Validate URL
        if (string.IsNullOrWhiteSpace(_url))
        {
            _urlError = "Source URL is required";
            isValid = false;
        }
        else if (!Uri.TryCreate(_url, UriKind.Absolute, out var uri))
        {
            _urlError = "Please enter a valid URL";
            isValid = false;
        }
        else if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            _urlError = "URL must use HTTP or HTTPS protocol";
            isValid = false;
        }

        return isValid;
    }

    private async Task SaveAsync()
    {
        if (!ValidateForm())
        {
            return;
        }

        _isSaving = true;
        _generalError = null;
        StateHasChanged();

        try
        {
            var hasCredentials = !string.IsNullOrWhiteSpace(_username) ||
                                 !string.IsNullOrWhiteSpace(_password) ||
                                 !string.IsNullOrWhiteSpace(_apiKey);

            // For edit mode, preserve HasCredentials if no new credentials provided
            if (IsEditMode && !hasCredentials && Source?.HasCredentials == true)
            {
                hasCredentials = true;
            }

            var source = new PackageSourceModel
            {
                Name = _name.Trim(),
                Url = _url.Trim(),
                IsEnabled = _isEnabled,
                IsTrusted = _isTrusted,
                HasCredentials = hasCredentials,
                Username = string.IsNullOrWhiteSpace(_username) ? null : _username.Trim(),
                Password = string.IsNullOrWhiteSpace(_password) ? null : _password,
                ApiKey = string.IsNullOrWhiteSpace(_apiKey) ? null : _apiKey
            };

            bool success;
            if (IsEditMode)
            {
                success = await PluginState.UpdateSourceAsync(Source!.Name, source);
            }
            else
            {
                success = await PluginState.AddSourceAsync(source);
            }

            if (success)
            {
                var actionText = IsEditMode ? "updated" : "added";
                ToastService.ShowSuccess($"Successfully {actionText} source \"{source.Name}\"", "Source Saved");
                await OnSaved.InvokeAsync(source);
                await OnClose.InvokeAsync();
            }
            else
            {
                _generalError = PluginState.ErrorMessage ?? "Failed to save source";
                ToastService.ShowError(_generalError, "Save Failed");
            }
        }
        catch (Exception ex)
        {
            _generalError = $"An error occurred: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private async Task Cancel()
    {
        if (!_isSaving)
        {
            await OnClose.InvokeAsync();
        }
    }

    private async Task HandleOverlayClick()
    {
        if (!_isSaving)
        {
            await OnClose.InvokeAsync();
        }
    }
}
