using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.AdvancedHttp.Nodes;

/// <summary>
/// HTTP batch request node that executes multiple HTTP requests in parallel or sequentially.
/// </summary>
[NodeDefinition(
    Name = "HTTP Batch",
    Description = "Execute multiple HTTP requests in parallel or sequentially with concurrency control",
    Icon = "fa-solid fa-layer-group")]
[NodeInput("input", DisplayName = "Input")]
[NodeOutput("output", DisplayName = "Results", Type = PortType.Object)]
[ConfigurationProperty("requests", "array",
    Description = "Array of request objects with url, method, id, headers, body", IsRequired = true)]
[ConfigurationProperty("mode", "string", Description = "Execution mode: parallel or sequential (default: parallel)")]
[ConfigurationProperty("maxConcurrency", "number", Description = "Max parallel requests (default: 10)")]
[ConfigurationProperty("stopOnError", "boolean",
    Description = "Stop on first error in sequential mode (default: false)")]
[ConfigurationProperty("timeout", "number", Description = "Per-request timeout in seconds (default: 30)")]
public class HttpBatchNode : BasePluginNode
{
    private readonly HttpClient? _httpClient;

    public override string Type => "http-batch";
    public override NodeCategory Category => NodeCategory.Action;

    public HttpBatchNode() : this(null)
    {
    }

    internal HttpBatchNode(HttpClient? httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            var requests = GetRequiredConfigValue<List<BatchRequestConfig>>(input, "requests");
            var mode = GetConfigValue<string>(input, "mode")?.ToLowerInvariant() ?? "parallel";
            var stopOnError = GetConfigValue<bool?>(input, "stopOnError") ?? false;
            var maxConcurrency = GetConfigValue<int?>(input, "maxConcurrency") ?? 10;
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;

            logger.LogInformation("HTTP batch executing {Count} requests in {Mode} mode", requests.Count, mode);

            using var client = _httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            var results = mode == "sequential"
                ? await ExecuteSequentialAsync(client, requests, stopOnError, context)
                : await ExecuteParallelAsync(client, requests, maxConcurrency, context);

            var successCount = results.Count(r => r.Success);
            var failureCount = results.Count - successCount;

            logger.LogInformation("HTTP batch completed: {Success} succeeded, {Failed} failed out of {Total}",
                successCount, failureCount, results.Count);

            return SuccessOutput(new
            {
                results,
                summary = new
                {
                    total = results.Count,
                    successful = successCount,
                    failed = failureCount,
                    allSuccessful = failureCount == 0
                }
            });
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("HTTP batch request was cancelled");
            return FailureOutput("Batch request was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP batch request error");
            return FailureOutput($"Batch request error: {ex.Message}");
        }
    }

    private async Task<List<BatchResult>> ExecuteSequentialAsync(
        HttpClient client,
        List<BatchRequestConfig> requests,
        bool stopOnError,
        IExecutionContext context)
    {
        var results = new List<BatchResult>();

        for (var i = 0; i < requests.Count; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var result = await ExecuteSingleRequestAsync(client, requests[i], i, context);
            results.Add(result);

            if (!result.Success && stopOnError)
                break;
        }

        return results;
    }

    private async Task<List<BatchResult>> ExecuteParallelAsync(
        HttpClient client,
        List<BatchRequestConfig> requests,
        int maxConcurrency,
        IExecutionContext context)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = requests.Select(async (req, index) =>
        {
            await semaphore.WaitAsync(context.CancellationToken);
            try
            {
                return await ExecuteSingleRequestAsync(client, req, index, context);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return [.. results.OrderBy(r => r.Index)];
    }

    private static async Task<BatchResult> ExecuteSingleRequestAsync(
        HttpClient client,
        BatchRequestConfig config,
        int index,
        IExecutionContext context)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var request = new HttpRequestMessage(new HttpMethod(config.Method ?? "GET"), config.Url);

            if (config.Headers is not null)
            {
                foreach (var (key, value) in config.Headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            if (config.Body is not null && config.Method is "POST" or "PUT" or "PATCH")
            {
                var bodyJson = JsonSerializer.Serialize(config.Body);
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, context.CancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);

            object? parsedBody = responseBody;
            try
            {
                if (!string.IsNullOrWhiteSpace(responseBody))
                    parsedBody = JsonSerializer.Deserialize<object>(responseBody);
            }
            catch (JsonException)
            {
            }

            return new BatchResult(
                index,
                config.Id ?? $"request_{index}",
                config.Url,
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                parsedBody,
                null,
                (DateTime.UtcNow - startTime).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            return new BatchResult(
                index,
                config.Id ?? $"request_{index}",
                config.Url,
                null,
                false,
                null,
                ex.Message,
                (DateTime.UtcNow - startTime).TotalMilliseconds);
        }
    }

    private record BatchRequestConfig(
        string Url,
        string? Method = "GET",
        string? Id = null,
        Dictionary<string, string>? Headers = null,
        object? Body = null);

    private record BatchResult(
        int Index,
        string Id,
        string Url,
        int? StatusCode,
        bool Success,
        object? Body,
        string? Error,
        double DurationMs);
}
