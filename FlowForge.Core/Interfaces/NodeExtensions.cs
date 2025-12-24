namespace FlowForge.Core.Interfaces;

/// <summary>
/// Extension methods for node execution.
/// </summary>
public static class NodeExtensions
{
    /// <summary>
    /// Gets the decrypted credentials for a node during execution.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="input">The node input containing the credential ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The credential values, or null if no credential is configured.</returns>
    public static async Task<IDictionary<string, string>?> GetNodeCredentialsAsync(
        this IExecutionContext context,
        NodeInput input,
        CancellationToken cancellationToken = default)
    {
        if (!input.CredentialId.HasValue)
        {
            return null;
        }
        
        return await context.Credentials.GetCredentialAsync(input.CredentialId.Value, cancellationToken);
    }
}
