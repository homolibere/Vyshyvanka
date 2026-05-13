# Execute Workflow

Execute another workflow as a sub-workflow, passing input data and returning the child workflow's output.

- **Category:** Action
- **Type ID:** `execute-workflow`
- **Icon:** `fa-solid fa-diagram-project`
- **Base class:** `BaseActionNode`

## Ports

| Direction | Name | Display Name | Port Type | Required |
|-----------|------|--------------|-----------|----------|
| Input | `input` | Parameters | Object | No |
| Output | `output` | Result | Object | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `workflowId` | string | Yes | ID of the workflow to execute (data source: workflows list) |
| `waitForCompletion` | boolean | No | Wait for the child workflow to complete before continuing (default: `true`) |
| `timeout` | number | No | Maximum execution time in seconds (default: 300) |

## Behavior

1. Validates the target workflow exists, is active, and is accessible to the current user.
2. Creates a child execution context with a new execution ID.
3. Passes the input data as trigger parameters to the child workflow.
4. Executes the child workflow via the workflow engine.
5. Returns the child workflow's output data and execution metadata.

**Safety checks:**
- A workflow cannot execute itself (prevents infinite recursion).
- The executing user must own the target workflow (unless running without user context, e.g., from a webhook).
- The child execution respects the configured timeout.

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `executionId` | string | The child execution's unique ID |
| `workflowId` | string | The target workflow ID |
| `workflowName` | string | The target workflow name |
| `success` | boolean | Whether the child workflow completed successfully |
| `data` | object/null | The child workflow's output data |
| `error` | string/null | Error message if the child workflow failed |
| `duration` | number | Execution duration in milliseconds |
| `nodeCount` | number | Number of nodes executed in the child workflow |

## Error Handling

| Error | Output |
|-------|--------|
| Invalid workflow ID | `"Invalid workflow ID: '<id>'"` |
| Self-execution | `"A workflow cannot execute itself..."` |
| Missing services | `"Service provider is not available..."` |
| Workflow not found | `"Workflow '<id>' not found"` |
| Inactive workflow | `"Workflow '<name>' is not active"` |
| Access denied | `"Access denied: you do not have permission..."` |
| Child failure | `"Child workflow '<name>' failed: <error>"` |
| Timeout | `"Child workflow '<name>' timed out after <n> seconds"` |

## Usage

Use the Execute Workflow node when you want to:

- Compose complex automations from reusable sub-workflows
- Separate concerns into smaller, testable workflows
- Reuse common logic across multiple parent workflows
- Implement recursive-like patterns (with different workflows)

## Expression Examples

```
{{$node.ExecuteWorkflow.data.data}}          // Child workflow output
{{$node.ExecuteWorkflow.data.success}}       // Whether child succeeded
{{$node.ExecuteWorkflow.data.duration}}      // Execution time in ms
{{$node.ExecuteWorkflow.data.executionId}}   // Child execution ID
```

## Notes

- The child workflow runs synchronously within the parent execution when `waitForCompletion` is `true`.
- Input data passed to the child workflow is available in the child's trigger node output.
- The default timeout of 300 seconds (5 minutes) prevents runaway child executions.
- Circular execution (workflow A → workflow B → workflow A) is not explicitly prevented beyond the self-execution check. Design your workflows to avoid circular dependencies.
