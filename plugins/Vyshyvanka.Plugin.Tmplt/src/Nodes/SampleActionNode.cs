using System.Text.Json;
using Vyshyvanka.Core.Attributes;
using Vyshyvanka.Core.Enums;
using Vyshyvanka.Core.Interfaces;

namespace Vyshyvanka.Plugin.Template.Nodes;

/// <summary>
/// A sample action node — replace this with your own implementation.
/// </summary>
[NodeDefinition(
    Name = "Sample Action",
    Description = "A sample action node that echoes its input with a greeting",
    Icon = "fa-solid fa-flask")]
[NodeInput("input", DisplayName = "Input", Type = PortType.Object, IsRequired = true)]
[NodeOutput("output", DisplayName = "Output", Type = PortType.Object)]
[ConfigurationProperty("greeting", "string", Description = "Greeting prefix", IsRequired = true)]
public class SampleActionNode : BasePluginNode
{
    public override string Type => "sample-action";
    public override NodeCategory Category => NodeCategory.Action;

    public override Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        try
        {
            var greeting = GetRequiredConfigValue<string>(input, "greeting");

            var result = SuccessOutput(new
            {
                message = $"{greeting} from SampleActionNode!",
                receivedInput = input.Data,
                executionId = context.ExecutionId,
                timestamp = DateTime.UtcNow
            });

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult(FailureOutput(ex.Message));
        }
    }
}
