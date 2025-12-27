using System.Text.Json;
using CsCheck;
using FlowForge.Designer.Services;
using FlowForge.Tests.Integration.Designer.Generators;

namespace FlowForge.Tests.Integration.Designer;

/// <summary>
/// Integration tests for WorkflowStateService serialization operations.
/// Tests JSON serialization format and round-trip consistency.
/// </summary>
public class WorkflowStateServiceSerializationTests
{
    #region JSON Format Tests (Task 7.1)

    /// <summary>
    /// Tests that serialized JSON uses camelCase property names.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void WhenSerializingWorkflowThenJsonUsesCamelCase()
    {
        // Arrange
        var service = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        service.LoadWorkflow(workflow);

        // Act
        var json = service.SerializeToJson();

        // Assert - Check for camelCase property names
        Assert.Contains("\"id\":", json);
        Assert.Contains("\"name\":", json);
        Assert.Contains("\"isActive\":", json);
        Assert.Contains("\"createdAt\":", json);
        Assert.Contains("\"updatedAt\":", json);
        Assert.Contains("\"createdBy\":", json);
        Assert.Contains("\"nodes\":", json);
        Assert.Contains("\"connections\":", json);
        
        // Assert - Should NOT contain PascalCase versions
        Assert.DoesNotContain("\"Id\":", json);
        Assert.DoesNotContain("\"Name\":", json);
        Assert.DoesNotContain("\"IsActive\":", json);
        Assert.DoesNotContain("\"CreatedAt\":", json);
        Assert.DoesNotContain("\"UpdatedAt\":", json);
        Assert.DoesNotContain("\"CreatedBy\":", json);
        Assert.DoesNotContain("\"Nodes\":", json);
        Assert.DoesNotContain("\"Connections\":", json);
    }

    /// <summary>
    /// Tests that serialized JSON for nodes uses camelCase property names.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void WhenSerializingWorkflowThenNodePropertiesUseCamelCase()
    {
        // Arrange
        var service = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        service.LoadWorkflow(workflow);

        // Act
        var json = service.SerializeToJson();

        // Assert - Check for camelCase node property names
        Assert.Contains("\"type\":", json);
        Assert.Contains("\"position\":", json);
        
        // Assert - Should NOT contain PascalCase versions
        Assert.DoesNotContain("\"Type\":", json);
        Assert.DoesNotContain("\"Position\":", json);
    }

    /// <summary>
    /// Tests that serialized JSON for connections uses camelCase property names.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void WhenSerializingWorkflowThenConnectionPropertiesUseCamelCase()
    {
        // Arrange
        var service = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        service.LoadWorkflow(workflow);

        // Act
        var json = service.SerializeToJson();

        // Assert - Check for camelCase connection property names
        Assert.Contains("\"sourceNodeId\":", json);
        Assert.Contains("\"sourcePort\":", json);
        Assert.Contains("\"targetNodeId\":", json);
        Assert.Contains("\"targetPort\":", json);
        
        // Assert - Should NOT contain PascalCase versions
        Assert.DoesNotContain("\"SourceNodeId\":", json);
        Assert.DoesNotContain("\"SourcePort\":", json);
        Assert.DoesNotContain("\"TargetNodeId\":", json);
        Assert.DoesNotContain("\"TargetPort\":", json);
    }

    /// <summary>
    /// Tests that serialized JSON is valid and parseable.
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void WhenSerializingWorkflowThenJsonIsValid()
    {
        // Arrange
        var service = new WorkflowStateService();
        var workflow = TestFixtures.CreateSimpleWorkflow();
        service.LoadWorkflow(workflow);

        // Act
        var json = service.SerializeToJson();

        // Assert - Should be valid JSON
        var exception = Record.Exception(() => JsonDocument.Parse(json));
        Assert.Null(exception);
    }

    #endregion

    #region Invalid JSON Tests (Task 7.1)

    /// <summary>
    /// Tests that deserializing invalid JSON returns null.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void WhenDeserializingInvalidJsonThenReturnsNull()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var result = WorkflowStateService.DeserializeFromJson(invalidJson);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that deserializing empty string returns null.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void WhenDeserializingEmptyStringThenReturnsNull()
    {
        // Arrange
        var emptyJson = "";

        // Act
        var result = WorkflowStateService.DeserializeFromJson(emptyJson);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that deserializing whitespace-only string returns null.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void WhenDeserializingWhitespaceOnlyStringThenReturnsNull()
    {
        // Arrange
        var whitespaceJson = "   \t\n  ";

        // Act
        var result = WorkflowStateService.DeserializeFromJson(whitespaceJson);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that deserializing null returns null.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void WhenDeserializingNullThenReturnsNull()
    {
        // Act
        var result = WorkflowStateService.DeserializeFromJson(null!);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that deserializing malformed JSON does not throw exception.
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void WhenDeserializingMalformedJsonThenDoesNotThrow()
    {
        // Arrange
        var malformedJson = "{ \"name\": \"test\", }"; // trailing comma

        // Act & Assert - should not throw
        var exception = Record.Exception(() => WorkflowStateService.DeserializeFromJson(malformedJson));
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that deserializing JSON array returns null (expects object).
    /// Validates: Requirements 4.3
    /// </summary>
    [Fact]
    public void WhenDeserializingJsonArrayThenReturnsNull()
    {
        // Arrange
        var arrayJson = "[1, 2, 3]";

        // Act
        var result = WorkflowStateService.DeserializeFromJson(arrayJson);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Property Tests (Task 7.2)

    /// <summary>
    /// Feature: blazor-integration-tests, Property 10: Workflow Serialization Round-Trip
    /// For any valid workflow, serializing to JSON then deserializing SHALL produce 
    /// an equivalent workflow with the same nodes, connections, and metadata.
    /// Validates: Requirements 4.1
    /// </summary>
    [Fact]
    public void Property10_WorkflowSerializationRoundTrip()
    {
        DesignerGenerators.WorkflowGen.Sample(workflow =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.LoadWorkflow(workflow);

            // Act
            var json = service.SerializeToJson();
            var deserialized = WorkflowStateService.DeserializeFromJson(json);

            // Assert - Deserialization succeeded
            Assert.NotNull(deserialized);

            // Assert - Core properties match
            Assert.Equal(workflow.Id, deserialized.Id);
            Assert.Equal(workflow.Name, deserialized.Name);
            Assert.Equal(workflow.Description, deserialized.Description);
            Assert.Equal(workflow.Version, deserialized.Version);
            Assert.Equal(workflow.IsActive, deserialized.IsActive);

            // Assert - Node count matches
            Assert.Equal(workflow.Nodes.Count, deserialized.Nodes.Count);

            // Assert - Connection count matches
            Assert.Equal(workflow.Connections.Count, deserialized.Connections.Count);

            // Assert - Tags match
            Assert.Equal(workflow.Tags.Count, deserialized.Tags.Count);
            for (int i = 0; i < workflow.Tags.Count; i++)
            {
                Assert.Equal(workflow.Tags[i], deserialized.Tags[i]);
            }

            // Assert - Settings match
            Assert.Equal(workflow.Settings.MaxRetries, deserialized.Settings.MaxRetries);
            Assert.Equal(workflow.Settings.ErrorHandling, deserialized.Settings.ErrorHandling);
            Assert.Equal(workflow.Settings.Timeout, deserialized.Settings.Timeout);

            // Assert - Node properties match
            for (int i = 0; i < workflow.Nodes.Count; i++)
            {
                var originalNode = workflow.Nodes[i];
                var deserializedNode = deserialized.Nodes.FirstOrDefault(n => n.Id == originalNode.Id);
                Assert.NotNull(deserializedNode);
                Assert.Equal(originalNode.Type, deserializedNode.Type);
                Assert.Equal(originalNode.Name, deserializedNode.Name);
                Assert.Equal(originalNode.Position.X, deserializedNode.Position.X);
                Assert.Equal(originalNode.Position.Y, deserializedNode.Position.Y);
                Assert.Equal(originalNode.CredentialId, deserializedNode.CredentialId);
            }

            // Assert - Connection properties match
            for (int i = 0; i < workflow.Connections.Count; i++)
            {
                var originalConn = workflow.Connections[i];
                var deserializedConn = deserialized.Connections.FirstOrDefault(c =>
                    c.SourceNodeId == originalConn.SourceNodeId &&
                    c.SourcePort == originalConn.SourcePort &&
                    c.TargetNodeId == originalConn.TargetNodeId &&
                    c.TargetPort == originalConn.TargetPort);
                Assert.NotNull(deserializedConn);
            }

            // Assert - CreatedBy matches
            Assert.Equal(workflow.CreatedBy, deserialized.CreatedBy);
        }, iter: 100);
    }

    #endregion
}
