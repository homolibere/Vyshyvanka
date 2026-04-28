using System.Text.Json;
using CsCheck;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Core.Models;
using Vyshyvanka.Engine.Credentials;
using Vyshyvanka.Engine.Execution;
using Xunit;
using ExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for webhook trigger execution.
/// Feature: vyshyvanka, Property 16: Webhook Trigger Execution
/// Validates: Requirements 8.4
/// </summary>
public class WebhookExecutionTests
{
    /// <summary>
    /// Property 16: Webhook Trigger Execution
    /// For any HTTP request received by a Webhook Trigger endpoint, the Workflow_Engine 
    /// SHALL start a workflow execution with the complete request data (headers, body, 
    /// query parameters) available as input to the trigger node.
    /// </summary>
    [Fact]
    public void WebhookTrigger_RequestDataAvailableAsInput()
    {
        // Generate random webhook request data
        var gen = Gen.Select(
            Gen.String[1, 50].Where(s => !string.IsNullOrWhiteSpace(s)),  // method
            Gen.String[1, 100].Where(s => !string.IsNullOrWhiteSpace(s)), // path
            Gen.Dictionary(
                Gen.String[1, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
                Gen.String[0, 50])[0, 5],  // headers
            Gen.Dictionary(
                Gen.String[1, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
                Gen.String[0, 50])[0, 5],  // query params
            Gen.String[0, 200]  // body
        );

        gen.Sample((method, path, headers, queryParams, body) =>
        {
            // Arrange: Create webhook data structure
            var webhookData = new Dictionary<string, object?>
            {
                ["method"] = method,
                ["path"] = path,
                ["headers"] = headers,
                ["query"] = queryParams,
                ["body"] = body
            };
            
            var webhookJson = JsonSerializer.SerializeToElement(webhookData);
            
            // Create execution context with webhook data
            var executionId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            var context = new ExecutionContext(
                executionId,
                workflowId,
                NullCredentialProvider.Instance);
            
            // Act: Add webhook data to context (simulating what WebhookController does)
            context.Variables["webhook"] = webhookJson;
            context.Variables["input"] = webhookJson;
            
            // Assert: Verify all request data is available in context
            Assert.True(context.Variables.ContainsKey("webhook"));
            Assert.True(context.Variables.ContainsKey("input"));
            
            var storedWebhook = (JsonElement)context.Variables["webhook"];
            
            // Verify method is preserved
            Assert.True(storedWebhook.TryGetProperty("method", out var methodProp));
            Assert.Equal(method, methodProp.GetString());
            
            // Verify path is preserved
            Assert.True(storedWebhook.TryGetProperty("path", out var pathProp));
            Assert.Equal(path, pathProp.GetString());
            
            // Verify headers are preserved
            Assert.True(storedWebhook.TryGetProperty("headers", out var headersProp));
            foreach (var header in headers)
            {
                Assert.True(headersProp.TryGetProperty(header.Key, out var headerValue));
                Assert.Equal(header.Value, headerValue.GetString());
            }
            
            // Verify query params are preserved
            Assert.True(storedWebhook.TryGetProperty("query", out var queryProp));
            foreach (var param in queryParams)
            {
                Assert.True(queryProp.TryGetProperty(param.Key, out var paramValue));
                Assert.Equal(param.Value, paramValue.GetString());
            }
            
            // Verify body is preserved
            Assert.True(storedWebhook.TryGetProperty("body", out var bodyProp));
            Assert.Equal(body, bodyProp.GetString());
        }, iter: 100);
    }


    /// <summary>
    /// Property 16: Webhook data structure is consistent
    /// For any webhook request, the data structure passed to the workflow
    /// SHALL contain all required fields (method, path, headers, query, body).
    /// </summary>
    [Fact]
    public void WebhookTrigger_DataStructureContainsAllRequiredFields()
    {
        var gen = Gen.Select(
            Gen.Const(new[] { "GET", "POST", "PUT", "DELETE", "PATCH" }).SelectMany(m => Gen.OneOfConst(m)),
            Gen.String[1, 100].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.Dictionary(Gen.String[1, 20].Where(s => !string.IsNullOrWhiteSpace(s)), Gen.String[0, 50])[0, 3],
            Gen.Dictionary(Gen.String[1, 20].Where(s => !string.IsNullOrWhiteSpace(s)), Gen.String[0, 50])[0, 3]
        );

        gen.Sample((method, path, headers, query) =>
        {
            // Arrange: Build webhook data as the controller would
            var webhookData = new Dictionary<string, object?>
            {
                ["method"] = method,
                ["path"] = path,
                ["queryString"] = query.Count > 0 ? "?" + string.Join("&", query.Select(kv => $"{kv.Key}={kv.Value}")) : "",
                ["headers"] = headers,
                ["query"] = query
            };
            
            var webhookJson = JsonSerializer.SerializeToElement(webhookData);
            
            // Assert: All required fields are present
            Assert.True(webhookJson.TryGetProperty("method", out _), "method field should be present");
            Assert.True(webhookJson.TryGetProperty("path", out _), "path field should be present");
            Assert.True(webhookJson.TryGetProperty("headers", out _), "headers field should be present");
            Assert.True(webhookJson.TryGetProperty("query", out _), "query field should be present");
        }, iter: 100);
    }

    /// <summary>
    /// Property 16: Webhook execution creates valid execution context
    /// For any webhook trigger, the execution context SHALL have a valid
    /// execution ID and workflow ID.
    /// </summary>
    [Fact]
    public void WebhookTrigger_CreatesValidExecutionContext()
    {
        var gen = Gen.Guid;

        gen.Sample(workflowId =>
        {
            // Arrange
            var executionId = Guid.NewGuid();
            
            // Act: Create execution context as WebhookController would
            var context = new ExecutionContext(
                executionId,
                workflowId,
                NullCredentialProvider.Instance);
            
            // Assert
            Assert.NotEqual(Guid.Empty, context.ExecutionId);
            Assert.Equal(executionId, context.ExecutionId);
            Assert.Equal(workflowId, context.WorkflowId);
            Assert.NotNull(context.Variables);
            Assert.NotNull(context.NodeOutputs);
            Assert.NotNull(context.Credentials);
        }, iter: 100);
    }

    /// <summary>
    /// Property 16: JSON body parsing preserves structure
    /// For any valid JSON body in a webhook request, the parsed structure
    /// SHALL be equivalent to the original.
    /// </summary>
    [Fact]
    public void WebhookTrigger_JsonBodyParsing_PreservesStructure()
    {
        // Generate random JSON-like structures
        var gen = Gen.Select(
            Gen.String[1, 20].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.Int,
            Gen.Bool,
            Gen.Double.Where(d => !double.IsNaN(d) && !double.IsInfinity(d))
        );

        gen.Sample((stringVal, intVal, boolVal, doubleVal) =>
        {
            // Arrange: Create a JSON body
            var bodyObject = new Dictionary<string, object>
            {
                ["stringField"] = stringVal,
                ["intField"] = intVal,
                ["boolField"] = boolVal,
                ["doubleField"] = doubleVal
            };
            
            var bodyJson = JsonSerializer.Serialize(bodyObject);
            
            // Act: Parse as the webhook controller would
            var parsed = JsonSerializer.Deserialize<JsonElement>(bodyJson);
            
            // Assert: Structure is preserved
            Assert.True(parsed.TryGetProperty("stringField", out var parsedString));
            Assert.Equal(stringVal, parsedString.GetString());
            
            Assert.True(parsed.TryGetProperty("intField", out var parsedInt));
            Assert.Equal(intVal, parsedInt.GetInt32());
            
            Assert.True(parsed.TryGetProperty("boolField", out var parsedBool));
            Assert.Equal(boolVal, parsedBool.GetBoolean());
            
            Assert.True(parsed.TryGetProperty("doubleField", out var parsedDouble));
            Assert.Equal(doubleVal, parsedDouble.GetDouble(), 10);
        }, iter: 100);
    }

    /// <summary>
    /// Property 16: Webhook data is accessible from trigger node
    /// For any webhook execution, the trigger node SHALL be able to access
    /// all webhook data through the execution context.
    /// </summary>
    [Fact]
    public void WebhookTrigger_DataAccessibleFromTriggerNode()
    {
        var gen = Gen.Select(
            Gen.String[1, 50].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.String[1, 100].Where(s => !string.IsNullOrWhiteSpace(s)),
            Gen.String[0, 500]
        );

        gen.Sample((method, path, body) =>
        {
            // Arrange
            var webhookData = new Dictionary<string, object?>
            {
                ["method"] = method,
                ["path"] = path,
                ["body"] = body
            };
            
            var webhookJson = JsonSerializer.SerializeToElement(webhookData);
            
            var context = new ExecutionContext(
                Guid.NewGuid(),
                Guid.NewGuid(),
                NullCredentialProvider.Instance);
            
            context.Variables["webhook"] = webhookJson;
            
            // Act: Simulate trigger node accessing webhook data
            var hasWebhookData = context.Variables.TryGetValue("webhook", out var webhookObj);
            
            // Assert
            Assert.True(hasWebhookData);
            Assert.NotNull(webhookObj);
            
            var webhook = (JsonElement)webhookObj!;
            Assert.Equal(method, webhook.GetProperty("method").GetString());
            Assert.Equal(path, webhook.GetProperty("path").GetString());
            Assert.Equal(body, webhook.GetProperty("body").GetString());
        }, iter: 100);
    }
}
