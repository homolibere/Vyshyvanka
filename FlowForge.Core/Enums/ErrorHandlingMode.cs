namespace FlowForge.Core.Enums;

/// <summary>
/// Defines how the workflow engine handles node execution errors.
/// </summary>
public enum ErrorHandlingMode
{
    /// <summary>Stop workflow execution on first node failure.</summary>
    StopOnFirstError,
    
    /// <summary>Continue with other branches, mark failed nodes.</summary>
    ContinueOnError,
    
    /// <summary>Retry failed nodes with exponential backoff.</summary>
    RetryWithBackoff
}
