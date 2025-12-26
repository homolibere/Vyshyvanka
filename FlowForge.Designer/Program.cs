using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowForge.Designer;
using FlowForge.Designer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with API base address
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });

// Register FlowForge services
builder.Services.AddScoped<FlowForgeApiClient>();
builder.Services.AddScoped<WorkflowStateService>();
builder.Services.AddScoped<PluginStateService>();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();
