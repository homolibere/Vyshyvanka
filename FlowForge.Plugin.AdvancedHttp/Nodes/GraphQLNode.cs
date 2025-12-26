using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;

namespace FlowForge.Plugin.AdvancedHttp.Nodes;

/// <summary>
/// GraphQL request node for executing queries and mutations.
/// </summary>
public class GraphQLNode : BasePluginNode
{
    private readonly HttpClient? _httpClient;

    public override string Type => "graphql-request";
    public override NodeCategory Category => NodeCategory.Action;

    public GraphQLNode() : this(null)
    {
    }

    internal GraphQLNode(HttpClient? httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        try
        {
            var endpoint = GetRequiredConfigValue<string>(input, "endpoint");
            var query = GetRequiredConfigValue<string>(input, "query");
            var variables = GetConfigValue<Dictionary<string, object>>(input, "variables");
            var operationName = GetConfigValue<string>(input, "operationName");
            var headers = GetConfigValue<Dictionary<string, string>>(input, "headers");
            var timeoutSeconds = GetConfigValue<int?>(input, "timeout") ?? 30;

            using var client = _httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

            var requestBody = new Dictionary<string, object?> { ["query"] = query };

            if (variables is not null && variables.Count > 0)
                requestBody["variables"] = variables;

            if (!string.IsNullOrWhiteSpace(operationName))
                requestBody["operationName"] = operationName;

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json")
            };

            if (headers is not null)
            {
                foreach (var (key, value) in headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            if (input.CredentialId.HasValue)
                await ApplyCredentialsAsync(request, input.CredentialId.Value, context);

            var response = await client.SendAsync(request, context.CancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(context.CancellationToken);

            GraphQLResponse? graphqlResponse = null;
            try
            {
                graphqlResponse = JsonSerializer.Deserialize<GraphQLResponse>(responseBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException)
            {
            }

            var hasErrors = graphqlResponse?.Errors is { Count: > 0 };

            return SuccessOutput(new
            {
                statusCode = (int)response.StatusCode,
                data = graphqlResponse?.Data,
                errors = graphqlResponse?.Errors,
                extensions = graphqlResponse?.Extensions,
                hasErrors,
                isSuccess = response.IsSuccessStatusCode && !hasErrors,
                rawResponse = graphqlResponse is null ? responseBody : null
            });
        }
        catch (OperationCanceledException)
        {
            return FailureOutput("GraphQL request was cancelled");
        }
        catch (Exception ex)
        {
            return FailureOutput($"GraphQL request error: {ex.Message}");
        }
    }

    private static async Task ApplyCredentialsAsync(HttpRequestMessage request, Guid credentialId,
        IExecutionContext context)
    {
        var credentials = await context.Credentials.GetCredentialAsync(credentialId, context.CancellationToken);
        if (credentials is null) return;

        if (credentials.TryGetValue("apiKey", out var apiKey))
        {
            credentials.TryGetValue("headerName", out var headerName);
            credentials.TryGetValue("prefix", out var prefix);
            headerName ??= "Authorization";
            prefix ??= "Bearer";
            request.Headers.TryAddWithoutValidation(headerName,
                string.IsNullOrWhiteSpace(prefix) ? apiKey : $"{prefix} {apiKey}");
        }
        else if (credentials.TryGetValue("username", out var username) &&
                 credentials.TryGetValue("password", out var password))
        {
            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }
    }

    private record GraphQLResponse(
        JsonElement? Data,
        List<GraphQLError>? Errors,
        JsonElement? Extensions);

    private record GraphQLError(
        string Message,
        List<GraphQLErrorLocation>? Locations,
        List<object>? Path,
        JsonElement? Extensions);

    private record GraphQLErrorLocation(int Line, int Column);
}
