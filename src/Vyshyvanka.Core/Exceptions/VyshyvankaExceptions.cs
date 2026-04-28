namespace Vyshyvanka.Core.Exceptions;

/// <summary>
/// Base exception for Vyshyvanka-specific errors.
/// </summary>
public abstract class VyshyvankaException : Exception
{
    /// <summary>Error code for programmatic handling.</summary>
    public string ErrorCode { get; }

    protected VyshyvankaException(string errorCode, string message) 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    protected VyshyvankaException(string errorCode, string message, Exception innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when a workflow is not found.
/// </summary>
public class WorkflowNotFoundException : VyshyvankaException
{
    public Guid WorkflowId { get; }

    public WorkflowNotFoundException(Guid workflowId)
        : base("WORKFLOW_NOT_FOUND", $"Workflow with ID '{workflowId}' was not found")
    {
        WorkflowId = workflowId;
    }
}

/// <summary>
/// Exception thrown when workflow validation fails.
/// </summary>
public class WorkflowValidationException : VyshyvankaException
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public WorkflowValidationException(IEnumerable<string> errors)
        : base("WORKFLOW_VALIDATION_FAILED", "Workflow validation failed")
    {
        ValidationErrors = errors.ToList();
    }
}

/// <summary>
/// Exception thrown when an execution is not found.
/// </summary>
public class ExecutionNotFoundException : VyshyvankaException
{
    public Guid ExecutionId { get; }

    public ExecutionNotFoundException(Guid executionId)
        : base("EXECUTION_NOT_FOUND", $"Execution with ID '{executionId}' was not found")
    {
        ExecutionId = executionId;
    }
}

/// <summary>
/// Exception thrown when workflow execution fails.
/// </summary>
public class WorkflowExecutionException : VyshyvankaException
{
    public Guid ExecutionId { get; }
    public string? NodeId { get; }

    public WorkflowExecutionException(Guid executionId, string message, string? nodeId = null)
        : base("EXECUTION_FAILED", message)
    {
        ExecutionId = executionId;
        NodeId = nodeId;
    }

    public WorkflowExecutionException(Guid executionId, string message, Exception innerException, string? nodeId = null)
        : base("EXECUTION_FAILED", message, innerException)
    {
        ExecutionId = executionId;
        NodeId = nodeId;
    }
}

/// <summary>
/// Exception thrown when a credential is not found.
/// </summary>
public class CredentialNotFoundException : VyshyvankaException
{
    public Guid CredentialId { get; }

    public CredentialNotFoundException(Guid credentialId)
        : base("CREDENTIAL_NOT_FOUND", $"Credential with ID '{credentialId}' was not found")
    {
        CredentialId = credentialId;
    }
}

/// <summary>
/// Exception thrown when there's a version conflict (optimistic concurrency).
/// </summary>
public class VersionConflictException : VyshyvankaException
{
    public int ExpectedVersion { get; }
    public int ActualVersion { get; }

    public VersionConflictException(int expectedVersion, int actualVersion)
        : base("VERSION_CONFLICT", $"Version conflict: expected {expectedVersion}, but current version is {actualVersion}")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
