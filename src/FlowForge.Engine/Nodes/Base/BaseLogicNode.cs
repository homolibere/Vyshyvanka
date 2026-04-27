using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Engine.Nodes.Base;

/// <summary>
/// Abstract base class for logic nodes that control workflow flow
/// (conditionals, loops, merges).
/// </summary>
public abstract class BaseLogicNode : BaseNode
{
    /// <inheritdoc />
    public override NodeCategory Category => NodeCategory.Logic;

    /// <summary>
    /// Evaluates a condition from the input data.
    /// </summary>
    protected static bool EvaluateCondition(JsonElement data, string propertyPath, string @operator, JsonElement compareValue)
    {
        var actualValue = GetNestedProperty(data, propertyPath);
        
        return @operator.ToLowerInvariant() switch
        {
            "equals" or "==" or "eq" => JsonElementEquals(actualValue, compareValue),
            "notequals" or "!=" or "neq" => !JsonElementEquals(actualValue, compareValue),
            "greaterthan" or ">" or "gt" => CompareNumbers(actualValue, compareValue) > 0,
            "lessthan" or "<" or "lt" => CompareNumbers(actualValue, compareValue) < 0,
            "greaterthanorequal" or ">=" or "gte" => CompareNumbers(actualValue, compareValue) >= 0,
            "lessthanorequal" or "<=" or "lte" => CompareNumbers(actualValue, compareValue) <= 0,
            "contains" => StringContains(actualValue, compareValue),
            "startswith" => StringStartsWith(actualValue, compareValue),
            "endswith" => StringEndsWith(actualValue, compareValue),
            "isempty" => IsEmpty(actualValue),
            "isnotempty" => !IsEmpty(actualValue),
            "istrue" => actualValue.ValueKind == JsonValueKind.True,
            "isfalse" => actualValue.ValueKind == JsonValueKind.False,
            "isnull" => actualValue.ValueKind == JsonValueKind.Null || actualValue.ValueKind == JsonValueKind.Undefined,
            "isnotnull" => actualValue.ValueKind != JsonValueKind.Null && actualValue.ValueKind != JsonValueKind.Undefined,
            _ => throw new InvalidOperationException($"Unknown operator: {@operator}")
        };
    }

    /// <summary>
    /// Gets a nested property from a JSON element using dot notation.
    /// </summary>
    protected static JsonElement GetNestedProperty(JsonElement element, string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return element;

        var parts = propertyPath.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return default;

            if (!current.TryGetProperty(part, out current))
                return default;
        }

        return current;
    }

    private static bool JsonElementEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
            return false;

        return a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetDecimal() == b.GetDecimal(),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            _ => a.GetRawText() == b.GetRawText()
        };
    }

    private static int CompareNumbers(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != JsonValueKind.Number || b.ValueKind != JsonValueKind.Number)
            return 0;

        return a.GetDecimal().CompareTo(b.GetDecimal());
    }

    private static bool StringContains(JsonElement element, JsonElement searchValue)
    {
        if (element.ValueKind != JsonValueKind.String || searchValue.ValueKind != JsonValueKind.String)
            return false;

        return element.GetString()?.Contains(searchValue.GetString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool StringStartsWith(JsonElement element, JsonElement searchValue)
    {
        if (element.ValueKind != JsonValueKind.String || searchValue.ValueKind != JsonValueKind.String)
            return false;

        return element.GetString()?.StartsWith(searchValue.GetString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool StringEndsWith(JsonElement element, JsonElement searchValue)
    {
        if (element.ValueKind != JsonValueKind.String || searchValue.ValueKind != JsonValueKind.String)
            return false;

        return element.GetString()?.EndsWith(searchValue.GetString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private static bool IsEmpty(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrEmpty(element.GetString()),
            JsonValueKind.Array => element.GetArrayLength() == 0,
            JsonValueKind.Object => !element.EnumerateObject().Any(),
            _ => false
        };
    }
}
