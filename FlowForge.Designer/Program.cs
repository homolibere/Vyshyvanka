using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowForge.Designer;
using FlowForge.Designer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure API base address
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;

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

// Register FlowForge services
builder.Services.AddScoped<FlowForgeApiClient>();
builder.Services.AddScoped<WorkflowStateService>();
builder.Services.AddScoped<PluginStateService>();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();
