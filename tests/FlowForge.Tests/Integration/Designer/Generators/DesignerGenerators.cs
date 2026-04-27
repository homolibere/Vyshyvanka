using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;

namespace FlowForge.Tests.Integration.Designer.Generators;

/// <summary>
/// CsCheck generators for Blazor Designer integration tests.
/// Provides generators for WorkflowNode, Workflow, Connection, NodeDefinition, and related types.
/// </summary>
public static class DesignerGenerators
{
    #region String Generators

    /// <summary>Generator for non-empty alphanumeric strings.</summary>
    public static Gen<string> NonEmptyString(int minLength = 1, int maxLength = 20) =>
        Gen.Char['a', 'z'].Array[minLength, maxLength].Select(chars => new string(chars));

    /// <summary>Generator for optional strings.</summary>
    public static Gen<string?> OptionalString(int maxLength = 50) =>
        Gen.Bool.SelectMany(hasValue =>
            hasValue
                ? NonEmptyString(1, maxLength).Select(s => (string?)s)
                : Gen.Const((string?)null));

    #endregion

    #region Enum Generators

    /// <summary>Generator for PortType enum values.</summary>
    public static readonly Gen<PortType> PortTypeGen =
        Gen.OneOf(
            Gen.Const(PortType.Any),
            Gen.Const(PortType.Object),
            Gen.Const(PortType.Array),
            Gen.Const(PortType.String),
            Gen.Const(PortType.Number),
            Gen.Const(PortType.Boolean)
        );

    /// <summary>Generator for NodeCategory enum values.</summary>
    public static readonly Gen<NodeCategory> NodeCategoryGen =
        Gen.OneOf(
            Gen.Const(NodeCategory.Trigger),
            Gen.Const(NodeCategory.Action),
            Gen.Const(NodeCategory.Logic),
            Gen.Const(NodeCategory.Transform)
        );

    /// <summary>Generator for CredentialType enum values.</summary>
    public static readonly Gen<CredentialType> CredentialTypeGen =
        Gen.OneOf(
            Gen.Const(CredentialType.ApiKey),
            Gen.Const(CredentialType.OAuth2),
            Gen.Const(CredentialType.BasicAuth),
            Gen.Const(CredentialType.CustomHeaders)
        );

    /// <summary>Generator for ErrorHandlingMode enum values.</summary>
    public static readonly Gen<ErrorHandlingMode> ErrorHandlingModeGen =
        Gen.OneOf(
            Gen.Const(ErrorHandlingMode.StopOnFirstError),
            Gen.Const(ErrorHandlingMode.ContinueOnError),
            Gen.Const(ErrorHandlingMode.RetryWithBackoff)
        );

    #endregion

    #region Toast Generators

    /// <summary>Generator for ToastType enum values.</summary>
    public static readonly Gen<FlowForge.Designer.Models.ToastType> ToastTypeGen =
        Gen.OneOf(
            Gen.Const(FlowForge.Designer.Models.ToastType.Success),
            Gen.Const(FlowForge.Designer.Models.ToastType.Error),
            Gen.Const(FlowForge.Designer.Models.ToastType.Warning),
            Gen.Const(FlowForge.Designer.Models.ToastType.Info)
        );

    /// <summary>Generator for toast messages (non-empty strings).</summary>
    public static readonly Gen<string> ToastMessageGen = NonEmptyString(1, 100);

    /// <summary>Generator for optional toast titles.</summary>
    public static readonly Gen<string?> ToastTitleGen = OptionalString(50);

    /// <summary>Generator for toast dismiss timeout (positive integers).</summary>
    public static readonly Gen<int> ToastTimeoutGen = Gen.Int[1000, 30000];

    #endregion

    #region Position Generator

    /// <summary>Generator for Position with valid canvas coordinates.</summary>
    public static readonly Gen<Position> PositionGen =
        from x in Gen.Double[0, 2000]
        from y in Gen.Double[0, 2000]
        select new Position(x, y);

    #endregion

    #region Configuration Generator

    /// <summary>Generator for simple JSON configuration elements.</summary>
    public static readonly Gen<JsonElement> ConfigurationGen =
        Gen.Int[0, 4].Select(i => i switch
        {
            0 => JsonDocument.Parse("{}").RootElement,
            1 => JsonDocument.Parse("{\"enabled\":true}").RootElement,
            2 => JsonDocument.Parse("{\"value\":42}").RootElement,
            3 => JsonDocument.Parse("{\"name\":\"test\",\"count\":5}").RootElement,
            _ => JsonDocument.Parse("{\"url\":\"https://example.com\",\"timeout\":30}").RootElement
        });

    #endregion

    #region Port Definition Generator

    /// <summary>Generator for PortDefinition.</summary>
    public static readonly Gen<PortDefinition> PortDefinitionGen =
        from name in NonEmptyString(1, 20)
        from displayName in NonEmptyString(1, 30)
        from portType in PortTypeGen
        from isRequired in Gen.Bool
        select new PortDefinition
        {
            Name = name,
            DisplayName = displayName,
            Type = portType,
            IsRequired = isRequired
        };

    #endregion

    #region Node Definition Generator

    /// <summary>Generator for NodeDefinition.</summary>
    public static readonly Gen<NodeDefinition> NodeDefinitionGen =
        from type in NonEmptyString(1, 30)
        from name in NonEmptyString(1, 50)
        from description in NonEmptyString(1, 100)
        from category in NodeCategoryGen
        from icon in NonEmptyString(1, 20)
        from inputs in PortDefinitionGen.List[0, 3]
        from outputs in PortDefinitionGen.List[1, 3]
        from hasCredential in Gen.Bool
        from credentialType in CredentialTypeGen
        from hasSourcePackage in Gen.Bool
        from sourcePackage in NonEmptyString(1, 30)
        select new NodeDefinition
        {
            Type = type,
            Name = name,
            Description = description,
            Category = category,
            Icon = icon,
            Inputs = inputs,
            Outputs = outputs,
            ConfigurationSchema = null,
            RequiredCredentialType = hasCredential ? credentialType : null,
            SourcePackage = hasSourcePackage ? sourcePackage : null
        };

    /// <summary>Generator for trigger NodeDefinition (no inputs).</summary>
    public static readonly Gen<NodeDefinition> TriggerNodeDefinitionGen =
        from type in NonEmptyString(1, 30)
        from name in NonEmptyString(1, 50)
        from description in NonEmptyString(1, 100)
        from icon in NonEmptyString(1, 20)
        from outputs in PortDefinitionGen.List[1, 3]
        select new NodeDefinition
        {
            Type = type,
            Name = name,
            Description = description,
            Category = NodeCategory.Trigger,
            Icon = icon,
            Inputs = [],
            Outputs = outputs,
            ConfigurationSchema = null,
            RequiredCredentialType = null,
            SourcePackage = null
        };

    #endregion

    #region Node Generator

    /// <summary>Generator for WorkflowNode.</summary>
    public static readonly Gen<WorkflowNode> NodeGen =
        from id in Gen.Guid.Select(g => g.ToString())
        from type in NonEmptyString(1, 30)
        from name in NonEmptyString(1, 50)
        from position in PositionGen
        from config in ConfigurationGen
        from hasCredential in Gen.Bool
        from credentialId in Gen.Guid
        select new WorkflowNode
        {
            Id = id,
            Type = type,
            Name = name,
            Position = position,
            Configuration = config,
            CredentialId = hasCredential ? credentialId : null
        };

    /// <summary>Generator for trigger WorkflowNode.</summary>
    public static readonly Gen<WorkflowNode> TriggerNodeGen =
        from id in Gen.Guid.Select(g => g.ToString())
        from name in NonEmptyString(1, 50)
        from position in PositionGen
        from config in ConfigurationGen
        select new WorkflowNode
        {
            Id = id,
            Type = "ManualTrigger",
            Name = name,
            Position = position,
            Configuration = config,
            CredentialId = null
        };

    /// <summary>Generator for action WorkflowNode.</summary>
    public static readonly Gen<WorkflowNode> ActionNodeGen =
        from id in Gen.Guid.Select(g => g.ToString())
        from name in NonEmptyString(1, 50)
        from position in PositionGen
        from config in ConfigurationGen
        from hasCredential in Gen.Bool
        from credentialId in Gen.Guid
        select new WorkflowNode
        {
            Id = id,
            Type = "HttpRequest",
            Name = name,
            Position = position,
            Configuration = config,
            CredentialId = hasCredential ? credentialId : null
        };

    #endregion

    #region Connection Generator

    /// <summary>Generator for Connection.</summary>
    public static readonly Gen<Connection> ConnectionGen =
        from sourceNodeId in Gen.Guid.Select(g => g.ToString())
        from sourcePort in NonEmptyString(1, 20)
        from targetNodeId in Gen.Guid.Select(g => g.ToString())
        from targetPort in NonEmptyString(1, 20)
        select new Connection
        {
            SourceNodeId = sourceNodeId,
            SourcePort = sourcePort,
            TargetNodeId = targetNodeId,
            TargetPort = targetPort
        };

    /// <summary>
    /// Creates a connection generator that uses node IDs from the provided nodes.
    /// Ensures connections reference valid nodes.
    /// </summary>
    public static Gen<Connection> ConnectionGenForNodes(List<WorkflowNode> nodes)
    {
        if (nodes.Count < 2)
            return Gen.Const(new Connection());

        return from sourceIndex in Gen.Int[0, nodes.Count - 1]
               from targetIndex in Gen.Int[0, nodes.Count - 1].Where(i => i != sourceIndex)
               select new Connection
               {
                   SourceNodeId = nodes[sourceIndex].Id,
                   SourcePort = "output",
                   TargetNodeId = nodes[targetIndex].Id,
                   TargetPort = "input"
               };
    }

    #endregion

    #region Workflow Generator

    /// <summary>Generator for DateTime (UTC, truncated to milliseconds for JSON compatibility).</summary>
    public static readonly Gen<DateTime> DateTimeGen =
        from year in Gen.Int[2020, 2030]
        from month in Gen.Int[1, 12]
        from day in Gen.Int[1, 28]
        from hour in Gen.Int[0, 23]
        from minute in Gen.Int[0, 59]
        from second in Gen.Int[0, 59]
        from millisecond in Gen.Int[0, 999]
        select new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);

    /// <summary>Generator for WorkflowSettings.</summary>
    public static readonly Gen<WorkflowSettings> WorkflowSettingsGen =
        from hasTimeout in Gen.Bool
        from timeoutSeconds in Gen.Int[1, 3600]
        from maxRetries in Gen.Int[0, 10]
        from errorHandling in ErrorHandlingModeGen
        select new WorkflowSettings
        {
            Timeout = hasTimeout ? TimeSpan.FromSeconds(timeoutSeconds) : null,
            MaxRetries = maxRetries,
            ErrorHandling = errorHandling
        };

    /// <summary>Generator for tags list.</summary>
    public static readonly Gen<List<string>> TagsGen =
        NonEmptyString(1, 20).List[0, 5];

    /// <summary>Generator for Workflow (may not have trigger node).</summary>
    public static readonly Gen<Workflow> WorkflowGen =
        from id in Gen.Guid
        from name in NonEmptyString(1, 50)
        from description in OptionalString(200)
        from version in Gen.Int[0, 1000]
        from isActive in Gen.Bool
        from nodes in NodeGen.List[0, 10]
        from connections in ConnectionGen.List[0, 10]
        from settings in WorkflowSettingsGen
        from tags in TagsGen
        from createdAt in DateTimeGen
        from updatedAt in DateTimeGen
        from createdBy in Gen.Guid
        select new Workflow
        {
            Id = id,
            Name = name,
            Description = description,
            Version = version,
            IsActive = isActive,
            Nodes = nodes,
            Connections = connections,
            Settings = settings,
            Tags = tags,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CreatedBy = createdBy
        };

    /// <summary>
    /// Generator for valid Workflow with at least one trigger node and valid connections.
    /// </summary>
    public static readonly Gen<Workflow> ValidWorkflowGen =
        from id in Gen.Guid
        from name in NonEmptyString(1, 50)
        from description in OptionalString(200)
        from version in Gen.Int[0, 1000]
        from isActive in Gen.Bool
        from triggerNode in TriggerNodeGen
        from actionNodes in ActionNodeGen.List[0, 5]
        from settings in WorkflowSettingsGen
        from tags in TagsGen
        from createdAt in DateTimeGen
        from updatedAt in DateTimeGen
        from createdBy in Gen.Guid
        select CreateValidWorkflow(
            id, name, description, version, isActive,
            triggerNode, actionNodes, settings, tags,
            createdAt, updatedAt, createdBy);

    private static Workflow CreateValidWorkflow(
        Guid id, string name, string? description, int version, bool isActive,
        WorkflowNode triggerNode, List<WorkflowNode> actionNodes,
        WorkflowSettings settings, List<string> tags,
        DateTime createdAt, DateTime updatedAt, Guid createdBy)
    {
        var allNodes = new List<WorkflowNode> { triggerNode };
        allNodes.AddRange(actionNodes);

        // Create valid connections from trigger to first action, then chain actions
        var connections = new List<Connection>();
        for (int i = 0; i < allNodes.Count - 1; i++)
        {
            connections.Add(new Connection
            {
                SourceNodeId = allNodes[i].Id,
                SourcePort = "output",
                TargetNodeId = allNodes[i + 1].Id,
                TargetPort = "input"
            });
        }

        return new Workflow
        {
            Id = id,
            Name = name,
            Description = description,
            Version = version,
            IsActive = isActive,
            Nodes = allNodes,
            Connections = connections,
            Settings = settings,
            Tags = tags,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CreatedBy = createdBy
        };
    }

    #endregion
}
