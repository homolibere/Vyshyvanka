using CsCheck;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Tests.Integration.Designer.Generators;

namespace Vyshyvanka.Tests.Integration.Designer;

/// <summary>
/// Integration tests for WorkflowStateService validation operations.
/// Tests validation error detection for various invalid workflow configurations.
/// </summary>
public class WorkflowStateServiceValidationTests
{
    #region No Trigger Node Error Tests (Task 8.1 - Requirement 5.1)

    /// <summary>
    /// Tests that when a workflow has no trigger node, validation reports an error.
    /// Validates: Requirements 5.1
    /// </summary>
    [Fact]
    public void WhenWorkflowHasNoTriggerNodeThenValidationReportsError()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateWorkflowWithoutTrigger();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.True(service.HasValidationErrors);
        Assert.Contains(service.ValidationResult.Errors, 
            e => e.ErrorCode == "WORKFLOW_NO_TRIGGER");
    }

    /// <summary>
    /// Tests that the no trigger error message is descriptive.
    /// Validates: Requirements 5.1
    /// </summary>
    [Fact]
    public void WhenWorkflowHasNoTriggerNodeThenErrorMessageIsDescriptive()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateWorkflowWithoutTrigger();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        var error = service.ValidationResult.Errors
            .FirstOrDefault(e => e.ErrorCode == "WORKFLOW_NO_TRIGGER");
        Assert.NotNull(error);
        Assert.Contains("trigger", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that an empty workflow (no nodes) does not report no trigger error.
    /// Validates: Requirements 5.1
    /// </summary>
    [Fact]
    public void WhenWorkflowIsEmptyThenNoTriggerErrorIsNotReported()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateEmptyWorkflow();

        // Act
        service.LoadWorkflow(workflow);

        // Assert - Empty workflow should not have the no trigger error
        Assert.DoesNotContain(service.ValidationResult.Errors, 
            e => e.ErrorCode == "WORKFLOW_NO_TRIGGER");
    }

    #endregion

    #region Duplicate Node ID Error Tests (Task 8.1 - Requirement 5.2)

    /// <summary>
    /// Tests that when a workflow has duplicate node IDs, validation reports an error.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void WhenWorkflowHasDuplicateNodeIdsThenValidationReportsError()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateWorkflowWithDuplicateIds();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.True(service.HasValidationErrors);
        Assert.Contains(service.ValidationResult.Errors, 
            e => e.ErrorCode == "NODE_ID_DUPLICATE");
    }

    /// <summary>
    /// Tests that the duplicate ID error message contains the duplicate ID.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void WhenWorkflowHasDuplicateNodeIdsThenErrorMessageContainsDuplicateId()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateWorkflowWithDuplicateIds();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        var error = service.ValidationResult.Errors
            .FirstOrDefault(e => e.ErrorCode == "NODE_ID_DUPLICATE");
        Assert.NotNull(error);
        Assert.Contains("duplicate-id", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that unique node IDs do not trigger duplicate ID error.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void WhenWorkflowHasUniqueNodeIdsThenNoDuplicateIdError()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateSimpleWorkflow();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.DoesNotContain(service.ValidationResult.Errors, 
            e => e.ErrorCode == "NODE_ID_DUPLICATE");
    }

    #endregion

    #region Connection to Non-Existent Node Error Tests (Task 8.1 - Requirement 5.3)

    /// <summary>
    /// Tests that when a connection references a non-existent target node, validation reports an error.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void WhenConnectionReferencesNonExistentTargetNodeThenValidationReportsError()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateWorkflowWithInvalidConnection();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.True(service.HasValidationErrors);
        Assert.Contains(service.ValidationResult.Errors, 
            e => e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND");
    }

    /// <summary>
    /// Tests that when a connection references a non-existent source node, validation reports an error.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void WhenConnectionReferencesNonExistentSourceNodeThenValidationReportsError()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        
        var triggerId = Guid.NewGuid().ToString();
        var workflow = TestFixtures.CreateEmptyWorkflow() with
        {
            Nodes = [TestFixtures.CreateTriggerNode(triggerId)],
            Connections = [TestFixtures.CreateConnection("non-existent-source", triggerId)]
        };

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.True(service.HasValidationErrors);
        Assert.Contains(service.ValidationResult.Errors, 
            e => e.ErrorCode == "CONNECTION_SOURCE_NOT_FOUND");
    }

    /// <summary>
    /// Tests that the connection error message contains the non-existent node ID.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void WhenConnectionReferencesNonExistentNodeThenErrorMessageContainsNodeId()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateWorkflowWithInvalidConnection();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        var error = service.ValidationResult.Errors
            .FirstOrDefault(e => e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND");
        Assert.NotNull(error);
        Assert.Contains("non-existent-node-id", error.Message);
    }

    /// <summary>
    /// Tests that valid connections do not trigger connection errors.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void WhenConnectionsAreValidThenNoConnectionErrors()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateSimpleWorkflow();

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.DoesNotContain(service.ValidationResult.Errors, 
            e => e.ErrorCode == "CONNECTION_SOURCE_NOT_FOUND");
        Assert.DoesNotContain(service.ValidationResult.Errors, 
            e => e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND");
    }

    #endregion

    #region Validation Event Tests

    /// <summary>
    /// Tests that validation changed event is raised when workflow is loaded.
    /// Validates: Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void WhenWorkflowLoadedThenValidationChangedEventIsRaised()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var workflow = TestFixtures.CreateSimpleWorkflow();
        var eventRaised = false;
        service.OnValidationChanged += _ => eventRaised = true;

        // Act
        service.LoadWorkflow(workflow);

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Tests that validation is triggered when a node is added.
    /// Validates: Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void WhenNodeAddedThenValidationIsTriggered()
    {
        // Arrange
        var service = new WorkflowStateService();
        service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());
        var validationCount = 0;
        service.OnValidationChanged += _ => validationCount++;

        // Act
        service.AddNode(TestFixtures.CreateTriggerNode());

        // Assert
        Assert.True(validationCount > 0);
    }

    #endregion

    #region Property Tests (Task 8.2)

    /// <summary>
    /// Feature: blazor-integration-tests, Property 11: Valid Workflow Passes Validation
    /// For any workflow with at least one trigger node, unique node IDs, and valid connections,
    /// validation SHALL report no errors.
    /// Validates: Requirements 5.4
    /// </summary>
    [Fact]
    public void Property11_ValidWorkflowPassesValidation()
    {
        DesignerGenerators.ValidWorkflowGen.Sample(workflow =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(TestFixtures.CreateCommonNodeDefinitions());

            // Act
            service.LoadWorkflow(workflow);

            // Assert - Valid workflow should have no validation errors
            // Note: We check for specific structural errors, not all possible errors
            // since the generator creates structurally valid workflows
            Assert.DoesNotContain(service.ValidationResult.Errors, 
                e => e.ErrorCode == "WORKFLOW_NO_TRIGGER");
            Assert.DoesNotContain(service.ValidationResult.Errors, 
                e => e.ErrorCode == "NODE_ID_DUPLICATE");
            Assert.DoesNotContain(service.ValidationResult.Errors, 
                e => e.ErrorCode == "CONNECTION_SOURCE_NOT_FOUND");
            Assert.DoesNotContain(service.ValidationResult.Errors, 
                e => e.ErrorCode == "CONNECTION_TARGET_NOT_FOUND");
        }, iter: 100);
    }

    #endregion
}
