using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Credentials;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;
using Vyshyvanka.Engine.Nodes.Actions;

namespace Vyshyvanka.Tests.Unit.Nodes;

public class FileOperationsNodeTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileOperationsNode _sut = new();

    public FileOperationsNodeTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"vyshyvanka_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private static ExecutionContext CreateContext() =>
        new(Guid.NewGuid(), Guid.NewGuid(), NullCredentialProvider.Instance);

    private string TestFile(string name) => Path.Combine(_testDir, name);

    [Fact]
    public void WhenCreatedThenHasCorrectMetadata()
    {
        _sut.Type.Should().Be("file-operations");
        _sut.Category.Should().Be(NodeCategory.Action);
        _sut.Id.Should().NotBeNullOrWhiteSpace();
    }

    // --- Read operation ---

    [Fact]
    public async Task WhenReadExistingFileThenReturnsContent()
    {
        var filePath = TestFile("read-test.txt");
        await File.WriteAllTextAsync(filePath, "Hello, World!");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "read",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("content").GetString().Should().Be("Hello, World!");
        result.Data.GetProperty("operation").GetString().Should().Be("read");
        result.Data.GetProperty("path").GetString().Should().Be(filePath);
        result.Data.GetProperty("isBinary").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task WhenReadNonExistentFileThenReturnsFailure()
    {
        var filePath = TestFile("nonexistent.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "read",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    [Fact]
    public async Task WhenReadBinaryFileThenReturnsBase64()
    {
        var filePath = TestFile("binary-test.bin");
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE };
        await File.WriteAllBytesAsync(filePath, bytes);

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "read",
            path = filePath,
            isBinary = true
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        var content = result.Data.GetProperty("content").GetString();
        Convert.FromBase64String(content!).Should().BeEquivalentTo(bytes);
        result.Data.GetProperty("isBinary").GetBoolean().Should().BeTrue();
    }

    // --- Write operation ---

    [Fact]
    public async Task WhenWriteNewFileThenCreatesFile()
    {
        var filePath = TestFile("write-test.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath,
            content = "New content"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("New content");
        result.Data.GetProperty("operation").GetString().Should().Be("write");
    }

    [Fact]
    public async Task WhenWriteExistingFileWithoutOverwriteThenReturnsFailure()
    {
        var filePath = TestFile("existing.txt");
        await File.WriteAllTextAsync(filePath, "original");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath,
            content = "new content",
            overwrite = false
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
        (await File.ReadAllTextAsync(filePath)).Should().Be("original");
    }

    [Fact]
    public async Task WhenWriteExistingFileWithOverwriteThenReplacesContent()
    {
        var filePath = TestFile("overwrite.txt");
        await File.WriteAllTextAsync(filePath, "original");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath,
            content = "replaced",
            overwrite = true
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("replaced");
    }

    [Fact]
    public async Task WhenWriteBinaryFileThenWritesBase64DecodedContent()
    {
        var filePath = TestFile("binary-write.bin");
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var base64 = Convert.ToBase64String(bytes);

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath,
            content = base64,
            isBinary = true
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        (await File.ReadAllBytesAsync(filePath)).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task WhenWriteWithCreateDirectoryThenCreatesParentDirs()
    {
        var filePath = Path.Combine(_testDir, "sub", "dir", "file.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath,
            content = "nested content",
            createDirectory = true
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    // --- Append operation ---

    [Fact]
    public async Task WhenAppendToExistingFileThenAddsContent()
    {
        var filePath = TestFile("append-test.txt");
        await File.WriteAllTextAsync(filePath, "Hello");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "append",
            path = filePath,
            content = " World"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("Hello World");
        result.Data.GetProperty("operation").GetString().Should().Be("append");
    }

    [Fact]
    public async Task WhenAppendToNewFileThenCreatesFile()
    {
        var filePath = TestFile("append-new.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "append",
            path = filePath,
            content = "First line"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().Be("First line");
    }

    // --- Delete operation ---

    [Fact]
    public async Task WhenDeleteExistingFileThenRemovesFile()
    {
        var filePath = TestFile("delete-test.txt");
        await File.WriteAllTextAsync(filePath, "to be deleted");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "delete",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
        result.Data.GetProperty("deleted").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task WhenDeleteNonExistentFileThenReturnsFailure()
    {
        var filePath = TestFile("nonexistent-delete.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "delete",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    // --- Exists operation ---

    [Fact]
    public async Task WhenCheckExistsForExistingFileThenReturnsTrue()
    {
        var filePath = TestFile("exists-test.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "exists",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("exists").GetBoolean().Should().BeTrue();
        result.Data.TryGetProperty("size", out _).Should().BeTrue();
    }

    [Fact]
    public async Task WhenCheckExistsForNonExistentFileThenReturnsFalse()
    {
        var filePath = TestFile("nonexistent.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "exists",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        result.Data.GetProperty("exists").GetBoolean().Should().BeFalse();
    }

    // --- Copy operation ---

    [Fact]
    public async Task WhenCopyFileThenCreatesDestination()
    {
        var sourcePath = TestFile("copy-source.txt");
        var destPath = TestFile("copy-dest.txt");
        await File.WriteAllTextAsync(sourcePath, "copy me");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "copy",
            path = sourcePath,
            destinationPath = destPath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        File.Exists(destPath).Should().BeTrue();
        (await File.ReadAllTextAsync(destPath)).Should().Be("copy me");
        File.Exists(sourcePath).Should().BeTrue(); // Source still exists
    }

    [Fact]
    public async Task WhenCopyWithNoDestinationThenReturnsFailure()
    {
        var sourcePath = TestFile("copy-source2.txt");
        await File.WriteAllTextAsync(sourcePath, "content");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "copy",
            path = sourcePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Destination path is required");
    }

    [Fact]
    public async Task WhenCopyNonExistentSourceThenReturnsFailure()
    {
        var sourcePath = TestFile("nonexistent-source.txt");
        var destPath = TestFile("dest.txt");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "copy",
            path = sourcePath,
            destinationPath = destPath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source file not found");
    }

    // --- Move operation ---

    [Fact]
    public async Task WhenMoveFileThenSourceIsRemovedAndDestinationCreated()
    {
        var sourcePath = TestFile("move-source.txt");
        var destPath = TestFile("move-dest.txt");
        await File.WriteAllTextAsync(sourcePath, "move me");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "move",
            path = sourcePath,
            destinationPath = destPath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        File.Exists(destPath).Should().BeTrue();
        File.Exists(sourcePath).Should().BeFalse();
        (await File.ReadAllTextAsync(destPath)).Should().Be("move me");
    }

    [Fact]
    public async Task WhenMoveWithNoDestinationThenReturnsFailure()
    {
        var sourcePath = TestFile("move-source2.txt");
        await File.WriteAllTextAsync(sourcePath, "content");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "move",
            path = sourcePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Destination path is required");
    }

    [Fact]
    public async Task WhenMoveNonExistentSourceThenReturnsFailure()
    {
        var sourcePath = TestFile("nonexistent-move.txt");
        var destPath = TestFile("dest-move.txt");

        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "move",
            path = sourcePath,
            destinationPath = destPath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Source file not found");
    }

    // --- Unknown operation ---

    [Fact]
    public async Task WhenUnknownOperationThenReturnsFailure()
    {
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "unknown",
            path = "/some/path"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unknown operation");
    }

    // --- Encoding ---

    [Fact]
    public async Task WhenWriteWithAsciiEncodingThenUsesAscii()
    {
        var filePath = TestFile("ascii-test.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath,
            content = "ASCII content",
            encoding = "ascii"
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task WhenWriteWithNullContentThenWritesEmptyFile()
    {
        var filePath = TestFile("empty-write.txt");
        var config = JsonSerializer.SerializeToElement(new
        {
            operation = "write",
            path = filePath
        });
        var input = new NodeInput { Data = default, Configuration = config };
        var context = CreateContext();

        var result = await _sut.ExecuteAsync(input, context);

        result.Success.Should().BeTrue();
        (await File.ReadAllTextAsync(filePath)).Should().BeEmpty();
    }
}
