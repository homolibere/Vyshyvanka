using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Jira.Nodes;

/// <summary>
/// Fetches project versions from Jira. Can return all versions or only active
/// (not released and not archived) ones.
/// </summary>
[NodeDefinition(
    Name = "Jira Version",
    Description = "Fetch project versions from Jira",
    Icon = "fa-brands fa-jira")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object)]
[NodeOutput("output", DisplayName = "Versions", Type = PortType.Object)]
[RequiresCredential(CredentialType.BasicAuth)]
[ConfigurationProperty("projectKey", "string", Description = "Jira project key (e.g. PROJ).", IsRequired = true)]
[ConfigurationProperty("activeOnly", "boolean",
    Description = "When true, return only active versions (not released and not archived). Default false.")]
public class JiraVersionNode : BaseJiraNode
{
    public override string Type => "jira-version";
    public override NodeCategory Category => NodeCategory.Action;

    public JiraVersionNode()
    {
    }

    internal JiraVersionNode(HttpClient? httpClient) : base(httpClient)
    {
    }

    public override async Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        var logger = CreateLogger(context);

        try
        {
            var projectKey = GetRequiredConfigValue<string>(input, "projectKey");
            var activeOnly = GetConfigValue<bool?>(input, "activeOnly") ?? false;

            logger.LogInformation("Jira version fetch for project {ProjectKey} (activeOnly={ActiveOnly})", projectKey,
                activeOnly);

            var (baseUrl, auth) = await ResolveCredentialsAsync(input, context);

            var path = $"project/{HttpUtility.UrlEncode(projectKey)}/versions";
            var response =
                await SendJiraRequestAsync(HttpMethod.Get, path, baseUrl, auth, null, context.CancellationToken);

            if (!response.IsSuccess)
                return FailureOutput($"Fetch versions failed ({response.StatusCode}): {response.ErrorBody}");

            var versions = response.Data!.Value;

            if (activeOnly && versions.ValueKind == JsonValueKind.Array)
            {
                var filtered = versions.EnumerateArray()
                    .Where(v =>
                    {
                        var released = v.TryGetProperty("released", out var r) && r.GetBoolean();
                        var archived = v.TryGetProperty("archived", out var a) && a.GetBoolean();
                        return !released && !archived;
                    })
                    .Select(v => v.Clone())
                    .ToList();

                return SuccessOutput(new { success = true, total = filtered.Count, versions = filtered });
            }

            return SuccessOutput(new { success = true, versions });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Jira version operation failed");
            return FailureOutput($"Jira Version error: {ex.Message}");
        }
    }
}
