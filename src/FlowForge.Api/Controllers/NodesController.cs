using FlowForge.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FlowForge.Api.Controllers;

/// <summary>
/// Controller for retrieving available node definitions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NodesController : ControllerBase
{
    private readonly INodeRegistry _nodeRegistry;

    public NodesController(INodeRegistry nodeRegistry)
    {
        _nodeRegistry = nodeRegistry;
    }

    /// <summary>Gets all available node definitions.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<NodeDefinition>> GetAll()
    {
        return Ok(_nodeRegistry.GetAllDefinitions());
    }

    /// <summary>Gets a specific node definition by type.</summary>
    [HttpGet("{nodeType}")]
    public ActionResult<NodeDefinition> GetByType(string nodeType)
    {
        var definition = _nodeRegistry.GetDefinition(nodeType);
        if (definition is null)
            return NotFound();

        return Ok(definition);
    }
}
