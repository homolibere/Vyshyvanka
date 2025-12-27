# Requirements Document

## Introduction

This document defines requirements for integration tests for the FlowForge.Designer Blazor WebAssembly application. These tests validate that Blazor components work correctly with their dependent services, ensuring proper state management, user interactions, and API communication. The tests use bUnit for component testing and CsCheck for property-based testing.

## Glossary

- **Designer**: The FlowForge.Designer Blazor WebAssembly application providing the visual workflow editor
- **WorkflowStateService**: Service managing workflow state, canvas state, node selection, and undo/redo operations
- **WorkflowCanvas**: Blazor component rendering the workflow editing canvas with nodes and connections
- **NodePalette**: Blazor component displaying available nodes grouped by category for drag-and-drop
- **FlowForgeApiClient**: HTTP client service for communicating with the FlowForge API
- **bUnit**: Testing library for Blazor components enabling component rendering and interaction testing
- **TestContext**: bUnit class providing test infrastructure for rendering and interacting with components

## Requirements

### Requirement 1: WorkflowStateService Node Operations

**User Story:** As a developer, I want integration tests for WorkflowStateService node operations, so that I can verify nodes are correctly added, removed, and updated in the workflow state.

#### Acceptance Criteria

1. WHEN a node is added to the workflow THEN THE WorkflowStateService SHALL include the node in the workflow's node collection and select it
2. WHEN a node is removed from the workflow THEN THE WorkflowStateService SHALL remove the node and all its connections from the workflow
3. WHEN a node's position is updated THEN THE WorkflowStateService SHALL update the node's position coordinates
4. WHEN a node's configuration is updated THEN THE WorkflowStateService SHALL update the node's configuration and mark the workflow as dirty
5. WHEN a node's name is updated THEN THE WorkflowStateService SHALL update the node's name and trigger validation

### Requirement 2: WorkflowStateService Connection Operations

**User Story:** As a developer, I want integration tests for WorkflowStateService connection operations, so that I can verify connections between nodes are correctly managed.

#### Acceptance Criteria

1. WHEN a valid connection is added THEN THE WorkflowStateService SHALL add the connection to the workflow
2. WHEN a duplicate connection is added THEN THE WorkflowStateService SHALL not add the duplicate connection
3. WHEN a connection is removed THEN THE WorkflowStateService SHALL remove the connection from the workflow
4. WHEN validating a connection THEN THE WorkflowStateService SHALL reject connections from a node to itself
5. WHEN validating port types THEN THE WorkflowStateService SHALL accept compatible port types and reject incompatible ones

### Requirement 3: WorkflowStateService Undo/Redo Operations

**User Story:** As a developer, I want integration tests for undo/redo functionality, so that I can verify users can revert and restore workflow changes.

#### Acceptance Criteria

1. WHEN an action is performed THEN THE WorkflowStateService SHALL save the previous state to the undo stack
2. WHEN undo is invoked THEN THE WorkflowStateService SHALL restore the previous workflow state
3. WHEN redo is invoked after undo THEN THE WorkflowStateService SHALL restore the undone state
4. WHEN a new action is performed after undo THEN THE WorkflowStateService SHALL clear the redo stack

### Requirement 4: WorkflowStateService Serialization

**User Story:** As a developer, I want integration tests for workflow serialization, so that I can verify workflows are correctly serialized to and deserialized from JSON.

#### Acceptance Criteria

1. FOR ALL valid workflows, serializing then deserializing SHALL produce an equivalent workflow (round-trip property)
2. WHEN serializing a workflow THEN THE WorkflowStateService SHALL produce valid JSON with camelCase property names
3. WHEN deserializing invalid JSON THEN THE WorkflowStateService SHALL return null without throwing exceptions

### Requirement 5: WorkflowStateService Validation

**User Story:** As a developer, I want integration tests for workflow validation, so that I can verify validation rules are correctly enforced.

#### Acceptance Criteria

1. WHEN a workflow has no trigger node THEN THE WorkflowStateService SHALL report a validation error
2. WHEN a workflow has duplicate node IDs THEN THE WorkflowStateService SHALL report a validation error
3. WHEN a connection references a non-existent node THEN THE WorkflowStateService SHALL report a validation error
4. WHEN a workflow is valid THEN THE WorkflowStateService SHALL report no validation errors

### Requirement 6: NodePalette Component Integration

**User Story:** As a developer, I want integration tests for the NodePalette component, so that I can verify nodes are correctly displayed and drag operations work.

#### Acceptance Criteria

1. WHEN node definitions are set THEN THE NodePalette SHALL display nodes grouped by category
2. WHEN a search term is entered THEN THE NodePalette SHALL filter nodes by name and description
3. WHEN a node drag starts THEN THE NodePalette SHALL notify the WorkflowStateService of the dragged node type
4. WHEN a node drag ends THEN THE NodePalette SHALL clear the dragged node type in WorkflowStateService

### Requirement 7: WorkflowCanvas Component Integration

**User Story:** As a developer, I want integration tests for the WorkflowCanvas component, so that I can verify canvas interactions work correctly with the state service.

#### Acceptance Criteria

1. WHEN the canvas is rendered THEN THE WorkflowCanvas SHALL display all nodes from the workflow state
2. WHEN a node is dropped on the canvas THEN THE WorkflowCanvas SHALL add the node at the drop position
3. WHEN the canvas is panned THEN THE WorkflowCanvas SHALL update the canvas state pan coordinates
4. WHEN the canvas is zoomed THEN THE WorkflowCanvas SHALL update the canvas state zoom level within bounds

### Requirement 8: FlowForgeApiClient Integration

**User Story:** As a developer, I want integration tests for the FlowForgeApiClient, so that I can verify API communication works correctly with mocked HTTP responses.

#### Acceptance Criteria

1. WHEN fetching workflows THEN THE FlowForgeApiClient SHALL deserialize the response into workflow objects
2. WHEN saving a workflow THEN THE FlowForgeApiClient SHALL serialize the workflow and send it to the API
3. WHEN an API error occurs THEN THE FlowForgeApiClient SHALL handle the error appropriately
4. WHEN fetching node definitions THEN THE FlowForgeApiClient SHALL deserialize the response into node definition objects

### Requirement 9: ToastService Integration

**User Story:** As a developer, I want integration tests for the ToastService, so that I can verify toast notifications are correctly managed.

#### Acceptance Criteria

1. WHEN a toast is shown THEN THE ToastService SHALL add the toast to the collection and raise the OnChange event
2. WHEN a toast is removed THEN THE ToastService SHALL remove the toast from the collection
3. WHEN toasts are cleared THEN THE ToastService SHALL remove all toasts from the collection
4. FOR ALL toast types (Success, Error, Warning, Info), THE ToastService SHALL create toasts with the correct type and default timeout
