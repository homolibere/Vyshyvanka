# Manual Trigger

Manually trigger a workflow execution from the UI or via the API.

- **Category:** Trigger
- **Type ID:** `manual-trigger`
- **Icon:** `fa-solid fa-play`
- **Base class:** `BaseTriggerNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Output | `output` | Output | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `testData` | object | No | Custom JSON payload to output when triggered. Leave empty to pass through trigger data. |

## Behavior

The Manual Trigger always fires when invoked. It determines its output using the following priority:

1. **Runtime trigger data** — if data is provided at trigger time (e.g., via the API request body), it is passed through as-is.
2. **Configured test data** — if no runtime data is present but `testData` is configured, that value is used.
3. **Default payload** — if neither is available, a default payload is emitted:

```json
{
  "triggered": true,
  "timestamp": "2025-01-15T10:30:00Z"
}
```

## Output Data

The output depends on the source (see Behavior above). When using the default payload:

| Field | Type | Description |
|-------|------|-------------|
| `triggered` | boolean | Always `true` |
| `timestamp` | string | UTC timestamp of execution |

## Usage

Use the Manual Trigger when you want to:

- Test a workflow during development
- Execute a workflow on-demand from the UI
- Trigger a workflow via the REST API with custom input data

## Expression Examples

```
{{$node.ManualTrigger.data}}           // Full trigger output
{{$node.ManualTrigger.data.timestamp}} // Trigger timestamp
```
