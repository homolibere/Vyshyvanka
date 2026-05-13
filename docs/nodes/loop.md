# Loop

Iterate over an array and process each item through downstream nodes.

- **Category:** Logic
- **Type ID:** `loop`
- **Icon:** `fa-solid fa-rotate`
- **Base class:** `BaseLogicNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input Array | Yes |
| Output | `item` | Output | — |
| Output | `done` | Loop Complete | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `field` | string | No | Field path to the array to iterate (if omitted, the entire input is treated as the array) |
| `batchSize` | number | No | Number of items to process in parallel |

## Behavior

1. Reads an array from the input data (optionally at a nested field path).
2. If the input is not an array, the node fails with an error.
3. For each element in the array, the `item` output port fires with the current item.
4. After all iterations complete, the `done` output port fires with aggregated results.

**Empty array:** If the array has zero elements, the node immediately fires the `done` port with an empty result.

**Engine integration:** The node returns a special `__loopItems` marker in its output. The workflow engine detects this marker and handles per-item iteration of downstream nodes connected to the `item` port.

## Output Data

### Per-item output (via `item` port)

Each iteration provides the current array element as the output data. The engine also injects metadata:

| Field | Type | Description |
|-------|------|-------------|
| *(element data)* | any | The current array element |
| `itemIndex` | number | Zero-based index of the current item |
| `isFirst` | boolean | Whether this is the first item |
| `isLast` | boolean | Whether this is the last item |
| `totalCount` | number | Total number of items in the array |

### Completion output (via `done` port)

| Field | Type | Description |
|-------|------|-------------|
| `items` | array | Collected outputs from all iterations |
| `totalCount` | number | Total number of items processed |
| `isComplete` | boolean | `true` |

## Usage

Use the Loop node when you want to:

- Process each item in a list individually
- Transform arrays element by element
- Send notifications to multiple recipients
- Batch-process records from a database query

## Example Configuration

Iterate over items in a nested field:

```json
{
  "field": "response.data.users"
}
```

Iterate over the entire input (when input is already an array):

```json
{}
```

## Expression Examples

```
// Inside the loop (connected to 'item' port):
{{$node.Loop.data}}                  // Current item
{{$node.Loop.data.itemIndex}}        // Current index

// After the loop (connected to 'done' port):
{{$node.Loop.data.items}}            // All collected results
{{$node.Loop.data.totalCount}}       // Number of items processed
```

## Notes

- The input must be an array (or contain an array at the specified `field` path). Non-array inputs cause a failure.
- Nodes connected to the `item` port execute once per array element.
- Nodes connected to the `done` port execute once after all iterations complete.
- The `batchSize` configuration controls parallel processing — items within a batch may execute concurrently.
- Be cautious with large arrays and expensive downstream operations. Consider using `batchSize` to control concurrency.
