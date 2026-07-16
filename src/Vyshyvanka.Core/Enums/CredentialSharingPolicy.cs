namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Determines how credentials are resolved when a shared workflow is executed
/// by someone other than the owner.
/// </summary>
public enum CredentialSharingPolicy
{
    /// <summary>
    /// The workflow owner's credentials are used for execution.
    /// The owner explicitly grants this when sharing.
    /// </summary>
    UseOwnerCredentials = 0,

    /// <summary>
    /// The executor must provide their own credentials.
    /// Execution fails if the executor has no matching credentials configured.
    /// </summary>
    RequireOwnCredentials = 1
}
