using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Models;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for workflow serialization.
/// </summary>
public class WorkflowSerializationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Feature: flowforge, Property 1: Workflow Serialization Round-Trip
    /// For any valid Workflow object, serializing it to JSON and then deserializing back
    /// to a Workflow object SHALL produce an equivalent object with identical nodes,
    /// connections, and metadata.
    /// Validates: Requirements 1.1, 1.6, 1.7
    /// </summary>
    [Fact]
    public void WorkflowSerializationRoundTrip_ProducesEquivalentObject()
    {
        GenWorkflow.Sample(original =>
        {
            // Act: Serialize to JSON
            var json = JsonSerializer.Serialize(original, SerializerOptions);
            
            // Act: Deserialize back to Workflow
            var deserialized = JsonSerializer.Deserialize<Workflow>(json, SerializerOptions);
            
            // Assert: Objects should be equivalent
            Assert.NotNull(deserialized);
            AssertWorkflowsEquivalent(original, deserialized);
        }, iter: 100);
    }

    private static void AssertWorkflowsEquivalent(Workflow expected, Workflow actual)
    {
        // Compare scalar properties
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.IsActive, actual.IsActive);
        Assert.Equal(expected.CreatedBy, actual.CreatedBy);
        
        // Compare dates (with tolerance for serialization precision)
        AssertDateTimesEquivalent(expected.CreatedAt, actual.CreatedAt);
        AssertDateTimesEquivalent(expected.UpdatedAt, actual.UpdatedAt);
        
        // Compare settings
        Assert.Equal(expected.Settings.Timeout, actual.Settings.Timeout);
        Assert.Equal(expected.Settings.MaxRetries, actual.Settings.MaxRetries);
        Assert.Equal(expected.Settings.ErrorHandling, actual.Settings.ErrorHandling);
        
        // Compare tags
        Assert.Equal(expected.Tags, actual.Tags);
        
        // Compare nodes
        Assert.Equal(expected.Nodes.Count, actual.Nodes.Count);
        for (int i = 0; i < expected.Nodes.Count; i++)
        {
            AssertNodesEquivalent(expected.Nodes[i], actual.Nodes[i]);
        }
        
        // Compare connections
        Assert.Equal(expected.Connections.Count, actual.Connections.Count);
        for (int i = 0; i < expected.Connections.Count; i++)
        {
            AssertConnectionsEquivalent(expected.Connections[i], actual.Connections[i]);
        }
    }

    private static void AssertNodesEquivalent(WorkflowNode expected, WorkflowNode actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Position.X, actual.Position.X);
        Assert.Equal(expected.Position.Y, actual.Position.Y);
        Assert.Equal(expected.CredentialId, actual.CredentialId);
        
        // Compare Configuration as JSON strings since JsonElement doesn't have value equality
        var expectedConfig = expected.Configuration.GetRawText();
        var actualConfig = actual.Configuration.GetRawText();
        Assert.Equal(expectedConfig, actualConfig);
    }

    private static void AssertConnectionsEquivalent(Connection expected, Connection actual)
    {
        Assert.Equal(expected.SourceNodeId, actual.SourceNodeId);
        Assert.Equal(expected.SourcePort, actual.SourcePort);
        Assert.Equal(expected.TargetNodeId, actual.TargetNodeId);
        Assert.Equal(expected.TargetPort, actual.TargetPort);
    }

    private static void AssertDateTimesEquivalent(DateTime expected, DateTime actual)
    {
        // JSON serialization may lose some precision, so compare with tolerance
        var diff = Math.Abs((expected - actual).TotalMilliseconds);
        Assert.True(diff < 1, $"DateTime difference too large: {diff}ms");
    }

    #region Generators

    /// <summary>Generator for non-empty alphanumeric strings.</summary>
    private static Gen<string> GenNonEmptyString(int minLength, int maxLength) =>
        Gen.Char['a', 'z'].Array[minLength, maxLength].Select(chars => new string(chars));

    /// <summary>Generator for optional strings.</summary>
    private static Gen<string?> GenOptionalString(int maxLength) =>
        Gen.Bool.SelectMany(hasValue => 
            hasValue 
                ? GenNonEmptyString(1, maxLength).Select(s => (string?)s)
                : Gen.Int[0, 0].Select(_ => (string?)null));

    /// <summary>Generator for Position.</summary>
    private static readonly Gen<Position> GenPosition =
        from x in Gen.Double[-10000, 10000]
        from y in Gen.Double[-10000, 10000]
        select new Position(x, y);

    /// <summary>Generator for simple JSON configuration.</summary>
    private static Gen<JsonElement> GenConfiguration =>
        Gen.Int[0, 3].Select(i => i switch
        {
            0 => JsonDocument.Parse("{}").RootElement,
            1 => JsonDocument.Parse("{\"enabled\":true}").RootElement,
            2 => JsonDocument.Parse("{\"value\":42}").RootElement,
            _ => JsonDocument.Parse("{\"name\":\"test\",\"count\":5}").RootElement
        });

    /// <summary>Generator for WorkflowNode.</summary>
    private static readonly Gen<WorkflowNode> GenWorkflowNode =
        from id in GenNonEmptyString(1, 20)
        from type in GenNonEmptyString(1, 50)
        from name in GenNonEmptyString(1, 50)
        from position in GenPosition
        from config in GenConfiguration
        from hasCredential in Gen.Bool
        from credentialId in Gen.Guid
        select new WorkflowNode
        {
            Id = id,
            Type = type,
            Name = name,
            Position = position,
            Configuration = config,
            CredentialId = hasCredential ? credentialId : null
        };

    /// <summary>Generator for Connection.</summary>
    private static readonly Gen<Connection> GenConnection =
        from sourceNodeId in GenNonEmptyString(1, 20)
        from sourcePort in GenNonEmptyString(1, 20)
        from targetNodeId in GenNonEmptyString(1, 20)
        from targetPort in GenNonEmptyString(1, 20)
        select new Connection
        {
            SourceNodeId = sourceNodeId,
            SourcePort = sourcePort,
            TargetNodeId = targetNodeId,
            TargetPort = targetPort
        };

    /// <summary>Generator for ErrorHandlingMode.</summary>
    private static readonly Gen<ErrorHandlingMode> GenErrorHandlingMode =
        Gen.OneOf(
            Gen.Const(ErrorHandlingMode.StopOnFirstError),
            Gen.Const(ErrorHandlingMode.ContinueOnError),
            Gen.Const(ErrorHandlingMode.RetryWithBackoff)
        );

    /// <summary>Generator for WorkflowSettings.</summary>
    private static readonly Gen<WorkflowSettings> GenWorkflowSettings =
        from hasTimeout in Gen.Bool
        from timeoutSeconds in Gen.Int[1, 3600]
        from maxRetries in Gen.Int[0, 10]
        from errorHandling in GenErrorHandlingMode
        select new WorkflowSettings
        {
            Timeout = hasTimeout ? TimeSpan.FromSeconds(timeoutSeconds) : null,
            MaxRetries = maxRetries,
            ErrorHandling = errorHandling
        };

    /// <summary>Generator for tags list.</summary>
    private static readonly Gen<List<string>> GenTags =
        GenNonEmptyString(1, 20).List[0, 5];

    /// <summary>Generator for DateTime (UTC, truncated to milliseconds for JSON compatibility).</summary>
    private static readonly Gen<DateTime> GenDateTime =
        from year in Gen.Int[2020, 2030]
        from month in Gen.Int[1, 12]
        from day in Gen.Int[1, 28]
        from hour in Gen.Int[0, 23]
        from minute in Gen.Int[0, 59]
        from second in Gen.Int[0, 59]
        from millisecond in Gen.Int[0, 999]
        select new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);

    /// <summary>Generator for Workflow.</summary>
    private static readonly Gen<Workflow> GenWorkflow =
        from id in Gen.Guid
        from name in GenNonEmptyString(1, 50)
        from description in GenOptionalString(200)
        from version in Gen.Int[0, 1000]
        from isActive in Gen.Bool
        from nodes in GenWorkflowNode.List[0, 10]
        from connections in GenConnection.List[0, 10]
        from settings in GenWorkflowSettings
        from tags in GenTags
        from createdAt in GenDateTime
        from updatedAt in GenDateTime
        from createdBy in Gen.Guid
        select new Workflow
        {
            Id = id,
            Name = name,
            Description = description,
            Version = version,
            IsActive = isActive,
            Nodes = nodes,
            Connections = connections,
            Settings = settings,
            Tags = tags,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CreatedBy = createdBy
        };

    #endregion
}
