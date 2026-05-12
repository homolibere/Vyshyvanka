using System.Text.Json;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Nodes.Base;
using Vyshyvanka.Core.Attributes;

namespace Vyshyvanka.Engine.Nodes.Logic;

/// <summary>
/// A logic node that evaluates a condition and routes data to different outputs.
/// </summary>
[NodeDefinition(
    Name = "If",
    Description = "Evaluate a condition and route data to true or false output",
    Icon = "fa-solid fa-code-branch")]
[NodeInput("input", DisplayName = "Input", IsRequired = true)]
[NodeOutput("true", DisplayName = "True")]
[NodeOutput("false", DisplayName = "False")]
[ConfigurationProperty("field", "string", Description = "Field path to evaluate", IsRequired = true)]
[ConfigurationProperty("operator", "string", Description = "Comparison operator", IsRequired = true,
    Options =
        "equals,notEquals,greaterThan,lessThan,greaterThanOrEqual,lessThanOrEqual,contains,startsWith,endsWith,isEmpty,isNotEmpty,isTrue,isFalse,isNull,isNotNull")]
[ConfigurationProperty("value", "string", Description = "Value to compare against")]
public class IfNode : BaseLogicNode
{
    private string _id = Guid.NewGuid().ToString();

    /// <inheritdoc />
    public override string Id => _id;

    /// <inheritdoc />
    public override string Type => "if";

    /// <inheritdoc />
    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var field = GetRequiredConfigValue<string>(input, "field");
        var @operator = GetRequiredConfigValue<string>(input, "operator");
        var compareValueJson = GetConfigValue<JsonElement?>(input, "value");

        var compareValue = compareValueJson ?? default;

        bool conditionResult;
        try
        {
            conditionResult = EvaluateCondition(input.Data, field, @operator, compareValue);
        }
        catch (Exception ex)
        {
            return Task.FromResult(FailureOutput($"Condition evaluation failed: {ex.Message}"));
        }

        // Return output with routing information
        var result = new Dictionary<string, object?>
        {
            ["data"] = JsonSerializer.Deserialize<object>(input.Data.GetRawText()),
            ["conditionResult"] = conditionResult,
            ["outputPort"] = conditionResult ? "true" : "false"
        };

        return Task.FromResult(SuccessOutput(result));
    }
}
