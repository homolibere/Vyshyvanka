# Implementation Plan: Blazor Integration Tests

## Overview

This implementation plan creates integration tests for the FlowForge.Designer Blazor WebAssembly application using bUnit and CsCheck. Tests are organized by service/component and validate correctness properties defined in the design document.

## Tasks

- [x] 1. Set up test infrastructure and generators
  - [x] 1.1 Create DesignerGenerators class with CsCheck generators
    - Create `FlowForge.Tests/Integration/Designer/Generators/DesignerGenerators.cs`
    - Implement `NodeGen` for generating random WorkflowNode instances
    - Implement `WorkflowGen` for generating random Workflow instances
    - Implement `ConnectionGen` for generating random Connection instances
    - Implement `NodeDefinitionGen` for generating random NodeDefinition instances
    - Implement `PortTypeGen` for generating random PortType values
    - _Requirements: 1.1, 2.1, 4.1_

  - [x] 1.2 Create MockHttpMessageHandler for API tests
    - Create `FlowForge.Tests/Integration/Designer/MockHttpMessageHandler.cs`
    - Implement configurable response handling
    - Support for different HTTP methods and status codes
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 1.3 Create TestFixtures class with common test data
    - Create `FlowForge.Tests/Integration/Designer/TestFixtures.cs`
    - Implement factory methods for trigger nodes, action nodes, node definitions
    - Implement factory methods for valid workflows with connections
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 2. Implement WorkflowStateService node operation tests
  - [x] 2.1 Implement node addition tests
    - Create `FlowForge.Tests/Integration/Designer/WorkflowStateServiceNodeTests.cs`
    - Test that added nodes appear in collection
    - Test that added nodes are selected
    - Test dirty flag is set
    - _Requirements: 1.1_

  - [x] 2.2 Write property test for node addition invariant
    - **Property 1: Node Addition Invariant**
    - **Validates: Requirements 1.1**

  - [x] 2.3 Implement node removal tests
    - Test that removed nodes are gone from collection
    - Test that connections to/from removed nodes are also removed
    - Test selection is cleared if removed node was selected
    - _Requirements: 1.2_

  - [x] 2.4 Write property test for node removal cascading
    - **Property 2: Node Removal Cascades Connections**
    - **Validates: Requirements 1.2**

  - [x] 2.5 Implement node position update tests
    - Test that position coordinates are updated
    - Test that state change event is raised
    - _Requirements: 1.3_

  - [x] 2.6 Write property test for node position update
    - **Property 3: Node Position Update**
    - **Validates: Requirements 1.3**

- [x] 3. Checkpoint - Ensure node operation tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement WorkflowStateService connection operation tests
  - [x] 4.1 Implement connection addition tests
    - Create `FlowForge.Tests/Integration/Designer/WorkflowStateServiceConnectionTests.cs`
    - Test that valid connections are added
    - Test dirty flag is set
    - _Requirements: 2.1_

  - [x] 4.2 Write property test for connection addition
    - **Property 4: Connection Addition**
    - **Validates: Requirements 2.1**

  - [x] 4.3 Implement duplicate connection prevention tests
    - Test that duplicate connections are not added
    - Test that connection count remains unchanged
    - _Requirements: 2.2_

  - [x] 4.4 Write property test for duplicate connection prevention
    - **Property 5: Duplicate Connection Prevention**
    - **Validates: Requirements 2.2**

  - [x] 4.5 Implement connection validation tests
    - Test self-connection rejection
    - Test port type compatibility rules
    - _Requirements: 2.4, 2.5_

  - [x] 4.6 Write property test for self-connection rejection
    - **Property 6: Self-Connection Rejection**
    - **Validates: Requirements 2.4**

  - [x] 4.7 Write property test for port type compatibility
    - **Property 7: Port Type Compatibility**
    - **Validates: Requirements 2.5**

- [x] 5. Implement WorkflowStateService undo/redo tests
  - [x] 5.1 Implement undo/redo tests
    - Create `FlowForge.Tests/Integration/Designer/WorkflowStateServiceUndoRedoTests.cs`
    - Test undo restores previous state
    - Test redo restores undone state
    - Test redo stack is cleared on new action
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 5.2 Write property test for undo restores previous state
    - **Property 8: Undo Restores Previous State**
    - **Validates: Requirements 3.1, 3.2**

  - [x] 5.3 Write property test for redo restores undone state
    - **Property 9: Redo Restores Undone State**
    - **Validates: Requirements 3.3**

- [x] 6. Checkpoint - Ensure connection and undo/redo tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement WorkflowStateService serialization tests
  - [x] 7.1 Implement serialization tests
    - Create `FlowForge.Tests/Integration/Designer/WorkflowStateServiceSerializationTests.cs`
    - Test JSON output format (camelCase)
    - Test invalid JSON returns null
    - _Requirements: 4.2, 4.3_

  - [x] 7.2 Write property test for serialization round-trip
    - **Property 10: Workflow Serialization Round-Trip**
    - **Validates: Requirements 4.1**

- [x] 8. Implement WorkflowStateService validation tests
  - [x] 8.1 Implement validation error tests
    - Create `FlowForge.Tests/Integration/Designer/WorkflowStateServiceValidationTests.cs`
    - Test no trigger node error
    - Test duplicate node ID error
    - Test connection to non-existent node error
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 8.2 Write property test for valid workflow passes validation
    - **Property 11: Valid Workflow Passes Validation**
    - **Validates: Requirements 5.4**

- [x] 9. Checkpoint - Ensure serialization and validation tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Implement NodePalette component integration tests
  - [x] 10.1 Implement NodePalette rendering tests
    - Create `FlowForge.Tests/Integration/Designer/NodePaletteIntegrationTests.cs`
    - Test nodes are grouped by category
    - Test search filtering works
    - Test drag start/end notifications
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 10.2 Write property test for category grouping
    - **Property 12: NodePalette Category Grouping**
    - **Validates: Requirements 6.1**

  - [x] 10.3 Write property test for search filtering
    - **Property 13: NodePalette Search Filtering**
    - **Validates: Requirements 6.2**

- [x] 11. Implement WorkflowCanvas component integration tests
  - [x] 11.1 Implement WorkflowCanvas rendering tests
    - Create `FlowForge.Tests/Integration/Designer/WorkflowCanvasIntegrationTests.cs`
    - Test nodes are rendered from workflow state
    - Test zoom bounds are enforced
    - _Requirements: 7.1, 7.4_

  - [x] 11.2 Write property test for canvas node rendering
    - **Property 14: Canvas Node Rendering**
    - **Validates: Requirements 7.1**

  - [x] 11.3 Write property test for canvas zoom bounds
    - **Property 15: Canvas Zoom Bounds**
    - **Validates: Requirements 7.4**

- [x] 12. Checkpoint - Ensure component integration tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Implement FlowForgeApiClient integration tests
  - [x] 13.1 Implement API client tests
    - Create `FlowForge.Tests/Integration/Designer/FlowForgeApiClientTests.cs`
    - Test workflow deserialization
    - Test node definition deserialization
    - Test error handling
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x] 13.2 Write property test for API deserialization
    - **Property 16: API Workflow Deserialization**
    - **Validates: Requirements 8.1, 8.4**

- [x] 14. Implement ToastService integration tests
  - [x] 14.1 Implement ToastService tests
    - Create `FlowForge.Tests/Integration/Designer/ToastServiceTests.cs`
    - Test toast addition and event raising
    - Test toast removal
    - Test clear all toasts
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 14.2 Write property test for toast type handling
    - **Property 17: Toast Service Type Handling**
    - **Validates: Requirements 9.1, 9.4**

  - [x] 14.3 Write property test for toast removal
    - **Property 18: Toast Removal**
    - **Validates: Requirements 9.2, 9.3**

- [x] 15. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive test coverage
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using CsCheck with minimum 100 iterations
- Unit tests validate specific examples and edge cases
- bUnit TestContext is used for component rendering tests
- MockHttpMessageHandler is used for API client tests
