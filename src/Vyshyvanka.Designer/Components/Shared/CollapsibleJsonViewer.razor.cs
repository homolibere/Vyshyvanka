using System.Text.Json;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Renders a JSON element as a collapsible tree with syntax highlighting.
/// Objects and arrays can be expanded/collapsed individually.
/// </summary>
public partial class CollapsibleJsonViewer : ComponentBase
{
    private readonly HashSet<string> _collapsedPaths = [];

    /// <summary>
    /// The JSON data to display.
    /// </summary>
    [Parameter]
    public JsonElement? Data { get; set; }

    /// <summary>
    /// Maximum depth to auto-expand on initial render. Nodes deeper than this start collapsed.
    /// Set to -1 to expand all. Default is 3.
    /// </summary>
    [Parameter]
    public int AutoExpandDepth { get; set; } = 3;

    private JsonElement? _previousData;

    protected override void OnParametersSet()
    {
        // Reset collapsed state when data changes
        if (!JsonElementEquals(_previousData, Data))
        {
            _collapsedPaths.Clear();
            if (Data.HasValue)
            {
                InitializeCollapsedState(Data.Value, "", 0);
            }
            _previousData = Data;
        }
    }

    private void InitializeCollapsedState(JsonElement element, string path, int depth)
    {
        if (AutoExpandDepth >= 0 && depth > AutoExpandDepth)
        {
            if (element.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                _collapsedPaths.Add(path);
            }
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var childPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    InitializeCollapsedState(prop.Value, childPath, depth + 1);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var childPath = $"{path}[{index}]";
                    InitializeCollapsedState(item, childPath, depth + 1);
                    index++;
                }
                break;
        }
    }

    private bool IsCollapsed(string path) => _collapsedPaths.Contains(path);

    private void Toggle(string path)
    {
        if (!_collapsedPaths.Remove(path))
        {
            _collapsedPaths.Add(path);
        }
    }

    private void ExpandAll()
    {
        _collapsedPaths.Clear();
    }

    private void CollapseAll()
    {
        if (Data.HasValue)
        {
            CollapseAllRecursive(Data.Value, "");
        }
    }

    private void CollapseAllRecursive(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject().ToList();
                if (properties.Count > 0)
                {
                    _collapsedPaths.Add(path);
                }
                foreach (var prop in properties)
                {
                    var childPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                    CollapseAllRecursive(prop.Value, childPath);
                }
                break;
            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToList();
                if (items.Count > 0)
                {
                    _collapsedPaths.Add(path);
                }
                for (var i = 0; i < items.Count; i++)
                {
                    var childPath = $"{path}[{i}]";
                    CollapseAllRecursive(items[i], childPath);
                }
                break;
        }
    }

    private static bool JsonElementEquals(JsonElement? a, JsonElement? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (!a.HasValue || !b.HasValue) return false;

        try
        {
            return a.Value.GetRawText() == b.Value.GetRawText();
        }
        catch
        {
            return false;
        }
    }
}
