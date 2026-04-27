using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Layout;

public partial class DesignerLayout
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
