using System.Text;
using FlowForge.Api.Authorization;
using FlowForge.Api.Middleware;
using FlowForge.Api.Services;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Auth;
using FlowForge.Engine.Credentials;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Expressions;
using FlowForge.Engine.Packages;
using FlowForge.Engine.Persistence;
using FlowForge.Engine.Plugins;
using FlowForge.Engine.Registry;
using FlowForge.Engine.Validation;
using Microsoft.AspNetCore.Authentication;
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
        // Register HTTP context accessor and current user service
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Register core services
        services.AddSingleton<INodeRegistry>(_ =>
        {
            var registry = new NodeRegistry();
            registry.RegisterFromAssembly(typeof(NodeRegistry).Assembly);
            return registry;
        });
        services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
        services.AddSingleton<WorkflowValidator>();

        // Register the base workflow engine
        services.AddScoped<WorkflowEngine>(sp =>
        {
            var nodeRegistry = sp.GetRequiredService<INodeRegistry>();
            var expressionEvaluator = sp.GetRequiredService<IExpressionEvaluator>();
            var pluginHost = sp.GetService<IPluginHost>();
            return new WorkflowEngine(nodeRegistry, expressionEvaluator, pluginHost);
        });

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

        var credentialStorageSettings = new CredentialStorageSettings();
        configuration.GetSection("CredentialStorage").Bind(credentialStorageSettings);
        services.AddSingleton(credentialStorageSettings);

        switch (credentialStorageSettings.Provider)
        {
            case CredentialStorageProvider.BuiltIn:
                services.AddSingleton<ICredentialEncryption>(_ =>
                {
                    var encryptionKey = configuration["FlowForge:EncryptionKey"] ??
                                        "Rk93Rm9yZ2VEZXZLZXkxMjM0NTY3ODkwMTIzNDU2Nzg=";
                    return new AesCredentialEncryption(encryptionKey);
                });
                services.AddScoped<ICredentialService, CredentialService>();
                break;

            case CredentialStorageProvider.HashiCorpVault:
            case CredentialStorageProvider.OpenBao:
                if (string.IsNullOrWhiteSpace(credentialStorageSettings.Url))
                {
                    throw new InvalidOperationException(
                        $"CredentialStorage:Url is required when using {credentialStorageSettings.Provider}");
                }

                services.AddSingleton<IVaultClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<VaultClient>>();
                    return new VaultClient(credentialStorageSettings, logger);
                });
                services.AddScoped<ICredentialService, VaultCredentialService>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported credential storage provider: {credentialStorageSettings.Provider}");
        }

        // Bind authentication settings
        var authSettings = new AuthenticationSettings();
        configuration.GetSection("Authentication").Bind(authSettings);
        services.AddSingleton(authSettings);

        services.AddScoped<IUserRepository, UserRepository>();

        // Register audit logging
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Register API key services
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IApiKeyService, ApiKeyService>();

        // Register NuGet package manager services
        services.AddFlowForgePackageServices(configuration);

        // Branch authentication setup based on the configured provider
        switch (authSettings.Provider)
        {
            case AuthenticationProvider.BuiltIn:
                services.AddFlowForgeBuiltInAuth(configuration);
                break;

            case AuthenticationProvider.Keycloak:
            case AuthenticationProvider.Authentik:
                services.AddFlowForgeOidcAuth(authSettings);
                break;

            case AuthenticationProvider.Ldap:
                services.AddFlowForgeLdapAuth(configuration, authSettings);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported authentication provider: {authSettings.Provider}");
        }

        services.AddAuthorization(options => { options.AddFlowForgePolicies(); });

        return services;
    }

    /// <summary>
    /// Configures built-in JWT + API key authentication (the original default).
    /// </summary>
    private static void AddFlowForgeBuiltInAuth(this IServiceCollection services, IConfiguration configuration)
    {
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
        services.AddScoped<IAuthService, AuthService>();

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

                // Forward to API key authentication if X-API-Key header is present
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey("X-API-Key"))
                    {
                        return ApiKeyAuthenticationDefaults.AuthenticationScheme;
                    }

                    return null;
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                            { code = "UNAUTHORIZED", message = "Authentication required" });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { code = "FORBIDDEN", message = "Access denied" });
                    }
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { });
    }

    /// <summary>
    /// Configures OIDC-based authentication for Keycloak or Authentik.
    /// The API validates access tokens issued by the external provider.
    /// </summary>
    private static void AddFlowForgeOidcAuth(this IServiceCollection services, AuthenticationSettings authSettings)
    {
        if (string.IsNullOrWhiteSpace(authSettings.Authority))
        {
            throw new InvalidOperationException(
                $"Authentication:Authority is required when using {authSettings.Provider}");
        }

        // Register OIDC user provisioning and claims transformation
        services.AddScoped<IOidcUserProvisioningService, OidcUserProvisioningService>();
        services.AddScoped<IClaimsTransformation, OidcClaimsTransformation>();

        var audience = authSettings.Audience ?? authSettings.ClientId;

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.Authority = authSettings.Authority;
                options.RequireHttpsMetadata = authSettings.RequireHttpsMetadata;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // Forward to API key authentication if X-API-Key header is present
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey("X-API-Key"))
                    {
                        return ApiKeyAuthenticationDefaults.AuthenticationScheme;
                    }

                    return null;
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                            { code = "UNAUTHORIZED", message = "Authentication required" });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { code = "FORBIDDEN", message = "Access denied" });
                    }
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { });
    }

    /// <summary>
    /// Configures LDAP authentication. Credentials are verified against the LDAP directory,
    /// but sessions use locally-issued JWT tokens (same as built-in).
    /// </summary>
    private static void AddFlowForgeLdapAuth(this IServiceCollection services, IConfiguration configuration,
        AuthenticationSettings authSettings)
    {
        if (authSettings.Ldap is null || string.IsNullOrWhiteSpace(authSettings.Ldap.Host))
        {
            throw new InvalidOperationException(
                "Authentication:Ldap:Host is required when using LDAP authentication");
        }

        // LDAP uses local JWT tokens, same as built-in
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

        // Register LDAP services
        services.AddScoped<ILdapAuthenticationService, LdapAuthenticationService>();
        services.AddScoped<IAuthService, LdapAuthService>();

        // Same JWT bearer + API key setup as built-in
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

                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey("X-API-Key"))
                    {
                        return ApiKeyAuthenticationDefaults.AuthenticationScheme;
                    }

                    return null;
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                            { code = "UNAUTHORIZED", message = "Authentication required" });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new { code = "FORBIDDEN", message = "Access denied" });
                    }
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.AuthenticationScheme,
                _ => { });
    }

    /// <summary>
    /// Adds FlowForge NuGet package manager services to the service collection.
    /// </summary>
    public static IServiceCollection AddFlowForgePackageServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind package options from configuration
        var packageOptions = new PackageOptions();
        configuration.GetSection("FlowForge:Packages").Bind(packageOptions);

        // Apply environment variable overrides
        var cacheDir = Environment.GetEnvironmentVariable("FLOWFORGE_PACKAGES_CACHEDIR");
        if (!string.IsNullOrWhiteSpace(cacheDir))
        {
            packageOptions.CacheDirectory = cacheDir;
        }

        var manifestPath = Environment.GetEnvironmentVariable("FLOWFORGE_PACKAGES_MANIFESTPATH");
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            packageOptions.ManifestPath = manifestPath;
        }

        var requireSigned = Environment.GetEnvironmentVariable("FLOWFORGE_PACKAGES_REQUIRESIGNED");
        if (bool.TryParse(requireSigned, out var requireSignedValue))
        {
            packageOptions.RequireSignedPackages = requireSignedValue;
        }

        services.AddSingleton(packageOptions);

        // Register ManifestManager
        services.AddSingleton<IManifestManager>(sp =>
        {
            var options = sp.GetRequiredService<PackageOptions>();
            var logger = sp.GetService<ILogger<ManifestManager>>();
            return new ManifestManager(options.ManifestPath, options.CacheDirectory, logger);
        });

        // Register PackageCache
        services.AddSingleton<IPackageCache>(sp =>
        {
            var options = sp.GetRequiredService<PackageOptions>();
            var logger = sp.GetService<ILogger<PackageCache>>();
            return new PackageCache(options.CacheDirectory, logger);
        });

        // Register PackageSourceService
        services.AddSingleton<IPackageSourceService>(sp =>
        {
            var manifestManager = sp.GetRequiredService<IManifestManager>();
            var credentialEncryption = sp.GetService<ICredentialEncryption>();
            var logger = sp.GetService<ILogger<PackageSourceService>>();
            return new PackageSourceService(manifestManager, credentialEncryption, logger);
        });

        // Register DependencyResolver
        services.AddSingleton<IDependencyResolver>(sp =>
        {
            var sourceService = sp.GetRequiredService<IPackageSourceService>();
            var logger = sp.GetService<ILogger<DependencyResolver>>();
            return new DependencyResolver(sourceService, logger);
        });

        // Register PluginValidator
        services.AddSingleton<IPluginValidator, PluginValidator>();

        // Register PluginLoader
        services.AddSingleton<IPluginLoader>(sp =>
        {
            var validator = sp.GetRequiredService<IPluginValidator>();
            var logger = sp.GetService<ILogger<PluginLoader>>();
            return new PluginLoader(validator, logger);
        });

        // Register PluginHost for isolated plugin execution
        services.AddSingleton<IPluginHost>(sp =>
        {
            var pluginLoader = sp.GetRequiredService<IPluginLoader>();
            var logger = sp.GetService<ILogger<PluginHost>>();
            return new PluginHost(pluginLoader, logger);
        });

        // Register NuGetPackageManager
        services.AddSingleton<INuGetPackageManager>(sp =>
        {
            var sourceService = sp.GetRequiredService<IPackageSourceService>();
            var manifestManager = sp.GetRequiredService<IManifestManager>();
            var dependencyResolver = sp.GetRequiredService<IDependencyResolver>();
            var packageCache = sp.GetRequiredService<IPackageCache>();
            var pluginLoader = sp.GetRequiredService<IPluginLoader>();
            var pluginValidator = sp.GetRequiredService<IPluginValidator>();
            var nodeRegistry = sp.GetRequiredService<INodeRegistry>();
            var options = sp.GetRequiredService<PackageOptions>();
            var logger = sp.GetService<ILogger<NuGetPackageManager>>();

            return new NuGetPackageManager(
                sourceService,
                manifestManager,
                dependencyResolver,
                packageCache,
                pluginLoader,
                pluginValidator,
                nodeRegistry,
                null, // IWorkflowRepository is scoped, pass null for singleton registration
                options,
                logger);
        });

        return services;
    }
}
