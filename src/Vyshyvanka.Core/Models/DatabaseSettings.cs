using Vyshyvanka.Core.Enums;

namespace Vyshyvanka.Core.Models;

/// <summary>
/// Configuration for the active database provider.
/// Bind from the "Database" section in appsettings.json.
/// </summary>
public record DatabaseSettings
{
    /// <summary>Which database provider to use.</summary>
    public DatabaseProvider Provider { get; init; } = DatabaseProvider.Sqlite;
}
