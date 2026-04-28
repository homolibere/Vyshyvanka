using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Vyshyvanka.Designer;
using Vyshyvanka.Designer.Services;

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
builder.Services.AddScoped<VyshyvankaApiClient>();
builder.Services.AddScoped<WorkflowStateService>();
builder.Services.AddScoped<PluginStateService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ThemeService>();

var host = builder.Build();

// Initialize auth state from browser storage
var authState = host.Services.GetRequiredService<AuthStateService>();
using (var scope = host.Services.CreateScope())
{
    var storage = scope.ServiceProvider.GetRequiredService<BrowserStorageService>();
    authState.SetStorageService(storage);
    await authState.InitializeAsync();

    // Initialize theme from browser storage
    var themeService = scope.ServiceProvider.GetRequiredService<ThemeService>();
    await themeService.InitializeAsync();
}

await host.RunAsync();
