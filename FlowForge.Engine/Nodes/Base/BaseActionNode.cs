using FlowForge.Core.Enums;

namespace FlowForge.Engine.Nodes.Base;

/// <summary>
/// Abstract base class for action nodes that perform operations
/// like HTTP requests, database queries, or service integrations.
/// </summary>
public abstract class BaseActionNode : BaseNode
{
    /// <inheritdoc />
    public override NodeCategory Category => NodeCategory.Action;
}
