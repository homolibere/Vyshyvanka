# Design Document: Dynamic Node Configuration UI

## Overview

This design introduces a modal-based node editor for FlowForge Designer, inspired by n8n's approach. The modal provides a three-panel layout with input data on the left, a dynamic configuration form in the center, and output data on the right. The configuration form is generated dynamically from the node's `ConfigurationSchema`, which is derived from `ConfigurationProperty` attributes on node classes.

## Architecture

The feature follows the existing Blazor component architecture in FlowForge.Designer:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         NodeEditorModal                                  │
│  ┌──────────────────┬────────────────────────┬──────────────────┐       │
│  │   InputPanel     │   ConfigurationPanel   │   OutputPanel    │       │
│  │                  │                        │                  │       │
│  │  - Last input    │  - PropertyEditor[]    │  - Last output   │       │
│  │  - JSON viewer   │  - Validation          │  - JSON viewer   │       │
│  │                  │  - JSON toggle         │                  │       │
│  └──────────────────┴────────────────────────┴──────────────────┘       │
└─────────────────────────────────────────────────────────────────────────┘
```

### Component Hierarchy

```
Designer.razor
└── WorkflowCanvas.razor
    └── CanvasNodeComponent.razor (double-click triggers modal)
└── NodeEditorModal.razor (new)
    ├── NodeEditorInputPanel.razor (new)
    ├── NodeEditorConfigPanel.razor (new)
    │   └── PropertyEditor.razor (new)
    │       ├── StringPropertyEditor.razor (new)
    │       ├── NumberPropertyEditor.razor (new)
    │       ├── BooleanPropertyEditor.razor (new)
    │       └── JsonPropertyEditor.razor (new)
    └── NodeEditorOutputPanel.razor (new)
```

## Components and Interfaces

### NodeEditorModal Component

The main modal container that orchestrates the three-panel layout.

```csharp
// NodeEditorModal.razor.cs
public partial class NodeEditorModal : ComponentBase, IDisposable
{
    [Inject] private WorkflowStateService StateService { get; set; } = null!;
    
    [Parameter] public string? NodeId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    
    private WorkflowNode? _node;
    private NodeDefinition? _definition;
    private NodeExecutionState? _executionState;
    private Dictionary<string, object?> _configValues = new();
    private bool _isJsonMode;
    private string? _validationError;
    
    public void Open(string nodeId);
    public void Close();
    private void SaveConfiguration();
    private void OnPropertyChanged(string propertyName, object? value);
    private void ToggleJsonMode();
}
```

### PropertyEditor Component

A factory component that renders the appropriate editor based on property type.

```csharp
// PropertyEditor.razor.cs
public partial class PropertyEditor : ComponentBase
{
    [Parameter, EditorRequired] public ConfigurationProperty Property { get; set; } = null!;
    [Parameter] public object? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }
    [Parameter] public bool ShowValidationError { get; set; }
}
```

### ConfigurationProperty Model

A model to represent parsed schema properties for the UI.

```csharp
// Models/ConfigurationProperty.cs
public record ConfigurationProperty
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "string";
    public string? Description { get; init; }
    public bool IsRequired { get; init; }
    public List<string>? Options { get; init; }  // For dropdown support
}
```

### Schema Parser Service

Utility to parse the JsonElement ConfigurationSchema into strongly-typed models.

```csharp
// Services/ConfigurationSchemaParser.cs
public static class ConfigurationSchemaParser
{
    public static List<ConfigurationProperty> Parse(JsonElement? schema);
    public static Dictionary<string, object?> ExtractValues(JsonElement config, List<ConfigurationProperty> properties);
    public static JsonElement BuildConfiguration(Dictionary<string, object?> values);
}
```

## Data Models

### ConfigurationProperty Record

```csharp
namespace FlowForge.Designer.Models;

/// <summary>
/// Represents a single configuration property parsed from the node's schema.
/// </summary>
public record ConfigurationProperty
{
    /// <summary>Property name (key in configuration JSON).</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Display label for the property.</summary>
    public string DisplayName { get; init; } = string.Empty;
    
    /// <summary>Property type: string, number, boolean, object, array.</summary>
    public string Type { get; init; } = "string";
    
    /// <summary>Description shown as helper text.</summary>
    public string? Description { get; init; }
    
    /// <summary>Whether this property is required.</summary>
    public bool IsRequired { get; init; }
    
    /// <summary>Predefined options for dropdown (null for free-form input).</summary>
    public List<string>? Options { get; init; }
}
```

### NodeEditorState Record

```csharp
namespace FlowForge.Designer.Models;

/// <summary>
/// Tracks the state of the node editor modal.
/// </summary>
public record NodeEditorState
{
    /// <summary>ID of the node being edited.</summary>
    public string NodeId { get; init; } = string.Empty;
    
    /// <summary>Current configuration values by property name.</summary>
    public Dictionary<string, object?> Values { get; init; } = new();
    
    /// <summary>Whether the editor is in JSON mode.</summary>
    public bool IsJsonMode { get; init; }
    
    /// <summary>Raw JSON string when in JSON mode.</summary>
    public string? RawJson { get; init; }
    
    /// <summary>Validation errors by property name.</summary>
    public Dictionary<string, string> ValidationErrors { get; init; } = new();
    
    /// <summary>Whether there are unsaved changes.</summary>
    public bool IsDirty { get; init; }
}
```



## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Modal Opens with Correct Node Context

*For any* node on the canvas, when the user double-clicks it, the Node_Editor_Modal should open and display that node's name and type in the header.

**Validates: Requirements 1.1, 1.2**

### Property 2: Configuration Persists on Modal Close

*For any* set of configuration changes made in the modal, when the modal is closed, those changes should be persisted to the WorkflowStateService and reflected in the node's configuration.

**Validates: Requirements 1.5, 4.3**

### Property 3: Execution Data Display

*For any* node with execution state, the Input_Panel should display the input data and the Output_Panel should display the output data. For nodes without execution state, both panels should show placeholder content.

**Validates: Requirements 2.2, 2.3, 2.5**

### Property 4: Property Count Matches Schema

*For any* node with a ConfigurationSchema containing N properties, the Configuration_Panel should render exactly N Property_Editor components.

**Validates: Requirements 3.1**

### Property 5: Type-to-Editor Mapping

*For any* configuration property, the Property_Editor should render the correct input control based on the property type:
- "string" → text input (or dropdown if options defined)
- "number" → numeric input
- "boolean" → toggle switch
- "object" → JSON editor
- "array" → JSON editor

**Validates: Requirements 3.2, 3.3, 3.4, 3.5, 3.6, 6.1**

### Property 6: Required Indicator Display

*For any* property marked as required in the schema, the Property_Editor should display a required indicator (asterisk).

**Validates: Requirements 3.7**

### Property 7: Description Display

*For any* property with a description in the schema, the Property_Editor should display that description as helper text.

**Validates: Requirements 3.8**

### Property 8: Configuration Round-Trip

*For any* valid node configuration, opening the modal should populate all Property_Editors with the correct values, and serializing those values back should produce an equivalent JSON configuration.

**Validates: Requirements 4.1, 4.4**

### Property 9: Required Field Validation

*For any* configuration where a required property has no value or an empty value, the Property_Editor should display a validation error, and attempting to save should fail with an error message listing the missing fields.

**Validates: Requirements 5.1, 5.2, 5.3**

### Property 10: Type Validation

*For any* number property, the Property_Editor should reject non-numeric input and display a validation error for invalid values.

**Validates: Requirements 6.2, 6.4**

### Property 11: Expression Preservation

*For any* string value containing expression syntax (e.g., `{{ nodes.nodeName.output.field }}`), the Property_Editor should preserve the expression through editing and provide visual indication that the field contains an expression.

**Validates: Requirements 7.1, 7.3**

### Property 12: Form/JSON Mode Round-Trip

*For any* valid form configuration, switching to JSON mode should produce valid JSON, and switching back to form mode should restore the same values. For invalid JSON, switching to form mode should fail gracefully and remain in JSON mode.

**Validates: Requirements 8.2, 8.3, 8.4**

### Property 13: Schema-less Fallback

*For any* node without a ConfigurationSchema (null or empty), the Configuration_Panel should display only the raw JSON editor.

**Validates: Requirements 9.1**

## Error Handling

### Invalid JSON in JSON Mode

When the user enters invalid JSON in JSON mode and attempts to switch to form mode:
1. Parse the JSON and catch `JsonException`
2. Display an error message below the JSON editor
3. Keep the modal in JSON mode
4. Do not modify the form state

### Missing Required Fields

When the user attempts to save with missing required fields:
1. Collect all required properties with empty/null values
2. Display a validation summary at the top of the Configuration_Panel
3. Highlight each invalid Property_Editor with an error state
4. Prevent the save operation until all required fields are filled

### Type Conversion Errors

When a value cannot be converted to the expected type:
1. Display an inline error on the Property_Editor
2. Keep the invalid value in the input for user correction
3. Prevent save until the error is resolved

### Node Not Found

If the modal is opened with an invalid node ID:
1. Display an error message in the modal
2. Provide only a close button
3. Log the error for debugging

## Testing Strategy

### Unit Tests

Unit tests should cover:
- `ConfigurationSchemaParser.Parse()` with various schema structures
- `ConfigurationSchemaParser.ExtractValues()` with different value types
- `ConfigurationSchemaParser.BuildConfiguration()` serialization
- Individual Property_Editor components with different property types
- Validation logic for required fields and type checking

### Property-Based Tests

Property-based tests using CsCheck should verify:
- **Property 4**: Schema property count matches rendered editors
- **Property 5**: Type-to-editor mapping is consistent
- **Property 8**: Configuration round-trip preserves values
- **Property 9**: Required field validation catches all missing fields
- **Property 11**: Expression syntax is preserved through editing
- **Property 12**: Form/JSON mode round-trip preserves data
- **Property 13**: Schema-less nodes show JSON editor only

### Integration Tests

Integration tests should cover:
- Modal opening via double-click on canvas node
- Configuration persistence through WorkflowStateService
- Execution data display in Input/Output panels
- Full workflow: open modal → edit → save → verify node updated

### Test Configuration

- Use CsCheck for property-based testing
- Minimum 100 iterations per property test
- Tag format: **Feature: dynamic-node-config-ui, Property {number}: {property_text}**
