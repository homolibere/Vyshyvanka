using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Vyshyvanka.Designer;
using Vyshyvanka.Designer.Services;

// Force InvariantCulture so SVG attributes always use '.' as decimal separator
// regardless of the browser's locale (which may use ',' — breaking SVG rendering).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API base address using service discovery with fallback to appsettings
var apiBaseAddress = ApiUrlResolver.ResolveApiUrl(builder.Configuration, builder.HostEnvironment.BaseAddress);

// Register browser storage service for localStorage access
builder.Services.AddScoped<BrowserStorageService>();

// Register AuthStateService as singleton to persist auth state across navigation
builder.Services.AddSingleton<AuthStateService>();

// Register the authorization message handler (depends on AuthStateService singleton)
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Configure HttpClient with authorization handler
builder.Services.AddScoped(sp =>
{
    var authState = sp.GetRequiredService<AuthStateService>();
    var handler = new AuthorizationMessageHandler(authState)
    {
        InnerHandler = new HttpClientHandler()
    };
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseAddress) };
});

// Register AuthService as scoped (depends on HttpClient and AuthStateService)
builder.Services.AddScoped<AuthService>();

// Register Vyshyvanka services
builder.Services.AddScoped<WorkflowApiClient>(sp => new WorkflowApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<PackageApiClient>(sp => new PackageApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<CredentialApiClient>(sp => new CredentialApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<ApiKeyApiClient>(sp => new ApiKeyApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<FolderApiClient>(sp => new FolderApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<TeamApiClient>(sp => new TeamApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<UserApiClient>(sp => new UserApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<SharingApiClient>(sp => new SharingApiClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddScoped<WorkflowStore>();
builder.Services.AddScoped<CanvasStateService>();
builder.Services.AddScoped<WorkflowValidationService>();
builder.Services.AddScoped<ExecutionStateService>();
builder.Services.AddScoped<WorkflowEditService>();
builder.Services.AddScoped<ExpressionAutocompleteService>();
builder.Services.AddScoped<PluginStateService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<TokenRefreshService>();
builder.Services.AddScoped<ExpressionDragService>();

var host = builder.Build();

// Initialize auth state from browser storage
var authState = host.Services.GetRequiredService<AuthStateService>();
using (var scope = host.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<BrowserStorageService>();
    authState.SetStorageService(storage);
    await authState.InitializeAsync();

    // Initialize theme from browser storage (deferred — async JS fetch doesn't work here)
    // Theme initialization moved to App.razor OnAfterRenderAsync where async JS interop is reliable.

    // Start proactive token refresh (subscribes to auth state changes)
    var tokenRefresh = scope.ServiceProvider.GetRequiredService<TokenRefreshService>();
    tokenRefresh.Start();
}

await host.RunAsync();
