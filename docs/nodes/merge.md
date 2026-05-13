# Merge

Merge data from multiple input branches into a single output.

- **Category:** Logic
- **Type ID:** `merge`
- **Icon:** `fa-solid fa-code-merge`
- **Base class:** `BaseLogicNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input1` | Input 1 | No |
| Input | `input2` | Input 2 | No |
| Output | `output` | Merged Output | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `mode` | string | No | Merge mode. Options: `passThrough`, `waitAll`, `combine` (default: `passThrough`) |
| `combineMode` | string | No | How to combine inputs. Options: `array`, `object`, `append` (default: `array`) |

## Modes

### passThrough

Forwards each input as-is without waiting for other inputs. Each time an input arrives, it is immediately passed to the output.

### waitAll

Waits for all expected inputs before emitting a combined result. The number of expected inputs defaults to 2 but can be configured.

### combine

Accumulates inputs and emits the combined result each time a new input arrives. The combination grows as more inputs are received.

## Combine Modes

Used by `waitAll` and `combine` modes to determine how multiple inputs are merged:

| Combine Mode | Behavior |
|-------------|----------|
| `array` | Collects all inputs into an array |
| `object` | Merges object properties (later inputs overwrite earlier ones for duplicate keys) |
| `append` | Flattens arrays and appends all elements into a single list |

### array example

Inputs: `{"name": "Alice"}` and `{"name": "Bob"}`

Output: `[{"name": "Alice"}, {"name": "Bob"}]`

### object example

Inputs: `{"a": 1, "b": 2}` and `{"b": 3, "c": 4}`

Output: `{"a": 1, "b": 3, "c": 4}`

### append example

Inputs: `[1, 2, 3]` and `[4, 5, 6]`

Output: `[1, 2, 3, 4, 5, 6]`

Non-array inputs are appended as individual elements.

## Output Data

Depends on the mode and combine mode:

- **passThrough:** The input data as-is.
- **waitAll/combine with `array`:** An array of all input values.
- **waitAll/combine with `object`:** A merged object with all properties.
- **waitAll/combine with `append`:** A flattened array of all elements.

When `waitAll` is still waiting for inputs, a status object is returned:

```json
{
  "status": "waiting",
  "received": 1
}
```

## Usage

Use the Merge node when you want to:

- Rejoin branches after an If or Switch node
- Combine results from parallel operations
- Aggregate data from multiple sources
- Wait for multiple async operations to complete

## Expression Examples

```
{{$node.Merge.data}}           // Merged output
{{$node.Merge.data[0]}}        // First input (array mode)
{{$node.Merge.data.fieldName}} // Merged field (object mode)
```

## Notes

- In `passThrough` mode, the node fires its output every time any input arrives — it does not wait.
- In `waitAll` mode, the node holds state and only fires after all expected inputs have arrived.
- The `object` combine mode performs a shallow merge. Nested objects are not deep-merged.
- For `append` mode, if an input is an array, its elements are flattened into the result. Non-array inputs are added as single elements.
- The Merge node is typically placed after branching nodes (If, Switch) to rejoin the workflow into a single path.
