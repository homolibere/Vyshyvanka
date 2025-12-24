using System.Text;
using FlowForge.Api.Authorization;
using FlowForge.Api.Middleware;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Auth;
using FlowForge.Engine.Credentials;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Expressions;
using FlowForge.Engine.Persistence;
using FlowForge.Engine.Registry;
using FlowForge.Engine.Validation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FlowForge.Api.Extensions;

/// <summary>
/// Extension methods for configuring FlowForge services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds FlowForge core services to the service collection.
    /// </summary>
    public static IServiceCollection AddFlowForgeServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register core services
        services.AddSingleton<INodeRegistry>(sp =>
        {
            var registry = new NodeRegistry();
            registry.RegisterFromAssembly(typeof(NodeRegistry).Assembly);
            return registry;
        });
        services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
        services.AddSingleton<WorkflowValidator>();

        // Register the base workflow engine
        services.AddScoped<WorkflowEngine>();

        // Register persistence
        var connectionString = configuration.GetConnectionString("FlowForge") ?? "Data Source=flowforge.db";
        services.AddDbContext<FlowForgeDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IExecutionRepository, ExecutionRepository>();

        // Register the persistent workflow engine as the primary IWorkflowEngine
        services.AddScoped<IWorkflowEngine>(sp =>
        {
            var innerEngine = sp.GetRequiredService<WorkflowEngine>();
            var repository = sp.GetRequiredService<IExecutionRepository>();
            return new PersistentWorkflowEngine(innerEngine, repository);
        });

        // Register credential services
        services.AddScoped<ICredentialRepository, CredentialRepository>();
        services.AddSingleton<ICredentialEncryption>(sp =>
        {
            var encryptionKey = configuration["FlowForge:EncryptionKey"] ?? "DefaultEncryptionKey123456789012";
            return new AesCredentialEncryption(encryptionKey);
        });
        services.AddScoped<ICredentialService, CredentialService>();

        // Register authentication services
        var jwtSettings = new JwtSettings
        {
            SecretKey = configuration["Jwt:SecretKey"] ?? "FlowForgeDefaultSecretKey123456789012345678901234567890",
            Issuer = configuration["Jwt:Issuer"] ?? "FlowForge",
            Audience = configuration["Jwt:Audience"] ?? "FlowForge",
            AccessTokenExpirationMinutes =
                int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var minutes) ? minutes : 60,
            RefreshTokenExpirationDays =
                int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 7
        };
        services.AddSingleton(jwtSettings);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthService, AuthService>();

        // Register audit logging
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Register API key services
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyService, ApiKeyService>();

        // Configure authentication with both JWT and API key
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { });

        services.AddAuthorization(options => { options.AddFlowForgePolicies(); });

        return services;
    }
}
