using System.Reflection;

namespace Vyshyvanka.Core.Interfaces;

/// <summary>
/// Validates plugin assemblies and their node definitions.
/// </summary>
public interface IPluginValidator
{
    /// <summary>
    /// Validates a plugin assembly.
    /// </summary>
    /// <param name="assembly">The assembly to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    PluginValidationResult ValidatePlugin(Assembly assembly);
    
    /// <summary>
    /// Validates a node type from a plugin.
    /// </summary>
    /// <param name="nodeType">The node type to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    PluginValidationResult ValidateNodeType(Type nodeType);
}

/// <summary>
/// Result of plugin validation.
/// </summary>
public record PluginValidationResult
{
    /// <summary>Whether the validation passed.</summary>
    public bool IsValid => Errors.Count == 0;
    
    /// <summary>Validation errors found.</summary>
    public IReadOnlyList<PluginValidationError> Errors { get; init; } = [];
    
    /// <summary>Warnings that don't prevent loading.</summary>
    public IReadOnlyList<PluginValidationWarning> Warnings { get; init; } = [];
    
    /// <summary>Creates a successful validation result.</summary>
    public static PluginValidationResult Success() => new();
    
    /// <summary>Creates a failed validation result with errors.</summary>
    public static PluginValidationResult Failure(params PluginValidationError[] errors) => 
        new() { Errors = errors };
    
    /// <summary>Creates a failed validation result with a single error.</summary>
    public static PluginValidationResult Failure(string code, string message) => 
        new() { Errors = [new PluginValidationError(code, message)] };
}

/// <summary>
/// A validation error that prevents plugin loading.
/// </summary>
public record PluginValidationError(string Code, string Message)
{
    /// <summary>Optional context about where the error occurred.</summary>
    public string? Context { get; init; }
}

/// <summary>
/// A validation warning that doesn't prevent plugin loading.
/// </summary>
public record PluginValidationWarning(string Code, string Message)
{
    /// <summary>Optional context about where the warning occurred.</summary>
    public string? Context { get; init; }
}
