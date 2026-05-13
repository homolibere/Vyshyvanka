# Node Reference

This folder contains documentation for every built-in node available in Vyshyvanka.

## Triggers

Trigger nodes start a workflow execution. Every workflow must have exactly one trigger node.

| Node | Type ID | Description |
|------|---------|-------------|
| [Manual Trigger](manual-trigger.md) | `manual-trigger` | Manually trigger a workflow via the UI or API |
| [Webhook Trigger](webhook-trigger.md) | `webhook-trigger` | Trigger when an HTTP request is received |
| [Schedule Trigger](schedule-trigger.md) | `schedule-trigger` | Trigger on a cron schedule or interval |

## Actions

Action nodes perform operations — call APIs, query databases, send emails, etc.

| Node | Type ID | Description |
|------|---------|-------------|
| [HTTP Request](http-request.md) | `http-request` | Make HTTP requests to external APIs |
| [Code](code.md) | `code` | Execute JavaScript or JSONata to transform data |
| [Database Query](database-query.md) | `database-query` | Execute SQL queries against a database |
| [Send Email](email-send.md) | `email-send` | Send emails via SMTP |
| [Execute Workflow](execute-workflow.md) | `execute-workflow` | Execute another workflow as a sub-workflow |
| [File Operations](file-operations.md) | `file-operations` | Read, write, and manage files |

## Logic

Logic nodes control the flow of execution — branching, looping, and merging.

| Node | Type ID | Description |
|------|---------|-------------|
| [If](if.md) | `if` | Evaluate a condition and route to true/false |
| [Switch](switch.md) | `switch` | Route data based on matching case values |
| [Loop](loop.md) | `loop` | Iterate over an array |
| [Merge](merge.md) | `merge` | Merge data from multiple branches |
