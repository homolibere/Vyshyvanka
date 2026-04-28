namespace Vyshyvanka.Core.Models;

/// <summary>
/// Result of workflow validation.
/// </summary>
public record ValidationResult
{
    /// <summary>Whether the workflow is valid.</summary>
    public bool IsValid { get; init; }
    
    /// <summary>Validation errors found.</summary>
    public List<ValidationError> Errors { get; init; } = [];
    
    /// <summary>Creates a successful validation result.</summary>
    public static ValidationResult Success() => new() { IsValid = true };
    
    /// <summary>Creates a failed validation result with errors.</summary>
    public static ValidationResult Failure(params ValidationError[] errors) => 
        new() { IsValid = false, Errors = [..errors] };
}

/// <summary>
/// A validation error.
/// </summary>
public record ValidationError
{
    /// <summary>JSON path to the invalid element.</summary>
    public string Path { get; init; } = string.Empty;
    
    /// <summary>Error message.</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>Error code for programmatic handling.</summary>
    public string ErrorCode { get; init; } = string.Empty;
}
