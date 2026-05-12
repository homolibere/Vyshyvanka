namespace Vyshyvanka.Engine.Auth;

/// <summary>
/// Validates password complexity requirements.
/// </summary>
public static class PasswordValidator
{
    /// <summary>Default minimum password length.</summary>
    public const int DefaultMinLength = 8;

    /// <summary>
    /// Validates that a password meets complexity requirements:
    /// minimum length, at least one uppercase, one lowercase, one digit, and one special character.
    /// </summary>
    /// <param name="password">The password to validate.</param>
    /// <param name="minLength">Minimum required length (default: 8).</param>
    /// <returns>A validation result with success status and error message if invalid.</returns>
    public static PasswordValidationResult Validate(string password, int minLength = DefaultMinLength)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return PasswordValidationResult.Failure("Password is required");
        }

        if (password.Length < minLength)
        {
            return PasswordValidationResult.Failure(
                $"Password must be at least {minLength} characters long");
        }

        if (!password.Any(char.IsUpper))
        {
            return PasswordValidationResult.Failure(
                "Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            return PasswordValidationResult.Failure(
                "Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            return PasswordValidationResult.Failure(
                "Password must contain at least one digit");
        }

        if (!password.Any(IsSpecialCharacter))
        {
            return PasswordValidationResult.Failure(
                "Password must contain at least one special character");
        }

        return PasswordValidationResult.Success();
    }

    private static bool IsSpecialCharacter(char c) =>
        !char.IsLetterOrDigit(c);
}

/// <summary>
/// Result of a password validation check.
/// </summary>
public record PasswordValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static PasswordValidationResult Success() => new() { IsValid = true };
    public static PasswordValidationResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
}
