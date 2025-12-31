using FlowForge.Core.Models;
using Microsoft.AspNetCore.Components;

namespace FlowForge.Designer.Components;

public partial class ConnectionLine
{
    [Parameter, EditorRequired]
    public Connection Connection { get; set; } = null!;

    [Parameter]
    public double StartX { get; set; }

    [Parameter]
    public double StartY { get; set; }

    [Parameter]
    public double EndX { get; set; }

    [Parameter]
    public double EndY { get; set; }

    [Parameter]
    public bool IsSelected { get; set; }

    [Parameter]
    public EventCallback OnSelect { get; set; }

    [Parameter]
    public EventCallback OnDelete { get; set; }

    private string GetBezierPath()
    {
        var dx = Math.Abs(EndX - StartX) * 0.5;
        var cp1x = StartX + dx;
        var cp2x = EndX - dx;
        return $"M {StartX} {StartY} C {cp1x} {StartY}, {cp2x} {EndY}, {EndX} {EndY}";
    }

    private static string GetArrowPoints() => "-6,-4 0,0 -6,4";

    private string GetArrowTransform()
    {
        var angle = Math.Atan2(EndY - StartY, EndX - StartX) * 180 / Math.PI;
        return $"translate({EndX}, {EndY}) rotate({angle})";
    }

    private async Task OnSelectClick()
    {
        await OnSelect.InvokeAsync();
    }

    private async Task OnDeleteClick()
    {
        await OnDelete.InvokeAsync();
    }
}
