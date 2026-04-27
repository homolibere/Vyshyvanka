using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Designer.Services;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for modal opening with correct node context.
/// </summary>
public class ModalOpensWithCorrectNodeTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 1: Modal Opens with Correct Node Context
    /// For any node on the canvas, when the user double-clicks it, the Node_Editor_Modal
    /// should open and display that node's name and type in the header.
    /// Validates: Requirements 1.1, 1.2
    /// </summary>
    [Fact]
    public void ModalOpensWithCorrectNodeContext()
    {
        GenNodeOnCanvas.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.Definition]);
            
            var node = new WorkflowNode
            {
                Id = testCase.NodeId,
                Type = testCase.Definition.Type,
                Name = testCase.NodeName,
                Position = new Position(testCase.PositionX, testCase.PositionY),
                Configuration = testCase.Configuration
            };
            service.AddNode(node);

            // Act: Simulate double-click by retrieving node for modal
            var retrievedNode = service.GetNode(testCase.NodeId);
            var retrievedDefinition = service.GetNodeDefinition(node.Type);

            // Assert: Node should be found with correct properties
            Assert.NotNull(retrievedNode);
            Assert.Equal(testCase.NodeId, retrievedNode.Id);
            Assert.Equal(testCase.NodeName, retrievedNode.Name);
            Assert.Equal(testCase.Definition.Type, retrievedNode.Type);

            // Assert: Definition should be found for displaying type name
            Assert.NotNull(retrievedDefinition);
            Assert.Equal(testCase.Definition.Name, retrievedDefinition.Name);
            Assert.Equal(testCase.Definition.Type, retrievedDefinition.Type);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 1: Modal Displays Node Name and Type
    /// For any node, the modal should be able to display the node's name and the
    /// definition's display name (type name).
    /// Validates: Requirements 1.1, 1.2
    /// </summary>
    [Fact]
    public void ModalCanDisplayNodeNameAndType()
    {
        GenNodeWithDefinition.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions([testCase.Definition]);
            
            var node = new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = testCase.Definition.Type,
                Name = testCase.NodeName,
                Position = new Position(100, 100)
            };
            service.AddNode(node);

            // Act: Get node and definition for modal header
            var retrievedNode = service.GetNode(node.Id);
            var definition = service.GetNodeDefinition(node.Type);

            // Assert: Modal header should display node name
            Assert.NotNull(retrievedNode);
            Assert.False(string.IsNullOrEmpty(retrievedNode.Name), "Node name should not be empty");
            Assert.Equal(testCase.NodeName, retrievedNode.Name);

            // Assert: Modal header should display node type (from definition)
            Assert.NotNull(definition);
            Assert.False(string.IsNullOrEmpty(definition.Name), "Definition name should not be empty");
            Assert.Equal(testCase.Definition.Name, definition.Name);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 1: Multiple Nodes Have Distinct Contexts
    /// For any set of nodes on the canvas, each node should have a distinct context
    /// that can be correctly identified when opening the modal.
    /// Validates: Requirements 1.1, 1.2
    /// </summary>
    [Fact]
    public void MultipleNodesHaveDistinctContexts()
    {
        GenMultipleNodesOnCanvas.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(testCase.Definitions);
            
            foreach (var node in testCase.Nodes)
            {
                service.AddNode(node);
            }

            // Act & Assert: Each node should be retrievable with correct context
            foreach (var expectedNode in testCase.Nodes)
            {
                var retrievedNode = service.GetNode(expectedNode.Id);
                var definition = service.GetNodeDefinition(expectedNode.Type);

                Assert.NotNull(retrievedNode);
                Assert.Equal(expectedNode.Id, retrievedNode.Id);
                Assert.Equal(expectedNode.Name, retrievedNode.Name);
                Assert.Equal(expectedNode.Type, retrievedNode.Type);

                Assert.NotNull(definition);
            }

            // Assert: All node IDs are unique
            var nodeIds = testCase.Nodes.Select(n => n.Id).ToList();
            Assert.Equal(nodeIds.Count, nodeIds.Distinct().Count());
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 1: Node Selection Updates Context
    /// For any sequence of node selections, the context should always reflect
    /// the most recently selected node.
    /// Validates: Requirements 1.1, 1.2
    /// </summary>
    [Fact]
    public void NodeSelectionUpdatesContext()
    {
        GenNodeSelectionSequence.Sample(testCase =>
        {
            // Arrange
            var service = new WorkflowStateService();
            service.SetNodeDefinitions(testCase.Definitions);
            
            foreach (var node in testCase.Nodes)
            {
                service.AddNode(node);
            }

            // Act & Assert: Simulate selecting each node in sequence
            foreach (var nodeId in testCase.SelectionSequence)
            {
                service.SelectNode(nodeId);
                
                // Verify the selected node is correct
                Assert.Equal(nodeId, service.SelectedNodeId);
                
                // Verify we can retrieve the correct node for the modal
                var selectedNode = service.GetNode(nodeId);
                Assert.NotNull(selectedNode);
                Assert.Equal(nodeId, selectedNode.Id);
            }
        }, iter: 100);
    }

    #region Generators

    private record NodeOnCanvasTestCase(
        NodeDefinition Definition,
        string NodeId,
        string NodeName,
        double PositionX,
        double PositionY,
        JsonElement Configuration);

    private record NodeWithDefinitionTestCase(
        NodeDefinition Definition,
        string NodeName);

    private record MultipleNodesTestCase(
        List<NodeDefinition> Definitions,
        List<WorkflowNode> Nodes);

    private record NodeSelectionSequenceTestCase(
        List<NodeDefinition> Definitions,
        List<WorkflowNode> Nodes,
        List<string> SelectionSequence);

    private static readonly Gen<string> GenNodeName =
        Gen.Char['A', 'Z'].SelectMany(first =>
            Gen.Char['a', 'z'].Array[3, 12].Select(rest =>
                first + new string(rest)));

    private static readonly Gen<string> GenNodeType =
        Gen.Char['a', 'z'].Array[3, 10].Select(chars => new string(chars) + "-node");

    private static readonly Gen<NodeCategory> GenCategory =
        Gen.OneOf(
            Gen.Const(NodeCategory.Trigger),
            Gen.Const(NodeCategory.Action),
            Gen.Const(NodeCategory.Logic),
            Gen.Const(NodeCategory.Transform));

    private static readonly Gen<NodeDefinition> GenNodeDefinition =
        from type in GenNodeType
        from name in GenNodeName
        from category in GenCategory
        select new NodeDefinition
        {
            Type = type,
            Name = name,
            Description = $"Description for {name}",
            Category = category,
            Inputs = category == NodeCategory.Trigger
                ? []
                : [new PortDefinition { Name = "input", DisplayName = "Input", Type = PortType.Any }],
            Outputs = [new PortDefinition { Name = "output", DisplayName = "Output", Type = PortType.Any }]
        };

    private static readonly Gen<JsonElement> GenSimpleConfig =
        from seed in Gen.Int[0, 10000]
        select BuildSimpleConfig(seed);

    private static JsonElement BuildSimpleConfig(int seed)
    {
        var random = new Random(seed);
        var config = new Dictionary<string, object?>
        {
            ["setting1"] = $"value_{random.Next(1000)}",
            ["setting2"] = random.Next(100)
        };
        return JsonSerializer.SerializeToElement(config);
    }

    private static readonly Gen<NodeOnCanvasTestCase> GenNodeOnCanvas =
        from definition in GenNodeDefinition
        from nodeName in GenNodeName
        from posX in Gen.Double[0, 1000]
        from posY in Gen.Double[0, 800]
        from config in GenSimpleConfig
        select new NodeOnCanvasTestCase(
            definition,
            Guid.NewGuid().ToString(),
            nodeName,
            posX,
            posY,
            config);

    private static readonly Gen<NodeWithDefinitionTestCase> GenNodeWithDefinition =
        from definition in GenNodeDefinition
        from nodeName in GenNodeName
        select new NodeWithDefinitionTestCase(definition, nodeName);

    private static readonly Gen<MultipleNodesTestCase> GenMultipleNodesOnCanvas =
        from nodeCount in Gen.Int[2, 5]
        from definitions in GenNodeDefinition.List[nodeCount, nodeCount]
        from nodeNames in GenNodeName.List[nodeCount, nodeCount]
        from positions in (
            from x in Gen.Double[0, 1000]
            from y in Gen.Double[0, 800]
            select (x, y)
        ).List[nodeCount, nodeCount]
        select BuildMultipleNodesTestCase(definitions, nodeNames, positions);

    private static MultipleNodesTestCase BuildMultipleNodesTestCase(
        List<NodeDefinition> definitions,
        List<string> nodeNames,
        List<(double x, double y)> positions)
    {
        // Ensure unique types for definitions
        var uniqueDefinitions = definitions
            .GroupBy(d => d.Type)
            .Select(g => g.First())
            .ToList();

        var nodes = new List<WorkflowNode>();
        for (int i = 0; i < Math.Min(uniqueDefinitions.Count, nodeNames.Count); i++)
        {
            nodes.Add(new WorkflowNode
            {
                Id = Guid.NewGuid().ToString(),
                Type = uniqueDefinitions[i].Type,
                Name = nodeNames[i],
                Position = new Position(positions[i].x, positions[i].y)
            });
        }

        return new MultipleNodesTestCase(uniqueDefinitions, nodes);
    }

    private static readonly Gen<NodeSelectionSequenceTestCase> GenNodeSelectionSequence =
        from multipleNodes in GenMultipleNodesOnCanvas
        from selectionCount in Gen.Int[2, 6]
        from selections in Gen.Int[0, Math.Max(0, multipleNodes.Nodes.Count - 1)].List[selectionCount, selectionCount]
        select new NodeSelectionSequenceTestCase(
            multipleNodes.Definitions,
            multipleNodes.Nodes,
            selections.Where(i => i < multipleNodes.Nodes.Count).Select(i => multipleNodes.Nodes[i].Id).ToList());

    #endregion
}
