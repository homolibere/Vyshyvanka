using System.Text.Json;
using System.Text.Json.Serialization;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Logic;

/// <summary>
/// A logic node that routes data to different outputs based on matching cases.
/// <para>
/// <b>Execution flow:</b> Reads a value from the input at the configured field path,
/// then compares it against each case definition in order. The first matching case
/// determines the output port. If no case matches, data routes to the <c>default</c> port.
/// </para>
/// <para>
/// <b>Ports:</b> Dynamic — one per case value, plus <c>default</c>.
/// Downstream nodes on inactive branches are skipped entirely.
/// </para>
/// </summary>
[NodeDefinition(
    Name = "Switch",
    Description = "Route data to different outputs based on matching values",
    Icon = "fa-solid fa-shuffle")]
[NodeInput("input", DisplayName = "Input", IsRequired = true)]
[NodeOutput("default", DisplayName = "Default")]
[ConfigurationProperty("field", "string", Description = "Field path to evaluate", IsRequired = true)]
[ConfigurationProperty("cases", "array", Description = "Array of case definitions with value and output", DataSource = "switch-cases")]
public class SwitchNode : BaseLogicNode
{
    private string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "switch";

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var field = GetRequiredConfigValue<string>(input, "field");
        var cases = GetConfigValue<List<SwitchCase>>(input, "cases") ?? [];

        // If the field contains a dot, treat it as a nested property path.
        // Otherwise (e.g. when an expression like {{ input.body.object_kind }} resolved to a value),
        // try as a path first, then fall back to using the resolved value directly.
        var fieldValue = GetNestedProperty(input.Data, field);

        if (fieldValue.ValueKind == JsonValueKind.Undefined && !field.Contains('.'))
        {
            // The field config resolved to a plain value (not a path) — use it directly for matching
            fieldValue = JsonSerializer.SerializeToElement(field);
        }

        // Find matching case
        string matchedOutput = "default";
        foreach (var switchCase in cases)
        {
            if (ValuesMatch(fieldValue, switchCase.Value))
            {
                matchedOutput = switchCase.Output ?? switchCase.Value?.ToString() ?? "default";
                break;
            }
        }

        var result = new Dictionary<string, object?>
        {
            ["data"] = JsonSerializer.Deserialize<object>(input.Data.GetRawText()),
            ["matchedCase"] = matchedOutput,
            ["outputPort"] = matchedOutput
        };

        return Task.FromResult(SuccessOutput(result));
    }

    private static bool ValuesMatch(JsonElement fieldValue, object? caseValue)
    {
        if (caseValue is null)
            return fieldValue.ValueKind == JsonValueKind.Null || fieldValue.ValueKind == JsonValueKind.Undefined;

        return fieldValue.ValueKind switch
        {
            JsonValueKind.String => fieldValue.GetString()
                ?.Equals(caseValue.ToString(), StringComparison.OrdinalIgnoreCase) ?? false,
            JsonValueKind.Number => decimal.TryParse(caseValue.ToString(), out var num) &&
                                    fieldValue.GetDecimal() == num,
            JsonValueKind.True => caseValue is true ||
                                  caseValue.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
            JsonValueKind.False => caseValue is false ||
                                   caseValue.ToString()?.Equals("false", StringComparison.OrdinalIgnoreCase) == true,
            _ => false
        };
    }
}

/// <summary>
/// Represents a case in a switch node.
/// </summary>
public record SwitchCase
{
    /// <summary>Value to match against.</summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }

    /// <summary>Output port name when matched.</summary>
    [JsonPropertyName("output")]
    public string? Output { get; init; }
}
