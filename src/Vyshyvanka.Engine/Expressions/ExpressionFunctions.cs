using System.Globalization;
using System.Text.Json;

namespace Vyshyvanka.Engine.Expressions;

/// <summary>
/// Registry of built-in expression functions.
/// </summary>
public static class ExpressionFunctions
{
    private static readonly Dictionary<string, Func<object?[], object?>> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        // String functions
        ["toUpper"] = ToUpper,
        ["toLower"] = ToLower,
        ["trim"] = Trim,
        ["substring"] = Substring,
        ["length"] = Length,
        ["concat"] = Concat,
        ["replace"] = Replace,
        ["contains"] = Contains,
        ["startsWith"] = StartsWith,
        ["endsWith"] = EndsWith,
        ["split"] = Split,
        
        // Date functions
        ["format"] = FormatDate,
        ["parseDate"] = ParseDate,
        ["addDays"] = AddDays,
        ["addHours"] = AddHours,
        ["addMinutes"] = AddMinutes,
        ["now"] = Now,
        ["utcNow"] = UtcNow,
        
        // Math functions
        ["round"] = Round,
        ["floor"] = Floor,
        ["ceil"] = Ceil,
        ["abs"] = Abs,
        ["min"] = Min,
        ["max"] = Max,
        
        // Type conversion functions
        ["toString"] = ToString,
        ["toNumber"] = ToNumber,
        ["toBoolean"] = ToBoolean,
        
        // Utility functions
        ["coalesce"] = Coalesce,
        ["ifNull"] = IfNull,
        ["iif"] = Iif
    };

    /// <summary>
    /// Gets all registered function names.
    /// </summary>
    public static IEnumerable<string> GetFunctionNames() => Functions.Keys;

    /// <summary>
    /// Checks if a function exists.
    /// </summary>
    public static bool HasFunction(string name) => Functions.ContainsKey(name);

    /// <summary>
    /// Invokes a function by name with the given arguments.
    /// </summary>
    public static object? Invoke(string name, object?[] args)
    {
        if (!Functions.TryGetValue(name, out var func))
        {
            throw new ExpressionEvaluationException($"Unknown function '{name}'", name);
        }

        try
        {
            return func(args);
        }
        catch (ExpressionEvaluationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExpressionEvaluationException($"Function '{name}' failed: {ex.Message}", name, ex);
        }
    }

    #region String Functions

    private static object? ToUpper(object?[] args)
    {
        ValidateArgCount("toUpper", args, 1);
        var value = ConvertToString(args[0]);
        return value?.ToUpperInvariant();
    }

    private static object? ToLower(object?[] args)
    {
        ValidateArgCount("toLower", args, 1);
        var value = ConvertToString(args[0]);
        return value?.ToLowerInvariant();
    }

    private static object? Trim(object?[] args)
    {
        ValidateArgCount("trim", args, 1);
        var value = ConvertToString(args[0]);
        return value?.Trim();
    }

    private static object? Substring(object?[] args)
    {
        ValidateArgCount("substring", args, 2, 3);
        var value = ConvertToString(args[0]);
        if (value is null) return null;

        var start = ConvertToInt(args[1], "substring", "start");
        
        if (args.Length == 3)
        {
            var length = ConvertToInt(args[2], "substring", "length");
            if (start >= value.Length) return string.Empty;
            var actualLength = Math.Min(length, value.Length - start);
            return value.Substring(start, actualLength);
        }

        if (start >= value.Length) return string.Empty;
        return value[start..];
    }

    private static object? Length(object?[] args)
    {
        ValidateArgCount("length", args, 1);
        var value = args[0];
        
        return value switch
        {
            null => 0,
            string s => s.Length,
            JsonElement je when je.ValueKind == JsonValueKind.Array => je.GetArrayLength(),
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString()?.Length ?? 0,
            _ => ConvertToString(value)?.Length ?? 0
        };
    }

    private static object? Concat(object?[] args)
    {
        if (args.Length == 0) return string.Empty;
        return string.Concat(args.Select(a => ConvertToString(a) ?? string.Empty));
    }

    private static object? Replace(object?[] args)
    {
        ValidateArgCount("replace", args, 3);
        var value = ConvertToString(args[0]);
        var oldValue = ConvertToString(args[1]) ?? string.Empty;
        var newValue = ConvertToString(args[2]) ?? string.Empty;
        return value?.Replace(oldValue, newValue);
    }

    private static object? Contains(object?[] args)
    {
        ValidateArgCount("contains", args, 2);
        var value = ConvertToString(args[0]);
        var search = ConvertToString(args[1]);
        if (value is null || search is null) return false;
        return value.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static object? StartsWith(object?[] args)
    {
        ValidateArgCount("startsWith", args, 2);
        var value = ConvertToString(args[0]);
        var prefix = ConvertToString(args[1]);
        if (value is null || prefix is null) return false;
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static object? EndsWith(object?[] args)
    {
        ValidateArgCount("endsWith", args, 2);
        var value = ConvertToString(args[0]);
        var suffix = ConvertToString(args[1]);
        if (value is null || suffix is null) return false;
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static object? Split(object?[] args)
    {
        ValidateArgCount("split", args, 2);
        var value = ConvertToString(args[0]);
        var separator = ConvertToString(args[1]) ?? string.Empty;
        if (value is null) return Array.Empty<string>();
        return value.Split(separator);
    }

    #endregion

    #region Date Functions

    private static object? FormatDate(object?[] args)
    {
        ValidateArgCount("format", args, 1, 2);
        var date = ConvertToDateTime(args[0], "format");
        if (date is null) return null;

        var formatString = args.Length > 1 ? ConvertToString(args[1]) ?? "yyyy-MM-dd" : "yyyy-MM-dd";
        return date.Value.ToString(formatString, CultureInfo.InvariantCulture);
    }

    private static object? ParseDate(object?[] args)
    {
        ValidateArgCount("parseDate", args, 1, 2);
        var value = ConvertToString(args[0]);
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (args.Length > 1)
        {
            var formatString = ConvertToString(args[1]);
            if (!string.IsNullOrWhiteSpace(formatString) &&
                DateTime.TryParseExact(value, formatString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        throw new ExpressionEvaluationException($"Cannot parse '{value}' as a date", "parseDate");
    }

    private static object? AddDays(object?[] args)
    {
        ValidateArgCount("addDays", args, 2);
        var date = ConvertToDateTime(args[0], "addDays");
        if (date is null) return null;

        var days = ConvertToDouble(args[1], "addDays", "days");
        return date.Value.AddDays(days);
    }

    private static object? AddHours(object?[] args)
    {
        ValidateArgCount("addHours", args, 2);
        var date = ConvertToDateTime(args[0], "addHours");
        if (date is null) return null;

        var hours = ConvertToDouble(args[1], "addHours", "hours");
        return date.Value.AddHours(hours);
    }

    private static object? AddMinutes(object?[] args)
    {
        ValidateArgCount("addMinutes", args, 2);
        var date = ConvertToDateTime(args[0], "addMinutes");
        if (date is null) return null;

        var minutes = ConvertToDouble(args[1], "addMinutes", "minutes");
        return date.Value.AddMinutes(minutes);
    }

    private static object? Now(object?[] args)
    {
        ValidateArgCount("now", args, 0);
        return DateTime.Now;
    }

    private static object? UtcNow(object?[] args)
    {
        ValidateArgCount("utcNow", args, 0);
        return DateTime.UtcNow;
    }

    #endregion

    #region Math Functions

    private static object? Round(object?[] args)
    {
        ValidateArgCount("round", args, 1, 2);
        var value = ConvertToDouble(args[0], "round", "value");
        var decimals = args.Length > 1 ? ConvertToInt(args[1], "round", "decimals") : 0;
        return Math.Round(value, decimals);
    }

    private static object? Floor(object?[] args)
    {
        ValidateArgCount("floor", args, 1);
        var value = ConvertToDouble(args[0], "floor", "value");
        return Math.Floor(value);
    }

    private static object? Ceil(object?[] args)
    {
        ValidateArgCount("ceil", args, 1);
        var value = ConvertToDouble(args[0], "ceil", "value");
        return Math.Ceiling(value);
    }

    private static object? Abs(object?[] args)
    {
        ValidateArgCount("abs", args, 1);
        var value = ConvertToDouble(args[0], "abs", "value");
        return Math.Abs(value);
    }

    private static object? Min(object?[] args)
    {
        if (args.Length < 2)
        {
            throw new ExpressionEvaluationException("Function 'min' requires at least 2 arguments", "min");
        }
        
        var values = args.Select(a => ConvertToDouble(a, "min", "value")).ToArray();
        return values.Min();
    }

    private static object? Max(object?[] args)
    {
        if (args.Length < 2)
        {
            throw new ExpressionEvaluationException("Function 'max' requires at least 2 arguments", "max");
        }
        
        var values = args.Select(a => ConvertToDouble(a, "max", "value")).ToArray();
        return values.Max();
    }

    #endregion

    #region Type Conversion Functions

    private static object? ToString(object?[] args)
    {
        ValidateArgCount("toString", args, 1);
        return ConvertToString(args[0]);
    }

    private static object? ToNumber(object?[] args)
    {
        ValidateArgCount("toNumber", args, 1);
        var value = args[0];
        
        return value switch
        {
            null => 0.0,
            int i => (double)i,
            long l => (double)l,
            float f => (double)f,
            double d => d,
            decimal dec => (double)dec,
            string s when double.TryParse(s, CultureInfo.InvariantCulture, out var d) => d,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDouble(),
            JsonElement je when je.ValueKind == JsonValueKind.String && 
                double.TryParse(je.GetString(), CultureInfo.InvariantCulture, out var d) => d,
            _ => throw new ExpressionEvaluationException($"Cannot convert '{value}' to number", "toNumber")
        };
    }

    private static object? ToBoolean(object?[] args)
    {
        ValidateArgCount("toBoolean", args, 1);
        var value = args[0];
        
        return value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            string s => !string.IsNullOrWhiteSpace(s) && 
                !s.Equals("false", StringComparison.OrdinalIgnoreCase) && 
                !s.Equals("0", StringComparison.Ordinal),
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDouble() != 0,
            JsonElement je when je.ValueKind == JsonValueKind.String => 
                !string.IsNullOrWhiteSpace(je.GetString()) && 
                !je.GetString()!.Equals("false", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    #endregion

    #region Utility Functions

    private static object? Coalesce(object?[] args)
    {
        foreach (var arg in args)
        {
            if (arg is not null && !(arg is JsonElement je && je.ValueKind == JsonValueKind.Null))
            {
                return arg;
            }
        }
        return null;
    }

    private static object? IfNull(object?[] args)
    {
        ValidateArgCount("ifNull", args, 2);
        var value = args[0];
        var defaultValue = args[1];
        
        if (value is null || (value is JsonElement je && je.ValueKind == JsonValueKind.Null))
        {
            return defaultValue;
        }
        return value;
    }

    private static object? Iif(object?[] args)
    {
        ValidateArgCount("iif", args, 3);
        var condition = ConvertToBool(args[0]);
        return condition ? args[1] : args[2];
    }

    #endregion

    #region Helper Methods

    private static void ValidateArgCount(string funcName, object?[] args, int expected)
    {
        if (args.Length != expected)
        {
            throw new ExpressionEvaluationException(
                $"Function '{funcName}' expects {expected} argument(s), got {args.Length}", funcName);
        }
    }

    private static void ValidateArgCount(string funcName, object?[] args, int min, int max)
    {
        if (args.Length < min || args.Length > max)
        {
            throw new ExpressionEvaluationException(
                $"Function '{funcName}' expects {min}-{max} argument(s), got {args.Length}", funcName);
        }
    }

    private static string? ConvertToString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
            JsonElement je when je.ValueKind == JsonValueKind.Null => null,
            JsonElement je => je.GetRawText(),
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static int ConvertToInt(object? value, string funcName, string paramName)
    {
        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal dec => (int)dec,
            string s when int.TryParse(s, out var i) => i,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            _ => throw new ExpressionEvaluationException(
                $"Function '{funcName}' parameter '{paramName}' must be an integer", funcName)
        };
    }

    private static double ConvertToDouble(object? value, string funcName, string paramName)
    {
        return value switch
        {
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            decimal dec => (double)dec,
            string s when double.TryParse(s, CultureInfo.InvariantCulture, out var d) => d,
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDouble(),
            _ => throw new ExpressionEvaluationException(
                $"Function '{funcName}' parameter '{paramName}' must be a number", funcName)
        };
    }

    private static DateTime? ConvertToDateTime(object? value, string funcName)
    {
        return value switch
        {
            null => null,
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) => dt,
            JsonElement je when je.ValueKind == JsonValueKind.String && 
                DateTime.TryParse(je.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) => dt,
            _ => throw new ExpressionEvaluationException(
                $"Function '{funcName}' requires a date value", funcName)
        };
    }

    private static bool ConvertToBool(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            string s => !string.IsNullOrWhiteSpace(s) && 
                !s.Equals("false", StringComparison.OrdinalIgnoreCase) && 
                !s.Equals("0", StringComparison.Ordinal),
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            _ => true
        };
    }

    #endregion
}
