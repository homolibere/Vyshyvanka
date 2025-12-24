using System.Text.Json;
using System.Text.RegularExpressions;
using FlowForge.Core.Interfaces;

namespace FlowForge.Engine.Expressions;

/// <summary>
/// Default implementation of expression evaluator.
/// Supports syntax like {{ nodes.nodeName.output.field }} and {{ variables.name }}
/// Also supports array indexing with brackets: {{ nodes.nodeName.output.items[0].name }}
/// Supports function calls: {{ toUpper(nodes.nodeName.output.field) }}
/// </summary>
public partial class ExpressionEvaluator : IExpressionEvaluator
{
    private static readonly Regex ExpressionPattern = ExpressionRegex();
    private static readonly Regex ArrayIndexPattern = ArrayIndexRegex();
    private static readonly Regex FunctionCallPattern = FunctionCallRegex();

    /// <inheritdoc />
    public object? Evaluate(string expression, IExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(context);

        if (!TryEvaluate(expression, context, out var result, out var error))
        {
            throw new ExpressionEvaluationException(error ?? "Unknown evaluation error", expression);
        }
        
        return result;
    }

    /// <inheritdoc />
    public bool TryEvaluate(string expression, IExecutionContext context, out object? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            result = expression;
            return true;
        }

        // Check if expression contains template syntax
        var match = ExpressionPattern.Match(expression);
        if (!match.Success)
        {
            result = expression;
            return true;
        }

        try
        {
            // If the entire string is a single expression, return the actual value (not string)
            if (ExpressionPattern.Replace(expression, "").Trim() == "" && 
                ExpressionPattern.Matches(expression).Count == 1)
            {
                var path = match.Groups[1].Value.Trim();
                result = EvaluatePath(path, context);
                return true;
            }

            // Replace all expressions in the string (string interpolation mode)
            var evaluated = ExpressionPattern.Replace(expression, m =>
            {
                var path = m.Groups[1].Value.Trim();
                var value = EvaluatePath(path, context);
                return ConvertToString(value);
            });

            result = evaluated;
            return true;
        }
        catch (ExpressionEvaluationException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = $"Expression evaluation failed at '{expression}': {ex.Message}";
            return false;
        }
    }

    /// <inheritdoc />
    public ExpressionValidationResult Validate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new ExpressionValidationResult { IsValid = true };
        }

        var errors = new List<ExpressionError>();
        var matches = ExpressionPattern.Matches(expression);

        foreach (Match match in matches)
        {
            var path = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                errors.Add(new ExpressionError
                {
                    Position = match.Index,
                    Message = "Empty expression"
                });
                continue;
            }

            // Validate the path structure
            var validationError = ValidatePath(path, match.Index);
            if (validationError != null)
            {
                errors.Add(validationError);
            }
        }

        return new ExpressionValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static ExpressionError? ValidatePath(string path, int position)
    {
        // Check for valid root
        var normalizedPath = NormalizePath(path);
        var parts = normalizedPath.Split('.');
        
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
        {
            return new ExpressionError
            {
                Position = position,
                Message = "Expression path cannot be empty"
            };
        }

        var root = parts[0].ToLowerInvariant();
        if (root != "nodes" && root != "variables")
        {
            return new ExpressionError
            {
                Position = position,
                Message = $"Unknown expression root '{parts[0]}'. Expected 'nodes' or 'variables'"
            };
        }

        if (parts.Length < 2)
        {
            return new ExpressionError
            {
                Position = position,
                Message = $"Expression '{path}' is incomplete. Expected format: {root}.name..."
            };
        }

        return null;
    }

    private static object? EvaluatePath(string path, IExecutionContext context)
    {
        // Check if this is a function call
        var funcMatch = FunctionCallPattern.Match(path);
        if (funcMatch.Success)
        {
            return EvaluateFunctionCall(funcMatch, context, path);
        }

        // Normalize path: convert bracket notation to dot notation
        var normalizedPath = NormalizePath(path);
        var parts = normalizedPath.Split('.');
        
        if (parts.Length == 0)
        {
            throw new ExpressionEvaluationException($"Empty path in expression", path);
        }

        return parts[0].ToLowerInvariant() switch
        {
            "nodes" => EvaluateNodePath(parts[1..], context, path),
            "variables" => EvaluateVariablePath(parts[1..], context, path),
            _ => throw new ExpressionEvaluationException(
                $"Unknown expression root '{parts[0]}'. Expected 'nodes' or 'variables'", path)
        };
    }

    private static object? EvaluateFunctionCall(Match funcMatch, IExecutionContext context, string originalPath)
    {
        var funcName = funcMatch.Groups[1].Value;
        var argsString = funcMatch.Groups[2].Value;

        if (!ExpressionFunctions.HasFunction(funcName))
        {
            throw new ExpressionEvaluationException($"Unknown function '{funcName}'", originalPath);
        }

        // Parse arguments
        var args = ParseFunctionArguments(argsString, context, originalPath);
        
        return ExpressionFunctions.Invoke(funcName, args);
    }

    private static object?[] ParseFunctionArguments(string argsString, IExecutionContext context, string originalPath)
    {
        if (string.IsNullOrWhiteSpace(argsString))
        {
            return [];
        }

        var args = new List<object?>();
        var currentArg = new System.Text.StringBuilder();
        var depth = 0;
        var inString = false;
        var stringChar = '\0';

        for (var i = 0; i < argsString.Length; i++)
        {
            var c = argsString[i];

            // Handle string literals
            if ((c == '"' || c == '\'') && (i == 0 || argsString[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    stringChar = c;
                }
                else if (c == stringChar)
                {
                    inString = false;
                }
                currentArg.Append(c);
                continue;
            }

            if (inString)
            {
                currentArg.Append(c);
                continue;
            }

            // Handle nested parentheses
            if (c == '(')
            {
                depth++;
                currentArg.Append(c);
            }
            else if (c == ')')
            {
                depth--;
                currentArg.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                // Argument separator at top level
                args.Add(EvaluateArgument(currentArg.ToString().Trim(), context, originalPath));
                currentArg.Clear();
            }
            else
            {
                currentArg.Append(c);
            }
        }

        // Add the last argument
        var lastArg = currentArg.ToString().Trim();
        if (!string.IsNullOrEmpty(lastArg))
        {
            args.Add(EvaluateArgument(lastArg, context, originalPath));
        }

        return [.. args];
    }

    private static object? EvaluateArgument(string arg, IExecutionContext context, string originalPath)
    {
        if (string.IsNullOrWhiteSpace(arg))
        {
            return null;
        }

        // Check for string literals
        if ((arg.StartsWith('"') && arg.EndsWith('"')) || 
            (arg.StartsWith('\'') && arg.EndsWith('\'')))
        {
            return arg[1..^1]; // Remove quotes
        }

        // Check for numeric literals
        if (double.TryParse(arg, System.Globalization.NumberStyles.Any, 
            System.Globalization.CultureInfo.InvariantCulture, out var numValue))
        {
            // Return as int if it's a whole number
            if (numValue == Math.Floor(numValue) && numValue >= int.MinValue && numValue <= int.MaxValue)
            {
                return (int)numValue;
            }
            return numValue;
        }

        // Check for boolean literals
        if (arg.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (arg.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for null literal
        if (arg.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Otherwise, evaluate as a path or nested function call
        return EvaluatePath(arg, context);
    }

    /// <summary>
    /// Normalizes a path by converting bracket notation to dot notation.
    /// Example: "items[0].name" becomes "items.0.name"
    /// </summary>
    private static string NormalizePath(string path)
    {
        // Replace [n] with .n
        return ArrayIndexPattern.Replace(path, ".$1");
    }

    private static object? EvaluateNodePath(string[] parts, IExecutionContext context, string originalPath)
    {
        if (parts.Length == 0)
        {
            throw new ExpressionEvaluationException(
                "Node path requires at least a node ID. Expected format: nodes.nodeId.field", originalPath);
        }

        var nodeId = parts[0];
        var output = context.NodeOutputs.Get(nodeId);
        
        if (output is null)
        {
            throw new ExpressionEvaluationException(
                $"Node '{nodeId}' has no output data. Ensure the node has executed before referencing its output.", originalPath);
        }
        
        if (parts.Length == 1)
        {
            return ConvertJsonElement(output.Value);
        }

        // Navigate through the JSON structure
        return NavigateJson(output.Value, parts[1..], originalPath);
    }

    private static object? EvaluateVariablePath(string[] parts, IExecutionContext context, string originalPath)
    {
        if (parts.Length == 0)
        {
            throw new ExpressionEvaluationException(
                "Variable path requires a variable name. Expected format: variables.name", originalPath);
        }

        var variableName = parts[0];
        if (!context.Variables.TryGetValue(variableName, out var value))
        {
            throw new ExpressionEvaluationException(
                $"Variable '{variableName}' not found in execution context", originalPath);
        }

        if (parts.Length == 1)
        {
            return value;
        }

        // If value is JsonElement, navigate through it
        if (value is JsonElement jsonElement)
        {
            return NavigateJson(jsonElement, parts[1..], originalPath);
        }

        throw new ExpressionEvaluationException(
            $"Cannot access property '{parts[1]}' on variable '{variableName}' of type {value?.GetType().Name ?? "null"}", 
            originalPath);
    }

    private static object? NavigateJson(JsonElement element, string[] path, string originalPath)
    {
        var current = element;
        var currentPath = new List<string>();

        foreach (var part in path)
        {
            currentPath.Add(part);
            
            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(part, out current))
                {
                    throw new ExpressionEvaluationException(
                        $"Property '{part}' not found at path '{string.Join(".", currentPath)}'", originalPath);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index))
            {
                var arrayLength = current.GetArrayLength();
                if (index < 0 || index >= arrayLength)
                {
                    throw new ExpressionEvaluationException(
                        $"Array index {index} is out of bounds. Array length is {arrayLength} at path '{string.Join(".", currentPath)}'", 
                        originalPath);
                }
                current = current[index];
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                throw new ExpressionEvaluationException(
                    $"Expected numeric index for array access, got '{part}' at path '{string.Join(".", currentPath)}'", 
                    originalPath);
            }
            else
            {
                throw new ExpressionEvaluationException(
                    $"Cannot access '{part}' on {current.ValueKind} value at path '{string.Join(".", currentPath)}'", 
                    originalPath);
            }
        }

        return ConvertJsonElement(current);
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intVal) => intVal,
            JsonValueKind.Number when element.TryGetInt64(out var longVal) => longVal,
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            _ => element // Return JsonElement for objects and arrays
        };
    }

    private static string ConvertToString(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string s => s,
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Undefined => string.Empty,
                _ => je.GetRawText()
            },
            _ => value.ToString() ?? string.Empty
        };
    }

    [GeneratedRegex(@"\{\{\s*(.+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex ExpressionRegex();

    [GeneratedRegex(@"\[(\d+)\]", RegexOptions.Compiled)]
    private static partial Regex ArrayIndexRegex();

    [GeneratedRegex(@"^(\w+)\s*\((.*)\)$", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex FunctionCallRegex();
}

/// <summary>
/// Exception thrown when expression evaluation fails.
/// </summary>
public class ExpressionEvaluationException : Exception
{
    /// <summary>The expression that failed to evaluate.</summary>
    public string Expression { get; }

    /// <summary>
    /// Creates a new expression evaluation exception.
    /// </summary>
    public ExpressionEvaluationException(string message, string expression) 
        : base(message)
    {
        Expression = expression;
    }

    /// <summary>
    /// Creates a new expression evaluation exception with inner exception.
    /// </summary>
    public ExpressionEvaluationException(string message, string expression, Exception innerException) 
        : base(message, innerException)
    {
        Expression = expression;
    }
}
