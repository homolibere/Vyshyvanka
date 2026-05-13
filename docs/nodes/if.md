# If

Evaluate a condition and route data to the true or false output branch.

- **Category:** Logic
- **Type ID:** `if`
- **Icon:** `fa-solid fa-code-branch`
- **Base class:** `BaseLogicNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | Yes |
| Output | `true` | True | — |
| Output | `false` | False | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `field` | string | Yes | Field path to evaluate (supports dot notation for nested access) |
| `operator` | string | Yes | Comparison operator (see table below) |
| `value` | string | No | Value to compare against |

## Operators

| Operator | Description | Requires `value` |
|----------|-------------|:----------------:|
| `equals` | Field equals value | Yes |
| `notEquals` | Field does not equal value | Yes |
| `greaterThan` | Field is greater than value | Yes |
| `lessThan` | Field is less than value | Yes |
| `greaterThanOrEqual` | Field is greater than or equal to value | Yes |
| `lessThanOrEqual` | Field is less than or equal to value | Yes |
| `contains` | Field contains value as substring | Yes |
| `startsWith` | Field starts with value | Yes |
| `endsWith` | Field ends with value | Yes |
| `isEmpty` | Field is empty/null/undefined | No |
| `isNotEmpty` | Field is not empty | No |
| `isTrue` | Field is boolean true | No |
| `isFalse` | Field is boolean false | No |
| `isNull` | Field is null or undefined | No |
| `isNotNull` | Field is not null | No |

## Behavior

1. Reads the value at the configured `field` path from the input data.
2. Applies the `operator` comparison against the configured `value`.
3. Routes the full input data to either the `true` or `false` output port.
4. Downstream nodes connected to the inactive branch are skipped entirely.

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `data` | object | The original input data (passed through) |
| `conditionResult` | boolean | The evaluation result |
| `outputPort` | string | `"true"` or `"false"` — indicates which branch is active |

## Usage

Use the If node when you want to:

- Branch workflow logic based on a condition
- Filter items that meet specific criteria
- Handle success/failure paths differently
- Implement conditional processing

## Expression Examples

```
{{$node.If.data.data}}              // The original input (passed through)
{{$node.If.data.conditionResult}}   // true or false
{{$node.If.data.outputPort}}        // "true" or "false"
```

## Example Configuration

Check if an HTTP response was successful:

```json
{
  "field": "isSuccess",
  "operator": "equals",
  "value": true
}
```

Check if a user's age is at least 18:

```json
{
  "field": "user.age",
  "operator": "greaterThanOrEqual",
  "value": 18
}
```

## Notes

- The `field` property supports dot notation for nested access (e.g., `user.address.city`).
- String comparisons are case-sensitive for `equals`/`notEquals` and case-insensitive for `contains`/`startsWith`/`endsWith`.
- Numeric comparisons parse both the field value and the compare value as numbers.
- If the field path does not exist in the input, the value is treated as `null`/`undefined`.
