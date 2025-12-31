using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class NodeIcon
{
    [Parameter]
    public string? Icon { get; set; }

    [Parameter]
    public string DefaultIcon { get; set; } = "📦";

    [Parameter]
    public string? Size { get; set; }

    private static bool IsUrl(string? icon) =>
        !string.IsNullOrEmpty(icon) &&
        (icon.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static bool IsFontAwesome(string? icon) =>
        !string.IsNullOrEmpty(icon) &&
        (icon.StartsWith("fa-", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("fa ", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("fas ", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("far ", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("fab ", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("fal ", StringComparison.OrdinalIgnoreCase) ||
         icon.StartsWith("fad ", StringComparison.OrdinalIgnoreCase));

    private string GetSizeStyle() => string.IsNullOrEmpty(Size) ? "" : $"font-size: {Size}; width: {Size}; height: {Size};";
}
