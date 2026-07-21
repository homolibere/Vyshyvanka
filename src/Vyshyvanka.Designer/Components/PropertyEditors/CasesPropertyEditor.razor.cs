using System.Text.Json;
using Vyshyvanka.Designer.Models;
using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Components;

/// <summary>
/// Visual editor for Switch node cases. Renders an add/remove row-based UI
/// instead of requiring users to write raw JSON arrays.
/// </summary>
public partial class CasesPropertyEditor : ComponentBase
{
    [Parameter, EditorRequired]
    public ConfigurationProperty Property { get; set; } = null!;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> ValueChanged { get; set; }

    [Parameter]
    public bool ShowValidationError { get; set; }

    private List<CaseEntry> _cases = [];
    private string _lastEmittedJson = "[]";
    private bool _initialized;

    protected override void OnParametersSet()
    {
        // Get the JSON representation of the incoming value
        var incomingJson = SerializeIncoming(Value);

        // Only re-parse if the value changed externally (not echoed from our own emit)
        if (!_initialized || incomingJson != _lastEmittedJson)
        {
            _cases = ParseCases(Value);
            _lastEmittedJson = incomingJson;
            _initialized = true;
        }
    }

    private void AddCase()
    {
        _cases.Add(new CaseEntry { Value = "", Output = "" });
        // Don't emit — empty cases are purely local UI state
    }

    private void RemoveCase(int index)
    {
        if (index >= 0 && index < _cases.Count)
        {
            _cases.RemoveAt(index);
            EmitValue();
        }
    }

    private void UpdateCaseValue(int index, ChangeEventArgs e)
    {
        if (index >= 0 && index < _cases.Count)
        {
            _cases[index] = _cases[index] with { Value = e.Value?.ToString() ?? "" };
            EmitValue();
        }
    }

    private void UpdateCaseOutput(int index, ChangeEventArgs e)
    {
        if (index >= 0 && index < _cases.Count)
        {
            _cases[index] = _cases[index] with { Output = e.Value?.ToString() ?? "" };
            EmitValue();
        }
    }

    private string GetDefaultOutputName(int index)
    {
        var value = _cases[index].Value;
        return string.IsNullOrEmpty(value) ? "output port name (optional)" : value;
    }

    private void EmitValue()
    {
        // Serialize only non-empty cases to the parent, but keep empty rows visible locally
        var casesArray = _cases
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c =>
            {
                var dict = new Dictionary<string, object?> { ["value"] = ParseTypedValue(c.Value) };
                if (!string.IsNullOrWhiteSpace(c.Output))
                    dict["output"] = c.Output;
                return dict;
            })
            .ToList();

        var json = JsonSerializer.Serialize(casesArray);
        _lastEmittedJson = json;

        var element = JsonDocument.Parse(json).RootElement.Clone();
        ValueChanged.InvokeAsync(element);
    }

    private static string SerializeIncoming(object? value)
    {
        if (value is null) return "[]";

        if (value is JsonElement je)
        {
            return je.ValueKind == JsonValueKind.Array ? je.GetRawText() : "[]";
        }

        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return "[]";
    }

    /// <summary>
    /// Attempts to parse a string value into its typed equivalent (number, boolean, null).
    /// Falls back to string if no other type matches.
    /// </summary>
    private static object? ParseTypedValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // null
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        // boolean
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        // number
        if (decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            // Return int if it's a whole number
            if (number == Math.Floor(number) && number is >= int.MinValue and <= int.MaxValue)
                return (int)number;
            return number;
        }

        // string
        return value;
    }

    private static List<CaseEntry> ParseCases(object? value)
    {
        if (value is null) return [];

        try
        {
            JsonElement element;

            if (value is JsonElement je)
            {
                element = je;
            }
            else if (value is string s && !string.IsNullOrWhiteSpace(s))
            {
                element = JsonDocument.Parse(s).RootElement;
            }
            else
            {
                return [];
            }

            if (element.ValueKind != JsonValueKind.Array) return [];

            var result = new List<CaseEntry>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var caseValue = "";
                var caseOutput = "";

                if (item.TryGetProperty("value", out var valProp))
                {
                    caseValue = valProp.ValueKind switch
                    {
                        JsonValueKind.String => valProp.GetString() ?? "",
                        JsonValueKind.Number => valProp.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => "null",
                        _ => valProp.GetRawText()
                    };
                }

                if (item.TryGetProperty("output", out var outProp) && outProp.ValueKind == JsonValueKind.String)
                {
                    caseOutput = outProp.GetString() ?? "";
                }

                result.Add(new CaseEntry { Value = caseValue, Output = caseOutput });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private record CaseEntry
    {
        public string Value { get; init; } = "";
        public string Output { get; init; } = "";
    }
}
