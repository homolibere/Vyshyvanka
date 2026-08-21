using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Persistence;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Engine.Scheduling;

/// <summary>
/// Hosted background service that fires schedule-triggered workflows.
/// On each tick it loads active workflows containing a schedule-trigger node, computes each one's
/// next fire time from its cron expression or interval, and dispatches an execution (mode
/// <see cref="ExecutionMode.Scheduled"/>) for any that are due. Missed fires while the host was
/// down are skipped (next occurrence is computed forward from now).
/// </summary>
/// <remarks>
/// Single-instance design: assumes one scheduler runs per deployment. See the SDD for the
/// multi-instance hardening path (advisory lock + cursor optimistic guard).
/// </remarks>
public sealed class WorkflowSchedulerService(
    IServiceScopeFactory scopeFactory,
    ISchedulePlanner planner,
    ILogger<WorkflowSchedulerService> logger) : BackgroundService
{
    private const string ScheduleTriggerType = "schedule-trigger";

    /// <summary>How often the scheduler wakes to evaluate due workflows.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Workflow scheduler started (poll interval {Seconds}s)", PollInterval.TotalSeconds);

        using var timer = new PeriodicTimer(PollInterval);

        // Run one pass immediately, then on each tick.
        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let a single bad tick tear down the service.
                logger.LogError(ex, "Workflow scheduler tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        logger.LogInformation("Workflow scheduler stopped");
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VyshyvankaDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkflowRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
        var executionRepository = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var credentialService = scope.ServiceProvider.GetService<ICredentialService>();

        var now = DateTime.UtcNow;

        // Candidate entities: Active, and their NodesJson mentions the schedule-trigger type.
        // The type filter narrows the set in SQL; exact node inspection happens on the model below.
        var candidates = await db.Workflows
            .Where(w => w.Status == WorkflowStatus.Active
                        && EF.Functions.Like(w.NodesJson, $"%\"{ScheduleTriggerType}\"%"))
            .ToListAsync(cancellationToken);

        foreach (var entity in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var workflow = await repository.GetByIdAsync(entity.Id, cancellationToken);
            if (workflow is null)
            {
                continue;
            }

            var scheduleNode = workflow.Nodes.FirstOrDefault(n =>
                n.Type.Equals(ScheduleTriggerType, StringComparison.OrdinalIgnoreCase));
            if (scheduleNode is null)
            {
                continue;
            }

            var (cron, interval, timezone) = ReadScheduleConfig(scheduleNode);
            if (cron is null && interval is null)
            {
                continue;
            }

            // Compute the next fire relative to the persisted cursor (or activation baseline / now).
            var baseline = entity.LastScheduledFireAt ?? entity.UpdatedAt;
            if (baseline > now)
            {
                baseline = now;
            }

            var nextFire = planner.GetNextOccurrence(cron, interval, baseline, timezone ?? "UTC");
            if (nextFire is null || nextFire.Value > now)
            {
                continue; // not due yet
            }

            // Overlap guard: skip if a prior scheduled run for this workflow is still in flight.
            var hasRunning = await db.Executions.AnyAsync(
                e => e.WorkflowId == workflow.Id
                     && (e.Status == ExecutionStatus.Running || e.Status == ExecutionStatus.Pending),
                cancellationToken);
            if (hasRunning)
            {
                logger.LogDebug("Skipping scheduled run for {WorkflowId}: a prior run is still active", workflow.Id);
                continue;
            }

            await DispatchAsync(workflow, scope.ServiceProvider, engine, credentialService, nextFire.Value, cancellationToken);

            // Advance the cursor so the next tick computes the following occurrence.
            entity.LastScheduledFireAt = nextFire.Value;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task DispatchAsync(
        Workflow workflow,
        IServiceProvider services,
        IWorkflowEngine engine,
        ICredentialService? credentialService,
        DateTime scheduledTime,
        CancellationToken cancellationToken)
    {
        var executionId = Guid.NewGuid();

        ICredentialProvider credentialProvider = credentialService is not null
            ? new OwnerCredentialProvider(credentialService, workflow.CreatedBy)
            : NullCredentialProvider.Instance;

        var context = new ExecutionContext(
            executionId,
            workflow.Id,
            credentialProvider,
            cancellationToken,
            services,
            workflow.CreatedBy,
            logger);

        // Populate the trigger context so ScheduleTriggerNode.ShouldTriggerAsync fires.
        context.Variables["triggerType"] = "schedule";
        context.Variables["scheduledTime"] = scheduledTime;

        logger.LogInformation(
            "Dispatching scheduled execution {ExecutionId} for workflow {WorkflowId} (scheduled {ScheduledTime:o})",
            executionId, workflow.Id, scheduledTime);

        try
        {
            await engine.ExecuteAsync(workflow, context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled execution {ExecutionId} for workflow {WorkflowId} failed",
                executionId, workflow.Id);
        }
    }

    private static (string? Cron, int? Interval, string? Timezone) ReadScheduleConfig(WorkflowNode node)
    {
        if (node.Configuration.ValueKind != JsonValueKind.Object)
        {
            return (null, null, null);
        }

        string? cron = null;
        int? interval = null;
        string? timezone = null;

        if (node.Configuration.TryGetProperty("cronExpression", out var cronProp)
            && cronProp.ValueKind == JsonValueKind.String)
        {
            cron = cronProp.GetString();
        }

        if (node.Configuration.TryGetProperty("interval", out var intervalProp)
            && intervalProp.ValueKind == JsonValueKind.Number
            && intervalProp.TryGetInt32(out var intervalValue))
        {
            interval = intervalValue;
        }

        if (node.Configuration.TryGetProperty("timezone", out var tzProp)
            && tzProp.ValueKind == JsonValueKind.String)
        {
            timezone = tzProp.GetString();
        }

        return (string.IsNullOrWhiteSpace(cron) ? null : cron, interval, timezone);
    }
}
