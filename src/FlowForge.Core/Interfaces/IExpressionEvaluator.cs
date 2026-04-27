namespace FlowForge.Core.Interfaces;

/// <summary>
/// Evaluates expressions within workflow execution context.
/// </summary>
public interface IExpressionEvaluator
{
    /// <summary>Evaluates an expression and returns the result.</summary>
    object? Evaluate(string expression, IExecutionContext context);
    
    /// <summary>Attempts to evaluate an expression, returning success status.</summary>
    bool TryEvaluate(string expression, IExecutionContext context, out object? result, out string? error);
    
    /// <summary>Validates an expression without evaluating it.</summary>
    ExpressionValidationResult Validate(string expression);
}

/// <summary>
/// Result of expression validation.
/// </summary>
public record ExpressionValidationResult
{
    /// <summary>Whether the expression is valid.</summary>
    public bool IsValid { get; init; }
    
    /// <summary>Validation errors, if any.</summary>
    public List<ExpressionError> Errors { get; init; } = [];
}

/// <summary>
/// Error in an expression.
/// </summary>
public record ExpressionError
{
    /// <summary>Position in the expression where error occurred.</summary>
    public int Position { get; init; }
    
    /// <summary>Error message.</summary>
    public string Message { get; init; } = string.Empty;
}
