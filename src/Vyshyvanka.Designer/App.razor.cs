using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Vyshyvanka.Designer;

public partial class App
{
    [Inject]
    private AuthService AuthService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ThemeService ThemeService { get; set; } = null!;

    private static readonly HashSet<string> PublicRoutes = ["/login"];

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await ThemeService.InitializeAsync();
        }
    }

    private void OnNavigateAsync(NavigationContext context)
    {
        var path = "/" + context.Path.TrimStart('/').ToLowerInvariant();

        if (!AuthService.IsAuthenticated && !PublicRoutes.Contains(path))
        {
            Navigation.NavigateTo("/login");
        }
    }
}
