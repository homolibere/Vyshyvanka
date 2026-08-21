# Schedule Trigger

Trigger a workflow on a recurring schedule using cron expressions or fixed intervals.

- **Category:** Trigger
- **Type ID:** `schedule-trigger`
- **Icon:** `fa-solid fa-clock`
- **Base class:** `BaseTriggerNode`

## Ports

| Direction | Name | Display Name | Required |
|-----------|------|--------------|----------|
| Output | `output` | Schedule Data | — |

## Configuration

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `cronExpression` | string | No | Cron expression for scheduling (5-field format: minute hour day month dayOfWeek) |
| `interval` | number | No | Interval in seconds (alternative to cron) |
| `timezone` | string | No | Timezone for schedule evaluation (default: `UTC`) |

Provide either `cronExpression` or `interval`, not both.

## Behavior

The Schedule Trigger is driven by the platform's workflow scheduler — a hosted background service that polls every 30 seconds. On each poll it evaluates all workflows whose status is `Active` and that contain a schedule-trigger node, and fires the trigger when its configured schedule is due:

- `cronExpression` — a standard 5-field cron expression, parsed with the Cronos library.
- `interval` — a fixed interval in seconds.

Due times are computed in the workflow's configured `timezone` (default `UTC`).

Notes:
- The workflow must be `Active` for the schedule to fire — inactive workflows are ignored by the scheduler.
- Runs are not backfilled: any schedule occurrences that fall due while the host is down are skipped rather than replayed on restart.

Trigger validation:
- The trigger context must contain schedule data with `triggerType` equal to `"schedule"`
- If a `scheduledTime` is provided, it must be within a 60-second tolerance of the current time

## Output Data

| Field | Type | Description |
|-------|------|-------------|
| `triggeredAt` | string | UTC timestamp when the trigger fired |
| `cronExpression` | string/null | The configured cron expression |
| `interval` | number/null | The configured interval in seconds |
| `timezone` | string | The configured timezone |
| `executionId` | string | The execution ID for this run |

## Cron Expression Format

Standard 5-field cron format:

```
┌───────────── minute (0–59)
│ ┌───────────── hour (0–23)
│ │ ┌───────────── day of month (1–31)
│ │ │ ┌───────────── month (1–12)
│ │ │ │ ┌───────────── day of week (0–6, Sunday = 0)
│ │ │ │ │
* * * * *
```

Examples:
- `0 9 * * 1-5` — every weekday at 9:00 AM
- `*/15 * * * *` — every 15 minutes
- `0 0 1 * *` — first day of every month at midnight
- `30 14 * * 3` — every Wednesday at 2:30 PM

## Usage

Use the Schedule Trigger when you want to:

- Run periodic data synchronization
- Generate scheduled reports
- Perform routine maintenance tasks
- Poll external systems at regular intervals

## Expression Examples

```
{{ nodes.ScheduleTrigger.triggeredAt }}        // When the schedule fired
{{ nodes.ScheduleTrigger.cronExpression }}     // The cron expression
{{ nodes.ScheduleTrigger.executionId }}        // Current execution ID
```
