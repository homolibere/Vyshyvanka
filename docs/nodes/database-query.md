# Database Query

Execute SQL queries against a database and return results as JSON.

- **Category:** Action
- **Type ID:** `database-query`
- **Icon:** `fa-solid fa-database`
- **Base class:** `BaseActionNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | No |
| Output | `output` | Results | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `connectionString` | string | Yes | Database connection string |
| `query` | string | Yes | SQL query to execute |
| `parameters` | object | No | Query parameters as key-value pairs |
| `queryType` | string | No | Query type: `select`, `execute`, `scalar` (default: `select`) |
| `timeout` | number | No | Command timeout in seconds (default: 30) |

## Query Types

| Type | Behavior | `data` field contains |
|------|----------|----------------------|
| `select` | Executes a query and returns rows | Array of row objects |
| `execute` | Executes a non-query (INSERT, UPDATE, DELETE) | `null` (check `rowsAffected`) |
| `scalar` | Returns a single value | The scalar value |

## Credentials

When a credential is attached, its fields are merged into the connection string:

| Credential Field | Connection String Property |
|-----------------|---------------------------|
| `username` | `User ID` |
| `password` | `Password` |
| `server` | `Server` |
| `database` | `Database` |

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Always `true` on success |
| `queryType` | string | The query type that was executed |
| `rowsAffected` | number | Number of rows affected (for `execute` type) |
| `data` | array/object/null | Query results (depends on query type) |

For `select` queries, `data` is an array of objects where keys are column names:

```json
{
  "success": true,
  "queryType": "select",
  "rowsAffected": 0,
  "data": [
    { "id": 1, "name": "Alice", "email": "alice@example.com" },
    { "id": 2, "name": "Bob", "email": "bob@example.com" }
  ]
}
```

## Error Handling

| Error | Output |
|-------|--------|
| Database error | `"Database error: <message>"` |
| Other | `"Query execution error: <message>"` |

## Usage

Use the Database Query node when you want to:

- Read data from a database for processing
- Insert or update records based on workflow data
- Run aggregation queries for reporting
- Check for the existence of records

## Expression Examples

```
{{$node.DatabaseQuery.data.data}}           // All result rows
{{$node.DatabaseQuery.data.data[0].name}}   // First row's name column
{{$node.DatabaseQuery.data.rowsAffected}}   // Rows affected by execute
```

## Notes

- Always use parameterized queries (`@paramName`) to prevent SQL injection. Never concatenate user input into the query string.
- The default database provider is SQLite. Other providers can be configured via the connection string format.
- Date/time values are returned in ISO 8601 format.
- Binary columns are returned as Base64-encoded strings.
- Parameter names can be specified with or without the `@` prefix — it is added automatically if missing.
