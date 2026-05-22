using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Designer.Models;
using Vyshyvanka.Designer.Services;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

public partial class NodePalette : IDisposable
{
    [Inject]
    private WorkflowStore Store { get; set; } = null!;

    [Inject]
    private CanvasStateService CanvasState { get; set; } = null!;

    private string searchText = string.Empty;
    private HashSet<NodeCategory> expandedCategories = [NodeCategory.Trigger, NodeCategory.Logic];

    protected override void OnInitialized()
    {
        Store.OnStateChanged += StateHasChanged;
    }

    public void Dispose()
    {
        Store.OnStateChanged -= StateHasChanged;
    }

    private IEnumerable<IGrouping<NodeCategory, NodeDefinition>> GetGroupedNodes()
    {
        var nodes = Store.NodeDefinitions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            nodes = nodes.Where(n =>
                n.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                n.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(n.SourcePackage) && n.SourcePackage.Contains(searchText, StringComparison.OrdinalIgnoreCase)));
        }

        return nodes.GroupBy(n => n.Category).OrderBy(g => g.Key);
    }

    private void ToggleCategory(NodeCategory category)
    {
        if (expandedCategories.Contains(category))
            expandedCategories.Remove(category);
        else
            expandedCategories.Add(category);
    }

    private static string GetCategoryIcon(NodeCategory category) => category switch
    {
        NodeCategory.Trigger => "⚡",
        NodeCategory.Action => "⚙",
        NodeCategory.Logic => "⑂",
        NodeCategory.Transform => "🔄",
        _ => "📦"
    };

    private static string GetNodeTooltip(NodeDefinition node)
    {
        var tooltip = node.Description;
        if (node.IsPluginNode)
        {
            tooltip = $"[Plugin: {node.SourcePackage}] {tooltip}";
        }

        return tooltip;
    }

    private void OnDragStart(NodeDefinition node)
    {
        CanvasState.StartDragFromPalette(node.Type);
    }

    private void OnDragEnd()
    {
        CanvasState.EndDragFromPalette();
    }
}
