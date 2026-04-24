using System.Text.Json;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Nodes.Base;
using FlowForge.Core.Attributes;

namespace FlowForge.Engine.Nodes.Logic;

/// <summary>
/// A logic node that routes data to different outputs based on matching cases.
/// </summary>
[NodeDefinition(
    Name = "Switch",
    Description = "Route data to different outputs based on matching values",
    Icon = "fa-solid fa-shuffle")]
[NodeInput("input", DisplayName = "Input", IsRequired = true)]
[NodeOutput("default", DisplayName = "Default")]
[ConfigurationProperty("field", "string", Description = "Field path to evaluate", IsRequired = true)]
[ConfigurationProperty("cases", "array", Description = "Array of case definitions with value and output")]
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

        var fieldValue = GetNestedProperty(input.Data, field);

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
    public object? Value { get; init; }

    /// <summary>Output port name when matched.</summary>
    public string? Output { get; init; }
}
