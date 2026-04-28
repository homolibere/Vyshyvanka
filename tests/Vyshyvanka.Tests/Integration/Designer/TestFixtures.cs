using System.Text.Json;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Tests.Integration.Designer;

/// <summary>
/// Factory methods for creating common test data for Blazor Designer integration tests.
/// Provides pre-configured nodes, workflows, and node definitions for testing.
/// </summary>
public static class TestFixtures
{
    #region Trigger Nodes

    /// <summary>
    /// Creates a ManualTrigger node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    /// <param name="name">Optional node name. Defaults to "Manual Trigger".</param>
    public static WorkflowNode CreateTriggerNode(string? id = null, string? name = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "ManualTrigger",
        Name = name ?? "Manual Trigger",
        Position = new Position(100, 100),
        Configuration = JsonDocument.Parse("{}").RootElement
    };

    /// <summary>
    /// Creates a WebhookTrigger node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    /// <param name="webhookPath">Optional webhook path. Defaults to "/webhook/test".</param>
    public static WorkflowNode CreateWebhookTriggerNode(string? id = null, string? webhookPath = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "WebhookTrigger",
        Name = "Webhook Trigger",
        Position = new Position(100, 100),
        Configuration = JsonDocument.Parse($"{{\"path\":\"{webhookPath ?? "/webhook/test"}\"}}").RootElement
    };

    /// <summary>
    /// Creates a ScheduleTrigger node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    /// <param name="cronExpression">Optional cron expression. Defaults to "0 * * * *" (every hour).</param>
    public static WorkflowNode CreateScheduleTriggerNode(string? id = null, string? cronExpression = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "ScheduleTrigger",
        Name = "Schedule Trigger",
        Position = new Position(100, 100),
        Configuration = JsonDocument.Parse($"{{\"cron\":\"{cronExpression ?? "0 * * * *"}\"}}").RootElement
    };

    #endregion

    #region Action Nodes

    /// <summary>
    /// Creates an HttpRequest action node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    /// <param name="url">Optional URL. Defaults to "https://api.example.com".</param>
    public static WorkflowNode CreateHttpRequestNode(string? id = null, string? url = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "HttpRequest",
        Name = "HTTP Request",
        Position = new Position(300, 100),
        Configuration = JsonDocument.Parse($"{{\"url\":\"{url ?? "https://api.example.com"}\",\"method\":\"GET\"}}").RootElement
    };

    /// <summary>
    /// Creates an EmailSend action node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    public static WorkflowNode CreateEmailSendNode(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "EmailSend",
        Name = "Send Email",
        Position = new Position(300, 200),
        Configuration = JsonDocument.Parse("{\"to\":\"test@example.com\",\"subject\":\"Test\"}").RootElement
    };

    /// <summary>
    /// Creates a DatabaseQuery action node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    public static WorkflowNode CreateDatabaseQueryNode(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "DatabaseQuery",
        Name = "Database Query",
        Position = new Position(300, 300),
        Configuration = JsonDocument.Parse("{\"query\":\"SELECT * FROM users\"}").RootElement
    };

    #endregion

    #region Logic Nodes

    /// <summary>
    /// Creates an If logic node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    public static WorkflowNode CreateIfNode(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "If",
        Name = "If Condition",
        Position = new Position(500, 100),
        Configuration = JsonDocument.Parse("{\"condition\":\"{{$node.previous.data.value}} > 0\"}").RootElement
    };

    /// <summary>
    /// Creates a Switch logic node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    public static WorkflowNode CreateSwitchNode(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "Switch",
        Name = "Switch",
        Position = new Position(500, 200),
        Configuration = JsonDocument.Parse("{\"expression\":\"{{$node.previous.data.type}}\"}").RootElement
    };

    /// <summary>
    /// Creates a Loop logic node.
    /// </summary>
    /// <param name="id">Optional node ID. If null, generates a new GUID.</param>
    public static WorkflowNode CreateLoopNode(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Type = "Loop",
        Name = "Loop",
        Position = new Position(500, 300),
        Configuration = JsonDocument.Parse("{\"items\":\"{{$node.previous.data.items}}\"}").RootElement
    };

    #endregion

    #region Node Definitions

    /// <summary>
    /// Creates a ManualTrigger node definition.
    /// </summary>
    public static NodeDefinition CreateTriggerDefinition() => new()
    {
        Type = "ManualTrigger",
        Name = "Manual Trigger",
        Description = "Manually trigger workflow execution",
        Category = NodeCategory.Trigger,
        Icon = "play",
        Inputs = [],
        Outputs =
        [
            new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any, IsRequired = false }
        ]
    };

    /// <summary>
    /// Creates a WebhookTrigger node definition.
    /// </summary>
    public static NodeDefinition CreateWebhookTriggerDefinition() => new()
    {
        Type = "WebhookTrigger",
        Name = "Webhook Trigger",
        Description = "Trigger workflow via HTTP webhook",
        Category = NodeCategory.Trigger,
        Icon = "webhook",
        Inputs = [],
        Outputs =
        [
            new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Object, IsRequired = false }
        ]
    };

    /// <summary>
    /// Creates an HttpRequest node definition.
    /// </summary>
    public static NodeDefinition CreateHttpRequestDefinition() => new()
    {
        Type = "HttpRequest",
        Name = "HTTP Request",
        Description = "Make HTTP requests to external APIs",
        Category = NodeCategory.Action,
        Icon = "http",
        Inputs =
        [
            new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }
        ],
        Outputs =
        [
            new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Object, IsRequired = false }
        ]
    };

    /// <summary>
    /// Creates an If node definition.
    /// </summary>
    public static NodeDefinition CreateIfDefinition() => new()
    {
        Type = "If",
        Name = "If Condition",
        Description = "Branch workflow based on condition",
        Category = NodeCategory.Logic,
        Icon = "branch",
        Inputs =
        [
            new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }
        ],
        Outputs =
        [
            new PortDefinition { Name = "true", DisplayName = "True", Type = PortType.Any, IsRequired = false },
            new PortDefinition { Name = "false", DisplayName = "False", Type = PortType.Any, IsRequired = false }
        ]
    };

    /// <summary>
    /// Creates a set of common node definitions for testing.
    /// </summary>
    public static List<NodeDefinition> CreateCommonNodeDefinitions() =>
    [
        CreateTriggerDefinition(),
        CreateWebhookTriggerDefinition(),
        CreateHttpRequestDefinition(),
        CreateIfDefinition(),
        new NodeDefinition
        {
            Type = "EmailSend",
            Name = "Send Email",
            Description = "Send email messages",
            Category = NodeCategory.Action,
            Icon = "email",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Object, IsRequired = false }]
        },
        new NodeDefinition
        {
            Type = "DatabaseQuery",
            Name = "Database Query",
            Description = "Execute database queries",
            Category = NodeCategory.Action,
            Icon = "database",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Array, IsRequired = false }]
        },
        new NodeDefinition
        {
            Type = "Switch",
            Name = "Switch",
            Description = "Route to different outputs based on value",
            Category = NodeCategory.Logic,
            Icon = "switch",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
            Outputs =
            [
                new PortDefinition { Name = "case1", DisplayName = "Case 1", Type = PortType.Any, IsRequired = false },
                new PortDefinition { Name = "case2", DisplayName = "Case 2", Type = PortType.Any, IsRequired = false },
                new PortDefinition { Name = "default", DisplayName = "Default", Type = PortType.Any, IsRequired = false }
            ]
        },
        new NodeDefinition
        {
            Type = "Loop",
            Name = "Loop",
            Description = "Iterate over array items",
            Category = NodeCategory.Logic,
            Icon = "loop",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Array, IsRequired = true }],
            Outputs = [new PortDefinition { Name = "item", DisplayName = "Item", Type = PortType.Any, IsRequired = false }]
        },
        new NodeDefinition
        {
            Type = "Transform",
            Name = "Transform Data",
            Description = "Transform data using expressions",
            Category = NodeCategory.Transform,
            Icon = "transform",
            Inputs = [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any, IsRequired = true }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any, IsRequired = false }]
        }
    ];

    #endregion

    #region Connections

    /// <summary>
    /// Creates a connection between two nodes.
    /// </summary>
    /// <param name="sourceNodeId">Source node ID.</param>
    /// <param name="targetNodeId">Target node ID.</param>
    /// <param name="sourcePort">Source port name. Defaults to "output".</param>
    /// <param name="targetPort">Target port name. Defaults to "input".</param>
    public static Connection CreateConnection(
        string sourceNodeId,
        string targetNodeId,
        string sourcePort = "output",
        string targetPort = "input") => new()
    {
        SourceNodeId = sourceNodeId,
        SourcePort = sourcePort,
        TargetNodeId = targetNodeId,
        TargetPort = targetPort
    };

    #endregion

    #region Workflows

    /// <summary>
    /// Creates an empty workflow with no nodes.
    /// </summary>
    /// <param name="id">Optional workflow ID. If null, generates a new GUID.</param>
    /// <param name="name">Optional workflow name. Defaults to "Test Workflow".</param>
    public static Workflow CreateEmptyWorkflow(Guid? id = null, string? name = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name ?? "Test Workflow",
        Description = "A test workflow",
        Version = 1,
        IsActive = false,
        Nodes = [],
        Connections = [],
        Settings = new WorkflowSettings
        {
            Timeout = TimeSpan.FromMinutes(5),
            MaxRetries = 3,
            ErrorHandling = ErrorHandlingMode.StopOnFirstError
        },
        Tags = ["test"],
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        CreatedBy = Guid.NewGuid()
    };

    /// <summary>
    /// Creates a simple workflow with a trigger and one action node.
    /// </summary>
    /// <param name="id">Optional workflow ID. If null, generates a new GUID.</param>
    /// <param name="name">Optional workflow name. Defaults to "Simple Workflow".</param>
    public static Workflow CreateSimpleWorkflow(Guid? id = null, string? name = null)
    {
        var triggerId = Guid.NewGuid().ToString();
        var actionId = Guid.NewGuid().ToString();

        return new Workflow
        {
            Id = id ?? Guid.NewGuid(),
            Name = name ?? "Simple Workflow",
            Description = "A simple workflow with trigger and action",
            Version = 1,
            IsActive = true,
            Nodes =
            [
                CreateTriggerNode(triggerId),
                CreateHttpRequestNode(actionId)
            ],
            Connections =
            [
                CreateConnection(triggerId, actionId)
            ],
            Settings = new WorkflowSettings
            {
                Timeout = TimeSpan.FromMinutes(5),
                MaxRetries = 3,
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = ["test", "simple"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    /// <summary>
    /// Creates a workflow with branching logic (If node).
    /// </summary>
    /// <param name="id">Optional workflow ID. If null, generates a new GUID.</param>
    public static Workflow CreateBranchingWorkflow(Guid? id = null)
    {
        var triggerId = Guid.NewGuid().ToString();
        var ifId = Guid.NewGuid().ToString();
        var trueActionId = Guid.NewGuid().ToString();
        var falseActionId = Guid.NewGuid().ToString();

        return new Workflow
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Branching Workflow",
            Description = "A workflow with conditional branching",
            Version = 1,
            IsActive = true,
            Nodes =
            [
                CreateTriggerNode(triggerId),
                CreateIfNode(ifId),
                CreateHttpRequestNode(trueActionId) with { Name = "True Branch Action", Position = new Position(700, 50) },
                CreateEmailSendNode(falseActionId) with { Position = new Position(700, 200) }
            ],
            Connections =
            [
                CreateConnection(triggerId, ifId),
                CreateConnection(ifId, trueActionId, "true", "input"),
                CreateConnection(ifId, falseActionId, "false", "input")
            ],
            Settings = new WorkflowSettings
            {
                Timeout = TimeSpan.FromMinutes(10),
                MaxRetries = 2,
                ErrorHandling = ErrorHandlingMode.ContinueOnError
            },
            Tags = ["test", "branching"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    /// <summary>
    /// Creates a workflow with duplicate node IDs (invalid for testing validation).
    /// </summary>
    public static Workflow CreateWorkflowWithDuplicateIds()
    {
        var duplicateId = "duplicate-id";

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Invalid Workflow - Duplicate IDs",
            Description = "A workflow with duplicate node IDs for validation testing",
            Version = 1,
            IsActive = false,
            Nodes =
            [
                CreateTriggerNode(duplicateId),
                CreateHttpRequestNode(duplicateId)
            ],
            Connections = [],
            Settings = new WorkflowSettings(),
            Tags = ["test", "invalid"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    /// <summary>
    /// Creates a workflow with no trigger node (invalid for testing validation).
    /// </summary>
    public static Workflow CreateWorkflowWithoutTrigger()
    {
        var actionId = Guid.NewGuid().ToString();

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Invalid Workflow - No Trigger",
            Description = "A workflow without a trigger node for validation testing",
            Version = 1,
            IsActive = false,
            Nodes =
            [
                CreateHttpRequestNode(actionId)
            ],
            Connections = [],
            Settings = new WorkflowSettings(),
            Tags = ["test", "invalid"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    /// <summary>
    /// Creates a workflow with a connection to a non-existent node (invalid for testing validation).
    /// </summary>
    public static Workflow CreateWorkflowWithInvalidConnection()
    {
        var triggerId = Guid.NewGuid().ToString();

        return new Workflow
        {
            Id = Guid.NewGuid(),
            Name = "Invalid Workflow - Bad Connection",
            Description = "A workflow with a connection to a non-existent node",
            Version = 1,
            IsActive = false,
            Nodes =
            [
                CreateTriggerNode(triggerId)
            ],
            Connections =
            [
                CreateConnection(triggerId, "non-existent-node-id")
            ],
            Settings = new WorkflowSettings(),
            Tags = ["test", "invalid"],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    #endregion
}
