using System.Text;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Nodes.Actions;

/// <summary>
/// An action node that performs file system operations.
/// Supports read, write, append, delete, copy, and move operations for text and binary files.
/// </summary>
[NodeDefinition(
    Name = "File Operations",
    Description = "Read, write, and manage files on the file system",
    Icon = "file")]
[NodeInput("input", DisplayName = "Input", IsRequired = false)]
[NodeOutput("output", DisplayName = "Result")]
[ConfigurationProperty("operation", "string",
    Description = "Operation: read, write, append, delete, exists, copy, move", IsRequired = true)]
[ConfigurationProperty("path", "string", Description = "File path for the operation", IsRequired = true)]
[ConfigurationProperty("content", "string", Description = "Content to write (for write/append operations)")]
[ConfigurationProperty("destinationPath", "string", Description = "Destination path (for copy/move operations)")]
[ConfigurationProperty("encoding", "string", Description = "Text encoding (utf-8, ascii, utf-16)")]
[ConfigurationProperty("isBinary", "boolean", Description = "Treat file as binary")]
[ConfigurationProperty("createDirectory", "boolean", Description = "Create parent directories if they don't exist")]
[ConfigurationProperty("overwrite", "boolean", Description = "Overwrite existing files")]
public class FileOperationsNode : BaseActionNode
{
    private readonly string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "file-operations";

    /// <summary>
    /// Creates a new FileOperationsNode.
    /// </summary>
    public FileOperationsNode()
    {
    }

    /// <inheritdoc />
    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var operation = GetRequiredConfigValue<string>(input, "operation").ToLowerInvariant();
            var path = GetRequiredConfigValue<string>(input, "path");
            var content = GetConfigValue<string>(input, "content");
            var destinationPath = GetConfigValue<string>(input, "destinationPath");
            var encodingName = GetConfigValue<string>(input, "encoding") ?? "utf-8";
            var isBinary = GetConfigValue<bool?>(input, "isBinary") ?? false;
            var createDirectory = GetConfigValue<bool?>(input, "createDirectory") ?? false;
            var overwrite = GetConfigValue<bool?>(input, "overwrite") ?? false;

            var encoding = GetEncoding(encodingName);

            // Ensure parent directory exists if requested
            if (createDirectory && operation is "write" or "append" or "copy" or "move")
            {
                var targetPath = operation is "copy" or "move" ? destinationPath : path;
                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    var directory = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
            }

            return operation switch
            {
                "read" => await ReadFileAsync(path, isBinary, encoding, context.CancellationToken),
                "write" => await WriteFileAsync(path, content, isBinary, encoding, overwrite,
                    context.CancellationToken),
                "append" => await AppendFileAsync(path, content, encoding, context.CancellationToken),
                "delete" => DeleteFile(path),
                "exists" => CheckFileExists(path),
                "copy" => CopyFile(path, destinationPath, overwrite),
                "move" => MoveFile(path, destinationPath, overwrite),
                _ => FailureOutput($"Unknown operation: {operation}")
            };
        }
        catch (IOException ex)
        {
            return FailureOutput($"File I/O error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailureOutput($"Access denied: {ex.Message}");
        }
        catch (Exception ex)
        {
            return FailureOutput($"File operation error: {ex.Message}");
        }
    }

    private static async Task<NodeOutput> ReadFileAsync(
        string path,
        bool isBinary,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return FailureOutput($"File not found: {path}");
        }

        var fileInfo = new FileInfo(path);

        object content;
        if (isBinary)
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            content = Convert.ToBase64String(bytes);
        }
        else
        {
            content = await File.ReadAllTextAsync(path, encoding, cancellationToken);
        }

        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "read",
            ["path"] = path,
            ["content"] = content,
            ["size"] = fileInfo.Length,
            ["lastModified"] = fileInfo.LastWriteTimeUtc.ToString("O"),
            ["isBinary"] = isBinary
        };

        return SuccessOutput(result);
    }

    private static async Task<NodeOutput> WriteFileAsync(
        string path,
        string? content,
        bool isBinary,
        Encoding encoding,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        if (File.Exists(path) && !overwrite)
        {
            return FailureOutput($"File already exists: {path}. Set overwrite to true to replace.");
        }

        if (isBinary && content is not null)
        {
            var bytes = Convert.FromBase64String(content);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        }
        else
        {
            await File.WriteAllTextAsync(path, content ?? string.Empty, encoding, cancellationToken);
        }

        var fileInfo = new FileInfo(path);
        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "write",
            ["path"] = path,
            ["size"] = fileInfo.Length,
            ["lastModified"] = fileInfo.LastWriteTimeUtc.ToString("O")
        };

        return SuccessOutput(result);
    }

    private static async Task<NodeOutput> AppendFileAsync(
        string path,
        string? content,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        await File.AppendAllTextAsync(path, content ?? string.Empty, encoding, cancellationToken);

        var fileInfo = new FileInfo(path);
        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "append",
            ["path"] = path,
            ["size"] = fileInfo.Length,
            ["lastModified"] = fileInfo.LastWriteTimeUtc.ToString("O")
        };

        return SuccessOutput(result);
    }

    private static NodeOutput DeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return FailureOutput($"File not found: {path}");
        }

        File.Delete(path);

        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "delete",
            ["path"] = path,
            ["deleted"] = true
        };

        return SuccessOutput(result);
    }

    private static NodeOutput CheckFileExists(string path)
    {
        var exists = File.Exists(path);
        FileInfo? fileInfo = exists ? new FileInfo(path) : null;

        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "exists",
            ["path"] = path,
            ["exists"] = exists,
            ["size"] = fileInfo?.Length,
            ["lastModified"] = fileInfo?.LastWriteTimeUtc.ToString("O")
        };

        return SuccessOutput(result);
    }

    private static NodeOutput CopyFile(string sourcePath, string? destinationPath, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return FailureOutput("Destination path is required for copy operation");
        }

        if (!File.Exists(sourcePath))
        {
            return FailureOutput($"Source file not found: {sourcePath}");
        }

        File.Copy(sourcePath, destinationPath, overwrite);

        var fileInfo = new FileInfo(destinationPath);
        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "copy",
            ["sourcePath"] = sourcePath,
            ["destinationPath"] = destinationPath,
            ["size"] = fileInfo.Length
        };

        return SuccessOutput(result);
    }

    private static NodeOutput MoveFile(string sourcePath, string? destinationPath, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return FailureOutput("Destination path is required for move operation");
        }

        if (!File.Exists(sourcePath))
        {
            return FailureOutput($"Source file not found: {sourcePath}");
        }

        File.Move(sourcePath, destinationPath, overwrite);

        var fileInfo = new FileInfo(destinationPath);
        var result = new Dictionary<string, object?>
        {
            ["success"] = true,
            ["operation"] = "move",
            ["sourcePath"] = sourcePath,
            ["destinationPath"] = destinationPath,
            ["size"] = fileInfo.Length
        };

        return SuccessOutput(result);
    }

    private static Encoding GetEncoding(string encodingName)
    {
        return encodingName.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => Encoding.UTF8,
            "ascii" => Encoding.ASCII,
            "utf-16" or "utf16" or "unicode" => Encoding.Unicode,
            "utf-32" or "utf32" => Encoding.UTF32,
            _ => Encoding.UTF8
        };
    }
}
