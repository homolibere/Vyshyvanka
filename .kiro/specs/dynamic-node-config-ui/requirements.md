# Requirements Document

## Introduction

This feature enhances the FlowForge Designer's node configuration experience by introducing a modal-based node editor similar to n8n. When a user double-clicks a node, a full-screen modal opens with a three-panel layout: input data on the left, configuration in the center, and output data on the right. The configuration panel dynamically generates form controls based on the `ConfigurationProperty` attributes from node definitions. This provides more space for complex configurations and makes node editing more intuitive.

## Glossary

- **Node_Editor_Modal**: A full-screen modal dialog that opens when editing a node, containing three panels for input, configuration, and output
- **Configuration_Schema**: JSON schema derived from `ConfigurationProperty` attributes that defines the structure, types, and requirements of node configuration
- **Property_Editor**: A UI component that renders an appropriate input control based on property type (string, number, boolean, object, array)
- **Node_Definition**: Metadata about a node type including its inputs, outputs, and configuration schema
- **Dynamic_Form**: A form generated at runtime based on the configuration schema rather than hardcoded fields
- **Input_Panel**: Left panel in the modal showing input data from connected nodes or last execution
- **Configuration_Panel**: Center panel in the modal containing the dynamic form for node settings
- **Output_Panel**: Right panel in the modal showing output data from last execution

## Requirements

### Requirement 1: Node Editor Modal Opening

**User Story:** As a workflow designer, I want to open a dedicated node editor when I double-click a node, so that I have more space to configure complex nodes.

#### Acceptance Criteria

1. WHEN a user double-clicks a node on the canvas, THE Node_Editor_Modal SHALL open displaying the selected node's configuration
2. WHEN the Node_Editor_Modal opens, THE Node_Editor_Modal SHALL display the node name and type in the header
3. THE Node_Editor_Modal SHALL provide a close button to dismiss the modal
4. WHEN the user clicks outside the modal or presses Escape, THE Node_Editor_Modal SHALL close
5. WHEN the Node_Editor_Modal closes, THE Node_Editor_Modal SHALL save any pending configuration changes

### Requirement 2: Three-Panel Layout

**User Story:** As a workflow designer, I want to see input data, configuration, and output data side by side, so that I can understand the data flow while configuring the node.

#### Acceptance Criteria

1. THE Node_Editor_Modal SHALL display three panels: Input_Panel on the left, Configuration_Panel in the center, and Output_Panel on the right
2. THE Input_Panel SHALL display the input data from the last execution or a placeholder if no execution data exists
3. THE Output_Panel SHALL display the output data from the last execution or a placeholder if no execution data exists
4. THE Configuration_Panel SHALL occupy the center panel and contain the dynamic form for node settings
5. WHEN execution data is available, THE Input_Panel and Output_Panel SHALL format JSON data with syntax highlighting

### Requirement 3: Schema-Driven Property Rendering

**User Story:** As a workflow designer, I want to see individual form fields for each node configuration property, so that I can easily configure nodes without writing JSON manually.

#### Acceptance Criteria

1. WHEN a node has a ConfigurationSchema, THE Configuration_Panel SHALL render a Property_Editor for each property defined in the schema
2. WHEN a property has type "string", THE Property_Editor SHALL render a text input field
3. WHEN a property has type "number", THE Property_Editor SHALL render a numeric input field
4. WHEN a property has type "boolean", THE Property_Editor SHALL render a toggle switch control
5. WHEN a property has type "object", THE Property_Editor SHALL render a JSON editor textarea for that property
6. WHEN a property has type "array", THE Property_Editor SHALL render a JSON editor textarea for that property
7. WHEN a property is marked as required in the schema, THE Property_Editor SHALL display a required indicator (asterisk)
8. WHEN a property has a description in the schema, THE Property_Editor SHALL display the description as helper text below the input

### Requirement 4: Property Value Binding

**User Story:** As a workflow designer, I want my configuration changes to be reflected immediately in the node state, so that I can see the effect of my changes.

#### Acceptance Criteria

1. WHEN the Node_Editor_Modal opens, THE Configuration_Panel SHALL populate each Property_Editor with the current value from the node's configuration
2. WHEN a user modifies a Property_Editor value, THE Configuration_Panel SHALL update the local state immediately
3. WHEN the user closes the modal or clicks Save, THE Configuration_Panel SHALL persist all changes to the WorkflowStateService
4. THE Configuration_Panel SHALL serialize all property values back to a valid JSON configuration object

### Requirement 5: Required Field Validation

**User Story:** As a workflow designer, I want to be notified when required configuration fields are missing, so that I can ensure my nodes are properly configured before execution.

#### Acceptance Criteria

1. WHEN a required property has no value or an empty value, THE Property_Editor SHALL display a validation error indicator
2. WHEN a user attempts to save configuration with missing required fields, THE Configuration_Panel SHALL display an error message listing the missing fields
3. WHEN all required fields have valid values, THE Configuration_Panel SHALL allow the configuration to be saved without error

### Requirement 6: Type-Specific Input Controls

**User Story:** As a workflow designer, I want input controls that match the expected data type, so that I can enter valid values more easily.

#### Acceptance Criteria

1. WHEN a string property has known options (e.g., HTTP method: GET, POST, PUT, DELETE), THE Property_Editor SHALL render a dropdown select control
2. WHEN a number property is rendered, THE Property_Editor SHALL only accept numeric input
3. WHEN a boolean property is rendered, THE Property_Editor SHALL provide a clear toggle switch
4. IF a user enters an invalid value for a property type, THEN THE Property_Editor SHALL display a type validation error

### Requirement 7: Expression Support in String Fields

**User Story:** As a workflow designer, I want to use expressions in string configuration fields, so that I can reference data from previous nodes.

#### Acceptance Criteria

1. WHEN a string Property_Editor contains expression syntax (e.g., `{{ nodes.nodeName.output.field }}`), THE Property_Editor SHALL accept and preserve the expression
2. THE Configuration_Panel SHALL display a hint about expression syntax for string fields
3. WHEN an expression is detected in a string field, THE Property_Editor SHALL provide visual indication that the field contains an expression

### Requirement 8: Fallback to JSON Editor

**User Story:** As an advanced user, I want the option to edit the raw JSON configuration, so that I can handle complex scenarios not covered by the dynamic form.

#### Acceptance Criteria

1. THE Configuration_Panel SHALL provide a toggle to switch between form view and raw JSON view
2. WHEN switching from form view to JSON view, THE Configuration_Panel SHALL serialize the current form values to JSON
3. WHEN switching from JSON view to form view, THE Configuration_Panel SHALL parse the JSON and populate the form fields
4. IF the JSON is invalid when switching to form view, THEN THE Configuration_Panel SHALL display an error and remain in JSON view

### Requirement 9: Nodes Without Configuration Schema

**User Story:** As a workflow designer, I want a consistent experience even for nodes that don't have a defined configuration schema.

#### Acceptance Criteria

1. WHEN a node has no ConfigurationSchema (null or empty), THE Configuration_Panel SHALL display only the raw JSON editor
2. WHEN a node has an empty configuration schema with no properties, THE Configuration_Panel SHALL display a message indicating no configuration is required
