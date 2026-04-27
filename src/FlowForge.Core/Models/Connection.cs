using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FlowForge.Core.Models;

/// <summary>
/// Represents a connection between two nodes in a workflow.
/// </summary>
public record Connection
{
    /// <summary>ID of the source node.</summary>
    [JsonPropertyName("sourceNodeId")]
    [Required(ErrorMessage = "Source node ID is required")]
    [MinLength(1, ErrorMessage = "Source node ID cannot be empty")]
    public string SourceNodeId { get; init; } = string.Empty;
    
    /// <summary>Name of the output port on the source node.</summary>
    [JsonPropertyName("sourcePort")]
    [Required(ErrorMessage = "Source port is required")]
    [MinLength(1, ErrorMessage = "Source port cannot be empty")]
    public string SourcePort { get; init; } = "output";
    
    /// <summary>ID of the target node.</summary>
    [JsonPropertyName("targetNodeId")]
    [Required(ErrorMessage = "Target node ID is required")]
    [MinLength(1, ErrorMessage = "Target node ID cannot be empty")]
    public string TargetNodeId { get; init; } = string.Empty;
    
    /// <summary>Name of the input port on the target node.</summary>
    [JsonPropertyName("targetPort")]
    [Required(ErrorMessage = "Target port is required")]
    [MinLength(1, ErrorMessage = "Target port cannot be empty")]
    public string TargetPort { get; init; } = "input";
}
