# File Operations

Read, write, and manage files on the file system.

- **Category:** Action
- **Type ID:** `file-operations`
- **Icon:** `fa-solid fa-file`
- **Base class:** `BaseActionNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | No |
| Output | `output` | Result | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `operation` | string | Yes | Operation to perform. Options: `read`, `write`, `append`, `delete`, `exists`, `copy`, `move` |
| `path` | string | Yes | File path for the operation |
| `content` | string | No | Content to write (for `write`/`append` operations) |
| `destinationPath` | string | No | Destination path (for `copy`/`move` operations) |
| `encoding` | string | No | Text encoding. Options: `utf-8`, `ascii`, `utf-16` (default: `utf-8`) |
| `isBinary` | boolean | No | Treat file as binary (default: `false`) |
| `createDirectory` | boolean | No | Create parent directories if they don't exist (default: `false`) |
| `overwrite` | boolean | No | Overwrite existing files (default: `false`) |

## Operations

### read

Reads the content of a file.

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"read"` |
| `path` | string | File path |
| `content` | string | File content (Base64 if binary) |
| `size` | number | File size in bytes |
| `lastModified` | string | Last modification time (ISO 8601) |
| `isBinary` | boolean | Whether file was read as binary |

### write

Writes content to a file. Fails if file exists and `overwrite` is `false`.

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"write"` |
| `path` | string | File path |
| `size` | number | Written file size in bytes |
| `lastModified` | string | Last modification time (ISO 8601) |

### append

Appends content to an existing file (creates it if it doesn't exist).

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"append"` |
| `path` | string | File path |
| `size` | number | File size after append |
| `lastModified` | string | Last modification time (ISO 8601) |

### delete

Deletes a file.

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"delete"` |
| `path` | string | File path |
| `deleted` | boolean | `true` |

### exists

Checks whether a file exists.

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"exists"` |
| `path` | string | File path |
| `exists` | boolean | Whether the file exists |
| `size` | number/null | File size (if exists) |
| `lastModified` | string/null | Last modification time (if exists) |

### copy

Copies a file to a new location.

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"copy"` |
| `sourcePath` | string | Source file path |
| `destinationPath` | string | Destination file path |
| `size` | number | Copied file size |

### move

Moves a file to a new location.

**Output:**

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | `true` |
| `operation` | string | `"move"` |
| `sourcePath` | string | Original file path |
| `destinationPath` | string | New file path |
| `size` | number | File size |

## Error Handling

| Error | Output |
|-------|--------|
| File not found (read/delete) | `"File not found: <path>"` |
| File exists (write without overwrite) | `"File already exists: <path>. Set overwrite to true to replace."` |
| Missing destination (copy/move) | `"Destination path is required for <op> operation"` |
| I/O error | `"File I/O error: <message>"` |
| Permission denied | `"Access denied: <message>"` |
| Other | `"File operation error: <message>"` |

## Usage

Use the File Operations node when you want to:

- Read configuration or data files for processing
- Write workflow results to disk
- Generate reports or export data
- Manage file-based integrations
- Check for file existence before processing

## Expression Examples

```
{{$node.FileOps.data.content}}       // File content (read)
{{$node.FileOps.data.exists}}        // Whether file exists
{{$node.FileOps.data.size}}          // File size in bytes
{{$node.FileOps.data.lastModified}}  // Last modification time
```

## Notes

- Binary files are read as Base64-encoded strings and must be written as Base64.
- The `createDirectory` option only applies to `write`, `append`, `copy`, and `move` operations.
- For `copy` and `move`, the `createDirectory` option creates the destination's parent directory.
- Supported encodings: `utf-8` (default), `ascii`, `utf-16`/`unicode`, `utf-32`.
