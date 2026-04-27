using FlowForge.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class UserMenu : ComponentBase, IDisposable
{
    [Inject]
    private AuthService AuthService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    protected override void OnInitialized()
    {
        AuthService.OnAuthStateChanged += StateHasChanged;
    }

    private string GetDisplayName()
    {
        var user = AuthService.CurrentUser;
        if (user is null) return "";
        return !string.IsNullOrWhiteSpace(user.DisplayName) ? user.DisplayName : user.Email;
    }

    private string GetInitials()
    {
        var name = GetDisplayName();
        if (string.IsNullOrWhiteSpace(name)) return "?";

        var parts = name.Split(' ', '@');
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name[..1].ToUpperInvariant();
    }

    private void HandleLogout()
    {
        AuthService.Logout();
        Navigation.NavigateTo("/login");
    }

    public void Dispose()
    {
        AuthService.OnAuthStateChanged -= StateHasChanged;
    }
}
