namespace Vyshyvanka.Core.Enums;

/// <summary>
/// Supported database providers for the Vyshyvanka persistence layer.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>SQLite — lightweight, file-based. Default for local development.</summary>
    Sqlite,

    /// <summary>PostgreSQL — full-featured relational database for production deployments.</summary>
    PostgreSql
}
