using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Pages;

public partial class Login
{
    [Inject]
    private AuthService AuthService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private LoginModel _model = new();
    private string? _errorMessage;
    private bool _isLoading;

    protected override void OnInitialized()
    {
        if (AuthService.IsAuthenticated)
        {
            Navigation.NavigateTo("/");
        }
    }

    private async Task HandleLogin()
    {
        _errorMessage = null;
        _isLoading = true;

        try
        {
            var (success, error) = await AuthService.LoginAsync(_model.Email, _model.Password);

            if (success)
            {
                Navigation.NavigateTo("/");
            }
            else
            {
                _errorMessage = error ?? "Login failed";
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private class LoginModel
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
