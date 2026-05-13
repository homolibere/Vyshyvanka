# Code

Execute custom code to transform data, implement logic, or perform calculations.

- **Category:** Action
- **Type ID:** `code`
- **Icon:** `fa-solid fa-code`
- **Base class:** `BaseActionNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Input | `input` | Input | No |
| Output | `output` | Output | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `language` | string | No | Programming language. Options: `javascript`, `jsonata` (default: `javascript`) |
| `code` | code | Yes | Code to execute. Return a value to pass it to the output. |
| `mode` | string | No | Execution mode. Options: `runOnce`, `runForEachItem` (default: `runOnce`) |
| `timeout` | number | No | Execution timeout in seconds (default: 30) |

## Languages

### JavaScript (Jint)

General-purpose scripting in a secure sandbox powered by the Jint engine.

**Globals available:**

| Variable | Type | Description |
|----------|------|-------------|
| `input` | object | The input data from upstream nodes |
| `executionId` | string | Current execution ID |
| `workflowId` | string | Current workflow ID |
| `log(message)` | function | Log a message (appears in execution logs) |
| `getItems()` | function | Returns input as an array (wraps single values) |
| `toJson(value)` | function | Serializes a value to a JSON string |

In `runForEachItem` mode, additional globals:

| Variable | Type | Description |
|----------|------|-------------|
| `currentItem` | object | The current array element |
| `itemIndex` | number | Zero-based index of the current item |

**Sandbox limits:**
- Timeout: configurable (default 30s)
- Max statements: 100,000
- Memory limit: 64 MB
- Recursion limit: 256 levels

**Example:**
```javascript
const items = getItems();
const filtered = items.filter(item => item.status === 'active');
log(`Filtered ${filtered.length} active items`);
return { items: filtered, count: filtered.length };
```

### JSONata

A declarative expression language for JSON transformation. The full input data is the root context (`$`).

**Example:**
```
Account.Order.Product.Price.$sum()
```

See [jsonata.org](https://jsonata.org) for the full language reference.

## Execution Modes

| Mode | Behavior |
|------|----------|
| `runOnce` | Executes the code once against the full input |
| `runForEachItem` | If input is an array, executes once per element and collects results into an array |

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `result` | any | The return value of the code (or array of results in `runForEachItem` mode) |
| `logs` | string[] | Log messages captured during execution (JavaScript only) |

## Error Handling

| Error | Output |
|-------|--------|
| Empty code | `"Code cannot be empty"` |
| JavaScript runtime error | `"JavaScript error: <message>"` |
| JSONata error | `"JSONata error: <message>"` |
| Timeout | `"Code execution timed out"` |
| Cancellation | `"Code execution was cancelled"` |

## Usage

Use the Code node when you want to:

- Transform data between nodes
- Implement custom business logic
- Filter or aggregate arrays
- Perform calculations
- Format output for downstream nodes

## Expression Examples

```
{{$node.Code.data.result}}        // The code return value
{{$node.Code.data.result[0]}}     // First item (runForEachItem mode)
{{$node.Code.data.logs}}          // Execution logs
```

## Notes

- Both runtimes are sandboxed with no access to the host file system, network, or environment variables.
- Use `return` to pass a value to the output. If no value is returned, `result` will be `null`.
- In `runForEachItem` mode with JSONata, each array element becomes the root context for the expression.
- The code is wrapped in an IIFE for JavaScript, so top-level `return` statements work.
