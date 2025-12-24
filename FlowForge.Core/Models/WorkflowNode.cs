using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowForge.Core.Models;

/// <summary>
/// Represents a node instance within a workflow.
/// </summary>
public record WorkflowNode
{
    /// <summary>Unique identifier for this node within the workflow.</summary>
    [JsonPropertyName("id")]
    [Required(ErrorMessage = "Node ID is required")]
    [MinLength(1, ErrorMessage = "Node ID cannot be empty")]
    public string Id { get; init; } = string.Empty;
    
    /// <summary>Type of the node (references NodeDefinition.Type).</summary>
    [JsonPropertyName("type")]
    [Required(ErrorMessage = "Node type is required")]
    [MinLength(1, ErrorMessage = "Node type cannot be empty")]
    public string Type { get; init; } = string.Empty;
    
    /// <summary>Display name for this node instance.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Node name is required")]
    [MinLength(1, ErrorMessage = "Node name cannot be empty")]
    [MaxLength(200, ErrorMessage = "Node name cannot exceed 200 characters")]
    public string Name { get; init; } = string.Empty;
    
    /// <summary>Node-specific configuration.</summary>
    [JsonPropertyName("configuration")]
    public JsonElement Configuration { get; init; }
    
    /// <summary>Position on the designer canvas.</summary>
    [JsonPropertyName("position")]
    [Required(ErrorMessage = "Node position is required")]
    public Position Position { get; init; } = new();
    
    /// <summary>Optional credential ID for nodes requiring authentication.</summary>
    [JsonPropertyName("credentialId")]
    public Guid? CredentialId { get; init; }
}

/// <summary>
/// Position on the designer canvas.
/// </summary>
public record Position
{
    /// <summary>X coordinate.</summary>
    [JsonPropertyName("x")]
    public double X { get; init; }
    
    /// <summary>Y coordinate.</summary>
    [JsonPropertyName("y")]
    public double Y { get; init; }
    
    /// <summary>Creates a new Position with the specified coordinates.</summary>
    public Position() { }
    
    /// <summary>Creates a new Position with the specified coordinates.</summary>
    public Position(double x, double y)
    {
        X = x;
        Y = y;
    }
}
