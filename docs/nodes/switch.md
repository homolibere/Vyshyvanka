# Switch

Route data to different outputs based on matching case values.

- **Category:** Logic
- **Type ID:** `switch`
- **Icon:** `fa-solid fa-shuffle`
- **Base class:** `BaseLogicNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | Yes |
| Output | `default` | Default | — |
| Output | *(dynamic)* | *(per case)* | — |

Output ports are created dynamically based on the configured cases.

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `field` | string | Yes | Field path to evaluate (supports dot notation) |
| `cases` | array | No | Array of case definitions |

### Case Definition

Each case in the `cases` array is an object:

| Field | Type | Description |
|-------|------|-------------|
| `value` | any | Value to match against |
| `output` | string | Output port name when matched (defaults to the string representation of `value`) |

## Behavior

1. Reads the value at the configured `field` path from the input data.
2. If the field resolves to a value (e.g., via an expression like `{{ input.body.event_type }}`) rather than a dot-path, that value is used directly for matching.
3. Iterates through the `cases` array in order.
4. The first case whose `value` matches the field value determines the output port.
5. If no case matches, data routes to the `default` port.
6. Downstream nodes on inactive branches are skipped entirely.

**Matching rules:**
- String comparison is case-insensitive.
- Numeric values are compared as decimals.
- Boolean values match their string equivalents (`"true"`, `"false"`).
- `null` matches when the field is null or undefined.

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `data` | object | The original input data (passed through) |
| `matchedCase` | string | The name of the matched case (or `"default"`) |
| `outputPort` | string | The active output port name |

## Usage

Use the Switch node when you want to:

- Route data to different processing paths based on a value
- Implement multi-way branching (more than true/false)
- Handle different event types or status codes differently
- Dispatch to different handlers based on a category

## Example Configuration

Route based on HTTP status code category:

```json
{
  "field": "statusCode",
  "cases": [
    { "value": 200, "output": "success" },
    { "value": 404, "output": "notFound" },
    { "value": 500, "output": "serverError" }
  ]
}
```

Route based on event type:

```json
{
  "field": "event.type",
  "cases": [
    { "value": "created", "output": "onCreate" },
    { "value": "updated", "output": "onUpdate" },
    { "value": "deleted", "output": "onDelete" }
  ]
}
```

## Expression Examples

```
{{ nodes.Switch.data }}               // The original input
{{ nodes.Switch.matchedCase }}        // Which case matched
{{ nodes.Switch.outputPort }}         // Active output port
```

## Notes

- Cases are evaluated in order — the first match wins.
- If no `output` is specified in a case, the string representation of `value` is used as the port name.
- The `default` output port always exists and is used when no case matches.
- The `field` property supports dot notation for nested access (e.g., `response.status`).
- The `field` property also accepts expressions (e.g., `{{ input.body.object_kind }}`). When an expression resolves to a plain value, that value is matched against cases directly without further path navigation.
