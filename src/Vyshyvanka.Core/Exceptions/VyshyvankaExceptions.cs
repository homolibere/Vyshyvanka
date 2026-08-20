namespace Vyshyvanka.Core.Exceptions;

/// <summary>
/// Base exception for Vyshyvanka-specific errors.
/// </summary>
public abstract class VyshyvankaException : Exception
{
    /// <summary>Error code for programmatic handling.</summary>
    public string ErrorCode { get; }

    /// <summary>Initializes the exception with an error code and message.</summary>
    /// <param name="errorCode">Stable, machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    protected VyshyvankaException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    /// <summary>Initializes the exception with an error code, message, and underlying cause.</summary>
    /// <param name="errorCode">Stable, machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="innerException">The exception that caused this error.</param>
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
    /// <summary>Identifier of the workflow that could not be found.</summary>
    public Guid WorkflowId { get; }

    /// <summary>Initializes the exception for the specified workflow.</summary>
    /// <param name="workflowId">Identifier of the workflow that was not found.</param>
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
    /// <summary>The individual validation errors that caused the failure.</summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>Initializes the exception with the set of validation errors.</summary>
    /// <param name="errors">The validation errors describing why the workflow is invalid.</param>
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
    /// <summary>Identifier of the execution that could not be found.</summary>
    public Guid ExecutionId { get; }

    /// <summary>Initializes the exception for the specified execution.</summary>
    /// <param name="executionId">Identifier of the execution that was not found.</param>
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
    /// <summary>Identifier of the execution that failed.</summary>
    public Guid ExecutionId { get; }

    /// <summary>Identifier of the node where the failure occurred, when known. Null otherwise.</summary>
    public string? NodeId { get; }

    /// <summary>Initializes the exception for a failed execution.</summary>
    /// <param name="executionId">Identifier of the failed execution.</param>
    /// <param name="message">Human-readable description of the failure.</param>
    /// <param name="nodeId">Identifier of the node that failed, if known.</param>
    public WorkflowExecutionException(Guid executionId, string message, string? nodeId = null)
        : base("EXECUTION_FAILED", message)
    {
        ExecutionId = executionId;
        NodeId = nodeId;
    }

    /// <summary>Initializes the exception for a failed execution with an underlying cause.</summary>
    /// <param name="executionId">Identifier of the failed execution.</param>
    /// <param name="message">Human-readable description of the failure.</param>
    /// <param name="innerException">The exception that caused the failure.</param>
    /// <param name="nodeId">Identifier of the node that failed, if known.</param>
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
    /// <summary>Identifier of the credential that could not be found.</summary>
    public Guid CredentialId { get; }

    /// <summary>Initializes the exception for the specified credential.</summary>
    /// <param name="credentialId">Identifier of the credential that was not found.</param>
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
    /// <summary>The version the caller expected to update.</summary>
    public int ExpectedVersion { get; }

    /// <summary>The current version stored on the server.</summary>
    public int ActualVersion { get; }

    /// <summary>Initializes the exception describing the version mismatch.</summary>
    /// <param name="expectedVersion">The version the caller expected.</param>
    /// <param name="actualVersion">The current server-side version.</param>
    public VersionConflictException(int expectedVersion, int actualVersion)
        : base("VERSION_CONFLICT", $"Version conflict: expected {expectedVersion}, but current version is {actualVersion}")
    {
        ExpectedVersion = expectedVersion;
        ActualVersion = actualVersion;
    }
}
