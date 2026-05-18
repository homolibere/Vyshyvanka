using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;

namespace Vyshyvanka.Designer.Services;

/// <summary>
/// Provides autocomplete suggestions for expression syntax ({{ }}) in node configuration fields.
/// Suggests node references, variables, input paths, and built-in functions based on the
/// current workflow context and cursor position within the expression.
/// </summary>
public class ExpressionAutocompleteService
{
    private static readonly string[] BuiltInFunctions =
    [
        "toUpper", "toLower", "trim", "substring", "length", "concat", "replace",
        "contains", "startsWith", "endsWith", "split",
        "format", "parseDate", "addDays", "addHours", "addMinutes", "now", "utcNow",
        "round", "floor", "ceil", "abs", "min", "max",
        "toString", "toNumber", "toBoolean",
        "coalesce", "ifNull", "iif"
    ];

    private readonly WorkflowStateService _workflowState;

    public ExpressionAutocompleteService(WorkflowStateService workflowState)
    {
        _workflowState = workflowState;
    }

    /// <summary>
    /// Gets autocomplete suggestions for the given expression fragment.
    /// </summary>
    /// <param name="fragment">The text typed so far inside {{ }}.</param>
    /// <param name="currentNodeId">The ID of the node being edited (excluded from suggestions).</param>
    /// <returns>A list of suggestions ordered by relevance.</returns>
    public List<ExpressionSuggestion> GetSuggestions(string fragment, string? currentNodeId)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return GetRootSuggestions();
        }

        var parts = fragment.Split('.');
        var root = parts[0].ToLowerInvariant();

        return root switch
        {
            "nodes" => GetNodeSuggestions(parts, currentNodeId),
            "variables" => GetVariableSuggestions(parts),
            "input" => GetInputSuggestions(parts, currentNodeId),
            _ => GetFilteredRootSuggestions(fragment)
        };
    }

    private List<ExpressionSuggestion> GetRootSuggestions()
    {
        var suggestions = new List<ExpressionSuggestion>
        {
            new()
            {
                Label = "nodes",
                InsertText = "nodes.",
                Description = "Reference output from another node",
                Kind = SuggestionKind.Keyword,
                SortOrder = 0
            },
            new()
            {
                Label = "input",
                InsertText = "input.",
                Description = "Reference the current node's input data",
                Kind = SuggestionKind.Keyword,
                SortOrder = 1
            },
            new()
            {
                Label = "variables",
                InsertText = "variables.",
                Description = "Reference execution variables",
                Kind = SuggestionKind.Keyword,
                SortOrder = 2
            }
        };

        // Add functions
        foreach (var funcName in BuiltInFunctions.OrderBy(f => f))
        {
            suggestions.Add(new ExpressionSuggestion
            {
                Label = $"{funcName}()",
                InsertText = $"{funcName}(",
                Description = GetFunctionDescription(funcName),
                Kind = SuggestionKind.Function,
                SortOrder = 10
            });
        }

        return suggestions;
    }

    private List<ExpressionSuggestion> GetFilteredRootSuggestions(string fragment)
    {
        return GetRootSuggestions()
            .Where(s => s.Label.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<ExpressionSuggestion> GetNodeSuggestions(string[] parts, string? currentNodeId)
    {
        // "nodes" or "nodes." — show available node names
        if (parts.Length <= 1 || (parts.Length == 2 && !parts[1].EndsWith('.')))
        {
            var filter = parts.Length > 1 ? parts[1] : "";
            return GetAvailableNodes(currentNodeId)
                .Where(n => string.IsNullOrEmpty(filter) ||
                            n.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            n.Id.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(n => new ExpressionSuggestion
                {
                    Label = n.Name,
                    InsertText = $"nodes.{n.Id}.",
                    Description = $"Output from {n.Name} ({n.Type})",
                    Detail = n.Id,
                    Kind = SuggestionKind.Node,
                    SortOrder = 0
                })
                .ToList();
        }

        // "nodes.nodeId.field..." — navigate into output data
        if (parts.Length >= 3)
        {
            var nodeId = parts[1];
            var node = _workflowState.GetNode(nodeId);
            if (node is null) return [];

            var definition = _workflowState.GetNodeDefinition(node.Type);
            var suggestions = new List<ExpressionSuggestion>();

            // Get the node's output data for deep navigation
            var executionState = _workflowState.GetNodeExecutionState(nodeId);
            var outputData = executionState?.OutputData;

            // For deep paths (nodes.id.field.subfield...), navigate into the JSON
            if (parts.Length > 3 && outputData is { } data &&
                data.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                return NavigateJsonForSuggestions(data, parts[2..], $"nodes.{nodeId}");
            }

            // For "nodes.nodeId." (exactly 3 parts) — show top-level fields
            var filter = parts[^1];

            // Add output port names from definition
            if (definition is not null)
            {
                foreach (var output in definition.Outputs)
                {
                    if (string.IsNullOrEmpty(filter) ||
                        output.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    {
                        suggestions.Add(new ExpressionSuggestion
                        {
                            Label = output.DisplayName,
                            InsertText = $"nodes.{nodeId}.{output.Name}",
                            Description = $"Port: {output.Type}",
                            Detail = output.Name,
                            Kind = SuggestionKind.Property,
                            SortOrder = 0
                        });
                    }
                }
            }

            // Add fields from actual output data
            if (outputData is { } od && od.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in od.EnumerateObject())
                {
                    if (suggestions.All(s => s.Detail != prop.Name) &&
                        (string.IsNullOrEmpty(filter) ||
                         prop.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    {
                        var isNested = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object ||
                                       prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array;
                        suggestions.Add(new ExpressionSuggestion
                        {
                            Label = prop.Name,
                            InsertText = isNested
                                ? $"nodes.{nodeId}.{prop.Name}."
                                : $"nodes.{nodeId}.{prop.Name}",
                            Description = $"Value: {GetJsonPreview(prop.Value)}",
                            Detail = prop.Name,
                            Kind = SuggestionKind.Property,
                            SortOrder = 5
                        });
                    }
                }
            }

            return suggestions;
        }

        return [];
    }

    /// <summary>
    /// Navigates a JSON element along a dot-separated path and suggests properties at the final level.
    /// </summary>
    private static List<ExpressionSuggestion> NavigateJsonForSuggestions(
        System.Text.Json.JsonElement root,
        string[] pathParts,
        string pathPrefix)
    {
        var current = root;
        var builtPath = pathPrefix;

        // Navigate through all parts except the last (which is the filter)
        for (var i = 0; i < pathParts.Length - 1; i++)
        {
            var segment = pathParts[i];
            if (string.IsNullOrEmpty(segment)) break;

            builtPath += $".{segment}";

            if (current.ValueKind == System.Text.Json.JsonValueKind.Object &&
                current.TryGetProperty(segment, out var child))
            {
                current = child;
            }
            else if (current.ValueKind == System.Text.Json.JsonValueKind.Array &&
                     int.TryParse(segment, out var index) &&
                     index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                return [];
            }
        }

        var filter = pathParts[^1];
        var suggestions = new List<ExpressionSuggestion>();

        if (current.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in current.EnumerateObject())
            {
                if (string.IsNullOrEmpty(filter) ||
                    prop.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    var isNested = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object ||
                                   prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array;
                    suggestions.Add(new ExpressionSuggestion
                    {
                        Label = prop.Name,
                        InsertText = isNested ? $"{builtPath}.{prop.Name}." : $"{builtPath}.{prop.Name}",
                        Description = $"Value: {GetJsonPreview(prop.Value)}",
                        Kind = SuggestionKind.Property,
                        SortOrder = 0
                    });
                }
            }
        }
        else if (current.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var length = current.GetArrayLength();
            for (var i = 0; i < Math.Min(length, 10); i++)
            {
                var indexStr = i.ToString();
                if (string.IsNullOrEmpty(filter) ||
                    indexStr.StartsWith(filter, StringComparison.Ordinal))
                {
                    suggestions.Add(new ExpressionSuggestion
                    {
                        Label = $"[{i}]",
                        InsertText = $"{builtPath}.{i}.",
                        Description = $"Item {i}: {GetJsonPreview(current[i])}",
                        Kind = SuggestionKind.Property,
                        SortOrder = i
                    });
                }
            }
        }

        return suggestions;
    }

    private List<ExpressionSuggestion> GetVariableSuggestions(string[] parts)
    {
        // Suggest common variable names
        var filter = parts.Length > 1 ? parts[1] : "";
        var suggestions = new List<ExpressionSuggestion>
        {
            new()
            {
                Label = "executionId",
                InsertText = "variables.executionId",
                Description = "Current execution identifier",
                Kind = SuggestionKind.Variable,
                SortOrder = 0
            },
            new()
            {
                Label = "workflowId",
                InsertText = "variables.workflowId",
                Description = "Current workflow identifier",
                Kind = SuggestionKind.Variable,
                SortOrder = 1
            }
        };

        if (!string.IsNullOrEmpty(filter))
        {
            suggestions = suggestions
                .Where(s => s.Label.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return suggestions;
    }

    private List<ExpressionSuggestion> GetInputSuggestions(string[] parts, string? currentNodeId)
    {
        var suggestions = new List<ExpressionSuggestion>();

        // Get the input data JSON element
        var inputData = GetCurrentNodeInputData(currentNodeId);
        if (inputData is null)
        {
            // Fall back to upstream inference for top-level only
            if (parts.Length <= 2)
            {
                var upstreamFields = GetUpstreamOutputFields(currentNodeId);
                if (upstreamFields.Count > 0)
                {
                    var filter = parts.Length > 1 ? parts[^1] : "";
                    foreach (var field in upstreamFields)
                    {
                        if (string.IsNullOrEmpty(filter) ||
                            field.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add(new ExpressionSuggestion
                            {
                                Label = field.Name,
                                InsertText = $"input.{field.Name}",
                                Description = field.Description,
                                Detail = field.Source,
                                Kind = SuggestionKind.Property,
                                SortOrder = field.SortOrder
                            });
                        }
                    }

                    return suggestions;
                }
            }

            if (parts.Length <= 2 && string.IsNullOrEmpty(parts.Length > 1 ? parts[^1] : ""))
            {
                suggestions.Add(new ExpressionSuggestion
                {
                    Label = "(no input data available)",
                    InsertText = "input",
                    Description = "Connect upstream nodes or run the workflow to see input fields",
                    Kind = SuggestionKind.Keyword,
                    SortOrder = 0
                });
            }

            return suggestions;
        }

        // Navigate into nested path: input.item.subfield → navigate to "item" then list its properties
        var current = inputData.Value;
        var pathPrefix = "input";

        // Navigate through intermediate path segments (skip "input" at index 0, and the last segment which is the filter)
        for (var i = 1; i < parts.Length - 1; i++)
        {
            var segment = parts[i];
            if (string.IsNullOrEmpty(segment)) break;

            pathPrefix += $".{segment}";

            if (current.ValueKind == System.Text.Json.JsonValueKind.Object &&
                current.TryGetProperty(segment, out var child))
            {
                current = child;
            }
            else if (current.ValueKind == System.Text.Json.JsonValueKind.Array &&
                     int.TryParse(segment, out var index) &&
                     index >= 0 && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                // Path doesn't exist — no suggestions
                return suggestions;
            }
        }

        // Now suggest properties of the current element
        var lastFilter = parts[^1];

        if (current.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in current.EnumerateObject())
            {
                if (string.IsNullOrEmpty(lastFilter) ||
                    prop.Name.Contains(lastFilter, StringComparison.OrdinalIgnoreCase))
                {
                    var isNested = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object ||
                                   prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array;
                    suggestions.Add(new ExpressionSuggestion
                    {
                        Label = prop.Name,
                        InsertText = isNested ? $"{pathPrefix}.{prop.Name}." : $"{pathPrefix}.{prop.Name}",
                        Description = $"Value: {GetJsonPreview(prop.Value)}",
                        Kind = SuggestionKind.Property,
                        SortOrder = 0
                    });
                }
            }
        }
        else if (current.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var length = current.GetArrayLength();
            for (var i = 0; i < Math.Min(length, 10); i++)
            {
                var indexStr = i.ToString();
                if (string.IsNullOrEmpty(lastFilter) ||
                    indexStr.StartsWith(lastFilter, StringComparison.Ordinal))
                {
                    suggestions.Add(new ExpressionSuggestion
                    {
                        Label = $"[{i}]",
                        InsertText = $"{pathPrefix}.{i}.",
                        Description = $"Item {i}: {GetJsonPreview(current[i])}",
                        Kind = SuggestionKind.Property,
                        SortOrder = i
                    });
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Gets the current node's input data as a JsonElement from execution state.
    /// </summary>
    private System.Text.Json.JsonElement? GetCurrentNodeInputData(string? currentNodeId)
    {
        if (string.IsNullOrEmpty(currentNodeId)) return null;

        var executionState = _workflowState.GetNodeExecutionState(currentNodeId);
        if (executionState is null) return null;

        // Try top-level InputData first
        var inputData = executionState.InputData;

        // If top-level is empty but iterations exist, use the last iteration's InputData
        if (inputData is null || inputData.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            if (executionState.Iterations.Count > 0)
            {
                var lastIteration = executionState.Iterations[^1];
                inputData = lastIteration.InputData;
            }
        }

        if (inputData is null || inputData.Value.ValueKind == System.Text.Json.JsonValueKind.Undefined)
        {
            return null;
        }

        return inputData;
    }

    /// <summary>
    /// Collects available input fields by inspecting upstream nodes connected to the current node.
    /// Uses execution output data when available, falls back to port definitions.
    /// </summary>
    private List<InputFieldInfo> GetUpstreamOutputFields(string? currentNodeId)
    {
        if (string.IsNullOrEmpty(currentNodeId)) return [];

        var fields = new List<InputFieldInfo>();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find all connections targeting the current node
        var incomingConnections = _workflowState.Workflow.Connections
            .Where(c => c.TargetNodeId == currentNodeId)
            .ToList();

        foreach (var connection in incomingConnections)
        {
            var sourceNode = _workflowState.GetNode(connection.SourceNodeId);
            if (sourceNode is null) continue;

            var sourceName = sourceNode.Name;

            // Try to get actual output data from execution state
            var executionState = _workflowState.GetNodeExecutionState(connection.SourceNodeId);
            if (executionState?.OutputData is { } outputData &&
                outputData.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in outputData.EnumerateObject())
                {
                    if (seenFields.Add(prop.Name))
                    {
                        fields.Add(new InputFieldInfo
                        {
                            Name = prop.Name,
                            Description = $"Value: {GetJsonPreview(prop.Value)}",
                            Source = sourceName,
                            SortOrder = 0
                        });
                    }
                }
            }
            else
            {
                // Fall back to output port definitions from the source node's schema
                var definition = _workflowState.GetNodeDefinition(sourceNode.Type);
                if (definition is not null)
                {
                    foreach (var output in definition.Outputs)
                    {
                        if (seenFields.Add(output.Name))
                        {
                            fields.Add(new InputFieldInfo
                            {
                                Name = output.Name,
                                Description = $"From {sourceName} ({output.Type})",
                                Source = sourceName,
                                SortOrder = 5
                            });
                        }
                    }
                }
            }
        }

        return fields;
    }

    private record InputFieldInfo
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? Source { get; init; }
        public int SortOrder { get; init; }
    }

    private IEnumerable<WorkflowNode> GetAvailableNodes(string? currentNodeId)
    {
        return _workflowState.Workflow.Nodes
            .Where(n => n.Id != currentNodeId)
            .OrderBy(n => n.Name);
    }

    private static string GetFunctionDescription(string funcName)
    {
        return funcName switch
        {
            "toUpper" => "Convert string to uppercase",
            "toLower" => "Convert string to lowercase",
            "trim" => "Remove leading/trailing whitespace",
            "substring" => "Extract part of a string",
            "length" => "Get string or array length",
            "concat" => "Concatenate strings",
            "replace" => "Replace text in a string",
            "contains" => "Check if string contains text",
            "startsWith" => "Check if string starts with text",
            "endsWith" => "Check if string ends with text",
            "split" => "Split string into array",
            "format" => "Format a date value",
            "parseDate" => "Parse string to date",
            "addDays" => "Add days to a date",
            "addHours" => "Add hours to a date",
            "addMinutes" => "Add minutes to a date",
            "now" => "Current local date/time",
            "utcNow" => "Current UTC date/time",
            "round" => "Round a number",
            "floor" => "Round down to integer",
            "ceil" => "Round up to integer",
            "abs" => "Absolute value",
            "min" => "Minimum of values",
            "max" => "Maximum of values",
            "toString" => "Convert to string",
            "toNumber" => "Convert to number",
            "toBoolean" => "Convert to boolean",
            "coalesce" => "First non-null value",
            "ifNull" => "Default value if null",
            "iif" => "Inline if/else",
            _ => $"Function: {funcName}"
        };
    }

    private static string GetJsonPreview(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => $"\"{Truncate(element.GetString() ?? "", 20)}\"",
            System.Text.Json.JsonValueKind.Number => element.GetRawText(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            System.Text.Json.JsonValueKind.Null => "null",
            System.Text.Json.JsonValueKind.Array => $"[{element.GetArrayLength()} items]",
            System.Text.Json.JsonValueKind.Object => "{...}",
            _ => ""
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength] + "…";
    }
}

/// <summary>
/// Represents a single autocomplete suggestion for expression input.
/// </summary>
public record ExpressionSuggestion
{
    /// <summary>Display label shown in the dropdown.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Text to insert when the suggestion is selected.</summary>
    public string InsertText { get; init; } = string.Empty;

    /// <summary>Short description shown alongside the label.</summary>
    public string? Description { get; init; }

    /// <summary>Additional detail (e.g., node ID, type info).</summary>
    public string? Detail { get; init; }

    /// <summary>Kind of suggestion for icon/styling purposes.</summary>
    public SuggestionKind Kind { get; init; }

    /// <summary>Sort order within the suggestion list (lower = higher priority).</summary>
    public int SortOrder { get; init; }
}

/// <summary>
/// Categorizes autocomplete suggestions for styling and grouping.
/// </summary>
public enum SuggestionKind
{
    Keyword,
    Node,
    Property,
    Variable,
    Function
}
