# Implementation Plan: Dynamic Node Configuration UI

## Overview

This implementation plan creates a modal-based node editor for FlowForge Designer with a three-panel layout (Input | Configuration | Output). The configuration panel dynamically generates form controls based on the node's ConfigurationSchema. Implementation follows a bottom-up approach: models first, then utilities, then individual editors, and finally the modal container.

## Tasks

- [x] 1. Create configuration models and schema parser
  - [x] 1.1 Create ConfigurationProperty model in Designer/Models
    - Add `ConfigurationProperty` record with Name, DisplayName, Type, Description, IsRequired, Options properties
    - Add `NodeEditorState` record for tracking modal state
    - _Requirements: 3.1, 3.7, 3.8_

  - [x] 1.2 Create ConfigurationSchemaParser utility
    - Implement `Parse(JsonElement? schema)` to extract properties from schema
    - Implement `ExtractValues(JsonElement config, List<ConfigurationProperty> properties)` to get current values
    - Implement `BuildConfiguration(Dictionary<string, object?> values)` to serialize back to JSON
    - Handle property name to display name conversion
    - _Requirements: 4.1, 4.4_

  - [x] 1.3 Write property test for configuration round-trip
    - **Property 8: Configuration Round-Trip**
    - **Validates: Requirements 4.1, 4.4**

- [x] 2. Create individual property editor components
  - [x] 2.1 Create StringPropertyEditor component
    - Render text input for string properties
    - Support expression syntax detection and visual indication
    - Display description as helper text
    - Show required indicator when applicable
    - _Requirements: 3.2, 7.1, 7.3, 3.7, 3.8_

  - [x] 2.2 Create NumberPropertyEditor component
    - Render numeric input with type validation
    - Reject non-numeric input
    - Display validation error for invalid values
    - _Requirements: 3.3, 6.2, 6.4_

  - [x] 2.3 Create BooleanPropertyEditor component
    - Render toggle switch control
    - Support true/false state
    - _Requirements: 3.4, 6.3_

  - [x] 2.4 Create JsonPropertyEditor component
    - Render textarea for object/array properties
    - Validate JSON syntax on blur
    - Display validation errors for invalid JSON
    - _Requirements: 3.5, 3.6_

  - [x] 2.5 Create SelectPropertyEditor component
    - Render dropdown for properties with predefined options
    - Support string properties with Options defined
    - _Requirements: 6.1_

  - [x] 2.6 Write property test for type-to-editor mapping
    - **Property 5: Type-to-Editor Mapping**
    - **Validates: Requirements 3.2, 3.3, 3.4, 3.5, 3.6, 6.1**

- [x] 3. Create PropertyEditor factory component
  - [x] 3.1 Create PropertyEditor component
    - Accept ConfigurationProperty and current value as parameters
    - Render appropriate editor based on property type
    - Handle value change callbacks
    - Display required indicator and description
    - _Requirements: 3.1, 3.7, 3.8_

  - [x] 3.2 Write property test for property count matches schema
    - **Property 4: Property Count Matches Schema**
    - **Validates: Requirements 3.1**

- [x] 4. Checkpoint - Ensure property editors work correctly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Create Input and Output panel components
  - [x] 5.1 Create NodeEditorInputPanel component
    - Display input data from last execution
    - Show placeholder when no execution data exists
    - Format JSON with indentation
    - _Requirements: 2.2, 2.5_

  - [x] 5.2 Create NodeEditorOutputPanel component
    - Display output data from last execution
    - Show placeholder when no execution data exists
    - Format JSON with indentation
    - _Requirements: 2.3, 2.5_

  - [x] 5.3 Write property test for execution data display
    - **Property 3: Execution Data Display**
    - **Validates: Requirements 2.2, 2.3, 2.5**

- [x] 6. Create configuration panel with validation
  - [x] 6.1 Create NodeEditorConfigPanel component
    - Render PropertyEditor for each schema property
    - Support form view and JSON view toggle
    - Handle value changes and update local state
    - Implement required field validation
    - Display validation summary for missing required fields
    - _Requirements: 3.1, 5.1, 5.2, 5.3, 8.1_

  - [x] 6.2 Implement form/JSON mode switching
    - Serialize form values to JSON when switching to JSON mode
    - Parse JSON and populate form when switching to form mode
    - Handle invalid JSON gracefully (show error, stay in JSON mode)
    - _Requirements: 8.2, 8.3, 8.4_

  - [x] 6.3 Write property test for required field validation
    - **Property 9: Required Field Validation**
    - **Validates: Requirements 5.1, 5.2, 5.3**

  - [x] 6.4 Write property test for form/JSON mode round-trip
    - **Property 12: Form/JSON Mode Round-Trip**
    - **Validates: Requirements 8.2, 8.3, 8.4**

- [x] 7. Create the main NodeEditorModal component
  - [x] 7.1 Create NodeEditorModal component structure
    - Create modal overlay with three-panel layout
    - Add header with node name, type, and close button
    - Add save and cancel buttons in footer
    - Support closing via Escape key and outside click
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.4_

  - [x] 7.2 Implement modal state management
    - Load node data and definition when modal opens
    - Track configuration changes in local state
    - Persist changes to WorkflowStateService on save/close
    - Handle nodes without ConfigurationSchema (show JSON editor only)
    - _Requirements: 1.5, 4.2, 4.3, 9.1, 9.2_

  - [x] 7.3 Write property test for configuration persistence
    - **Property 2: Configuration Persists on Modal Close**
    - **Validates: Requirements 1.5, 4.3**

  - [x] 7.4 Write property test for schema-less fallback
    - **Property 13: Schema-less Fallback**
    - **Validates: Requirements 9.1**

- [x] 8. Integrate modal with canvas
  - [x] 8.1 Add double-click handler to CanvasNodeComponent
    - Detect double-click on node
    - Emit event to open NodeEditorModal with node ID
    - _Requirements: 1.1_

  - [x] 8.2 Add NodeEditorModal to Designer page
    - Add modal component to Designer.razor
    - Wire up open/close events
    - Pass selected node ID to modal
    - _Requirements: 1.1, 1.3, 1.4_

  - [x] 8.3 Write property test for modal opens with correct node
    - **Property 1: Modal Opens with Correct Node Context**
    - **Validates: Requirements 1.1, 1.2**

- [x] 9. Add expression support and styling
  - [x] 9.1 Implement expression detection in StringPropertyEditor
    - Detect `{{ ... }}` syntax in string values
    - Add visual indicator (icon or highlight) for expression fields
    - Display expression syntax hint
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 9.2 Write property test for expression preservation
    - **Property 11: Expression Preservation**
    - **Validates: Requirements 7.1, 7.3**

  - [x] 9.3 Add CSS styling for modal and editors
    - Style three-panel layout with flexbox
    - Style property editors consistently
    - Add validation error styling
    - Add expression indicator styling
    - Style JSON viewer panels
    - _Requirements: 2.1, 2.4, 2.5_

- [x] 10. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks including property tests are required for comprehensive testing
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- Use CsCheck for property-based testing as specified in tech stack
