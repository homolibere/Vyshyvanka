using FlowForge.Api.Extensions;
using FlowForge.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add FlowForge services
builder.Services.AddFlowForgeServices(builder.Configuration);

// Add API services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseErrorHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
