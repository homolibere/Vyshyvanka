using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.AdvancedHttp.Nodes;

/// <summary>
/// HTTP polling node that repeatedly calls an endpoint until a condition is met.
/// Useful for waiting on async operations or checking status endpoints.
/// </summary>
[NodeDefinition(
    Name = "HTTP Polling",
    Description = "Poll an HTTP endpoint at intervals until a success or failure condition is met",
    Icon = "fa-solid fa-rotate")]
[NodeInput("input", DisplayName = "Input")]
[NodeOutput("output", DisplayName = "Response", Type = PortType.Object)]
[ConfigurationProperty("url", "string", Description = "URL to poll", IsRequired = true)]
[ConfigurationProperty("method", "string", Description = "HTTP method (default: GET)",
    Options = "GET,POST,PUT,DELETE,PATCH")]
[ConfigurationProperty("headers", "object", Description = "HTTP headers to include")]
[ConfigurationProperty("body", "object", Description = "Request body (for POST/PUT/PATCH)")]
[ConfigurationProperty("intervalMs", "number", Description = "Polling interval in milliseconds (default: 5000)")]
[ConfigurationProperty("maxAttempts", "number", Description = "Maximum polling attempts (default: 60)")]
[ConfigurationProperty("timeout", "number", Description = "Per-request timeout in seconds (default: 30)")]
[ConfigurationProperty("successJsonPath", "string", Description = "JSON path to check for success value")]
[ConfigurationProperty("successValue", "string", Description = "Value that indicates success")]
[ConfigurationProperty("failureJsonPath", "string", Description = "JSON path to check for failure value")]
[ConfigurationProperty("failureValue", "string", Description = "Value that indicates failure")]
public class HttpPollingNode : BasePluginNode
{
    private readonly HttpClient? _httpClient;

    public override string Type => "http-polling";
    public override NodeCategory Category => NodeCategory.Action;

    public HttpPollingNode() : this(null)
    {
    }

    internal HttpPollingNode(HttpClient? httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            var url = GetRequiredConfigValue<string>(input, "url");
            var method = GetConfigValue<string>(input, "method")?.ToUpperInvariant() ?? "GET";
            var headers = GetConfigValue<Dictionary<string, string>>(input, "headers");
            var body = GetConfigValue<JsonElement?>(input, "body");
            var intervalMs = GetConfigValue<int?>(input, "intervalMs") ?? 5000;
            var maxAttempts = GetConfigValue<int?>(input, "maxAttempts") ?? 60;
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;
            var successJsonPath = GetConfigValue<string>(input, "successJsonPath");
            var successValue = GetConfigValue<string>(input, "successValue");
            var failureJsonPath = GetConfigValue<string>(input, "failureJsonPath");
            var failureValue = GetConfigValue<string>(input, "failureValue");

            logger.LogInformation("HTTP polling {Method} {Url} (interval={IntervalMs}ms, maxAttempts={MaxAttempts})",
                method, url, intervalMs, maxAttempts);

            using var client = _httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            var pollResults = new List<PollResult>();

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var request = new HttpRequestMessage(new HttpMethod(method), url);

                if (headers is not null)
                {
                    foreach (var (key, value) in headers)
                        request.Headers.TryAddWithoutValidation(key, value);
                }

                if (body.HasValue && method is "POST" or "PUT" or "PATCH")
                    request.Content = new StringContent(body.Value.GetRawText(), Encoding.UTF8, "application/json");

                var attemptStart = DateTime.UtcNow;
                HttpResponseMessage response;

                try
                {
                    response = await client.SendAsync(request, context.CancellationToken);
                }
                catch (HttpRequestException ex)
                {
                    pollResults.Add(new PollResult(attempt, null, ex.Message,
                        (DateTime.UtcNow - attemptStart).TotalMilliseconds));

                    if (attempt < maxAttempts)
                        await Task.Delay(intervalMs, context.CancellationToken);
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);
                JsonElement? parsedBody = null;

                try
                {
                    if (!string.IsNullOrWhiteSpace(responseBody))
                        parsedBody = JsonSerializer.Deserialize<JsonElement>(responseBody);
                }
                catch (JsonException)
                {
                }

                pollResults.Add(new PollResult(attempt, (int)response.StatusCode, null,
                    (DateTime.UtcNow - attemptStart).TotalMilliseconds));

                // Check failure condition first
                if (!string.IsNullOrWhiteSpace(failureJsonPath) && parsedBody.HasValue)
                {
                    var failureCheckValue = GetJsonPathValue(parsedBody.Value, failureJsonPath);
                    if (failureCheckValue == failureValue)
                    {
                        logger.LogWarning(
                            "HTTP polling {Url} matched failure condition on attempt {Attempt}: {Path}={Value}", url,
                            attempt, failureJsonPath, failureCheckValue);
                        return SuccessOutput(new
                        {
                            status = "failure_condition_met",
                            statusCode = (int)response.StatusCode,
                            body = parsedBody.HasValue
                                ? JsonSerializer.Deserialize<object>(parsedBody.Value.GetRawText())
                                : responseBody,
                            pollAttempts = pollResults,
                            totalAttempts = attempt,
                            matchedValue = failureCheckValue
                        });
                    }
                }

                // Check success condition
                if (!string.IsNullOrWhiteSpace(successJsonPath) && parsedBody.HasValue)
                {
                    var successCheckValue = GetJsonPathValue(parsedBody.Value, successJsonPath);
                    if (successCheckValue == successValue)
                    {
                        logger.LogInformation("HTTP polling {Url} matched success condition on attempt {Attempt}", url,
                            attempt);
                        return SuccessOutput(new
                        {
                            status = "success",
                            statusCode = (int)response.StatusCode,
                            body = parsedBody.HasValue
                                ? JsonSerializer.Deserialize<object>(parsedBody.Value.GetRawText())
                                : responseBody,
                            pollAttempts = pollResults,
                            totalAttempts = attempt,
                            matchedValue = successCheckValue
                        });
                    }
                }
                else if (response.IsSuccessStatusCode && string.IsNullOrWhiteSpace(successJsonPath))
                {
                    // No condition specified, success on 2xx
                    return SuccessOutput(new
                    {
                        status = "success",
                        statusCode = (int)response.StatusCode,
                        body = parsedBody.HasValue
                            ? JsonSerializer.Deserialize<object>(parsedBody.Value.GetRawText())
                            : responseBody,
                        pollAttempts = pollResults,
                        totalAttempts = attempt
                    });
                }

                if (attempt < maxAttempts)
                    await Task.Delay(intervalMs, context.CancellationToken);
            }

            return SuccessOutput(new
            {
                status = "timeout",
                message = $"Polling timed out after {maxAttempts} attempts",
                pollAttempts = pollResults,
                totalAttempts = maxAttempts
            });
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("HTTP polling was cancelled");
            return FailureOutput("Polling was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP polling error");
            return FailureOutput($"Polling error: {ex.Message}");
        }
    }

    private static string? GetJsonPathValue(JsonElement element, string path)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var current = element;

        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(part, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText()
        };
    }

    private record PollResult(int Attempt, int? StatusCode, string? Error, double DurationMs);
}
