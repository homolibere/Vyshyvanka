using System.Reflection;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Engine.Registry;

namespace FlowForge.Engine.Plugins;

/// <summary>
/// Validates plugin assemblies and their node definitions.
/// </summary>
public class PluginValidator : IPluginValidator
{
    /// <inheritdoc />
    public PluginValidationResult ValidatePlugin(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        
        var errors = new List<PluginValidationError>();
        var warnings = new List<PluginValidationWarning>();
        
        // Check for PluginAttribute
        var pluginAttr = assembly.GetCustomAttribute<PluginAttribute>();
        if (pluginAttr is null)
        {
            errors.Add(new PluginValidationError(
                "PLUGIN_MISSING_ATTRIBUTE",
                "Assembly must have a PluginAttribute to be recognized as a FlowForge plugin")
            {
                Context = assembly.FullName
            });
            return new PluginValidationResult { Errors = errors, Warnings = warnings };
        }
        
        // Validate plugin ID
        if (string.IsNullOrWhiteSpace(pluginAttr.Id))
        {
            errors.Add(new PluginValidationError(
                "PLUGIN_INVALID_ID",
                "Plugin ID cannot be empty")
            {
                Context = assembly.FullName
            });
        }
        
        // Validate plugin name
        if (string.IsNullOrWhiteSpace(pluginAttr.Name))
        {
            warnings.Add(new PluginValidationWarning(
                "PLUGIN_MISSING_NAME",
                "Plugin should have a display name")
            {
                Context = pluginAttr.Id
            });
        }
        
        // Validate plugin version
        if (string.IsNullOrWhiteSpace(pluginAttr.Version))
        {
            warnings.Add(new PluginValidationWarning(
                "PLUGIN_MISSING_VERSION",
                "Plugin should have a version")
            {
                Context = pluginAttr.Id
            });
        }
        
        // Discover and validate node types
        var nodeTypes = DiscoverNodeTypes(assembly);
        
        if (nodeTypes.Count == 0)
        {
            warnings.Add(new PluginValidationWarning(
                "PLUGIN_NO_NODES",
                "Plugin does not contain any node types")
            {
                Context = pluginAttr.Id
            });
        }
        
        foreach (var nodeType in nodeTypes)
        {
            var nodeValidation = ValidateNodeType(nodeType);
            errors.AddRange(nodeValidation.Errors);
            warnings.AddRange(nodeValidation.Warnings);
        }
        
        return new PluginValidationResult { Errors = errors, Warnings = warnings };
    }

    /// <inheritdoc />
    public PluginValidationResult ValidateNodeType(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        
        var errors = new List<PluginValidationError>();
        var warnings = new List<PluginValidationWarning>();
        var context = nodeType.FullName ?? nodeType.Name;
        
        // Check if type implements INode
        if (!typeof(INode).IsAssignableFrom(nodeType))
        {
            errors.Add(new PluginValidationError(
                "NODE_NOT_INODE",
                "Type must implement INode interface")
            {
                Context = context
            });
            return new PluginValidationResult { Errors = errors, Warnings = warnings };
        }
        
        // Check if type is concrete (not abstract or interface)
        if (nodeType.IsAbstract || nodeType.IsInterface)
        {
            errors.Add(new PluginValidationError(
                "NODE_NOT_CONCRETE",
                "Node type must be a concrete class")
            {
                Context = context
            });
            return new PluginValidationResult { Errors = errors, Warnings = warnings };
        }
        
        // Check for parameterless constructor
        var constructor = nodeType.GetConstructor(Type.EmptyTypes);
        if (constructor is null)
        {
            errors.Add(new PluginValidationError(
                "NODE_NO_PARAMETERLESS_CONSTRUCTOR",
                "Node type must have a parameterless constructor")
            {
                Context = context
            });
            return new PluginValidationResult { Errors = errors, Warnings = warnings };
        }
        
        // Try to instantiate and validate instance
        INode? instance;
        try
        {
            instance = Activator.CreateInstance(nodeType) as INode;
        }
        catch (Exception ex)
        {
            errors.Add(new PluginValidationError(
                "NODE_INSTANTIATION_FAILED",
                $"Failed to create instance: {ex.Message}")
            {
                Context = context
            });
            return new PluginValidationResult { Errors = errors, Warnings = warnings };
        }
        
        if (instance is null)
        {
            errors.Add(new PluginValidationError(
                "NODE_INSTANTIATION_NULL",
                "Node instantiation returned null")
            {
                Context = context
            });
            return new PluginValidationResult { Errors = errors, Warnings = warnings };
        }
        
        // Validate node properties
        if (string.IsNullOrWhiteSpace(instance.Id))
        {
            warnings.Add(new PluginValidationWarning(
                "NODE_EMPTY_ID",
                "Node Id property returns empty value")
            {
                Context = context
            });
        }
        
        if (string.IsNullOrWhiteSpace(instance.Type))
        {
            errors.Add(new PluginValidationError(
                "NODE_EMPTY_TYPE",
                "Node Type property must return a non-empty value")
            {
                Context = context
            });
        }
        
        // Validate node definition attribute
        var definitionAttr = nodeType.GetCustomAttribute<NodeDefinitionAttribute>();
        if (definitionAttr is null)
        {
            warnings.Add(new PluginValidationWarning(
                "NODE_MISSING_DEFINITION",
                "Node should have a NodeDefinitionAttribute for proper metadata")
            {
                Context = context
            });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(definitionAttr.Name))
            {
                warnings.Add(new PluginValidationWarning(
                    "NODE_MISSING_NAME",
                    "Node definition should have a display name")
                {
                    Context = context
                });
            }
            
            if (string.IsNullOrWhiteSpace(definitionAttr.Description))
            {
                warnings.Add(new PluginValidationWarning(
                    "NODE_MISSING_DESCRIPTION",
                    "Node definition should have a description")
                {
                    Context = context
                });
            }
        }
        
        // Validate category is set
        if (!Enum.IsDefined(typeof(NodeCategory), instance.Category))
        {
            errors.Add(new PluginValidationError(
                "NODE_INVALID_CATEGORY",
                "Node Category must be a valid NodeCategory value")
            {
                Context = context
            });
        }
        
        return new PluginValidationResult { Errors = errors, Warnings = warnings };
    }

    private static List<Type> DiscoverNodeTypes(Assembly assembly)
    {
        var nodeTypes = new List<Type>();
        
        try
        {
            var types = assembly.GetTypes()
                .Where(t => typeof(INode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
            
            nodeTypes.AddRange(types);
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types couldn't be loaded, but we can still use the ones that did
            var loadedTypes = ex.Types
                .Where(t => t is not null && typeof(INode).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Cast<Type>();
            
            nodeTypes.AddRange(loadedTypes);
        }

        return nodeTypes;
    }
}
