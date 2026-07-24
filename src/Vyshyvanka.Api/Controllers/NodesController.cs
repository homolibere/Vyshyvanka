using Vyshyvanka.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Vyshyvanka.Api.Controllers;

/// <summary>
/// Controller for retrieving available node definitions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class NodesController(INodeRegistry nodeRegistry) : ControllerBase
{
    /// <summary>Gets all available node definitions.</summary>
    [HttpGet]
    public ActionResult<IEnumerable<NodeDefinition>> GetAll()
    {
        return Ok(nodeRegistry.GetAllDefinitions());
    }

    /// <summary>Gets a specific node definition by type.</summary>
    [HttpGet("{nodeType}")]
    public ActionResult<NodeDefinition> GetByType(string nodeType)
    {
        var definition = nodeRegistry.GetDefinition(nodeType);
        if (definition is null)
            return NotFound();

        return Ok(definition);
    }
}
