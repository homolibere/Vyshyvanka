using Microsoft.AspNetCore.Components;

namespace Vyshyvanka.Designer.Layout;

public partial class DesignerLayout
{
    private RenderFragment? _toolbarContent;

    /// <summary>
    /// Called by child pages to inject toolbar content into the header.
    /// </summary>
    public void SetToolbar(RenderFragment? content)
    {
        _toolbarContent = content;
        StateHasChanged();
    }
}
