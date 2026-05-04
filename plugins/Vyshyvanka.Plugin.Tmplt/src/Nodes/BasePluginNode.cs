using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Template.Nodes;

/// <summary>
/// Base class for plugin nodes providing common helpers.
/// Inherit from this instead of implementing INode directly.
/// </summary>
public abstract class BasePluginNode : INode
{
    private readonly string _id = Guid.NewGuid().ToString();

    public string Id => _id;
    public abstract string Type { get; }
    public abstract NodeCategory Category { get; }

    public abstract Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context);

    /// <summary>
    /// Creates a logger that bridges to the host's logging infrastructure via a delegate
    /// stored in the execution context. Works across assembly load context boundaries.
    /// </summary>
    protected ILogger CreateLogger(IExecutionContext context)
    {
        if (context.Variables.TryGetValue("__logAction", out var action) &&
            action is Action<int, string, string> logAction)
        {
            return new DelegateLogger(GetType().FullName ?? GetType().Name, logAction);
        }

        return NullLogger.Instance;
    }

    /// <summary>
    /// Lightweight ILogger implementation that forwards to a host-provided delegate.
    /// </summary>
    private sealed class DelegateLogger(string category, Action<int, string, string> logAction) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var level = logLevel switch
            {
                LogLevel.Debug => 0,
                LogLevel.Information => 1,
                LogLevel.Warning => 2,
                LogLevel.Error => 3,
                LogLevel.Critical => 3,
                _ => 1
            };

            var message = formatter(state, exception);
            if (exception is not null)
                message = $"{message} {exception.Message}";

            logAction(level, category, message);
        }
    }

    /// <summary>Creates a successful output from any serializable object.</summary>
    protected static NodeOutput SuccessOutput(object data) => new()
    {
        Data = JsonSerializer.SerializeToElement(data),
        Success = true
    };

    /// <summary>Creates a failure output with an error message.</summary>
    protected static NodeOutput FailureOutput(string errorMessage) => new()
    {
        Data = default,
        Success = false,
        ErrorMessage = errorMessage
    };

    /// <summary>Reads an optional configuration value.</summary>
    protected static T? GetConfigValue<T>(NodeInput input, string key)
    {
        if (input.Configuration.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        return input.Configuration.TryGetProperty(key, out var value)
            ? JsonSerializer.Deserialize<T>(value.GetRawText())
            : default;
    }

    /// <summary>Reads a required configuration value or throws.</summary>
    protected static T GetRequiredConfigValue<T>(NodeInput input, string key)
    {
        return GetConfigValue<T>(input, key)
            ?? throw new InvalidOperationException($"Required configuration '{key}' is missing");
    }
}
