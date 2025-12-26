using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Designer.Services;
using System.Text.Json;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for node palette grouping.
/// Feature: designer-plugin-management, Property 10: Node Palette Grouping
/// </summary>
public class NodePaletteGroupingTests
{
    /// <summary>
    /// Feature: designer-plugin-management, Property 10: Node Palette Grouping
    /// For any set of installed packages with nodes, the Node Palette SHALL group nodes by their category,
    /// and plugin nodes SHALL be distinguishable from built-in nodes.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void NodePaletteGrouping_NodesGroupedByCategory_AllCategoriesRepresented()
    {
        GenNodeDefinitionSet.Sample(scenario =>
        {
            // Arrange
            var workflowStateService = new WorkflowStateService();
            workflowStateService.SetNodeDefinitions(scenario.NodeDefinitions);

            // Act
            var definitions = workflowStateService.NodeDefinitions;
            var groupedByCategory = definitions.GroupBy(n => n.Category).ToList();

            // Assert: All nodes should be grouped by category
            foreach (var group in groupedByCategory)
            {
                var nodesInGroup = group.ToList();

                // Property: All nodes in a group should have the same category
                Assert.True(nodesInGroup.All(n => n.Category == group.Key),
                    $"All nodes in category {group.Key} should have the same category");
            }

            // Property: Total count should match
            var totalGroupedCount = groupedByCategory.Sum(g => g.Count());
            Assert.Equal(scenario.NodeDefinitions.Count, totalGroupedCount);

            // Property: Each category in the original set should be represented
            var originalCategories = scenario.NodeDefinitions.Select(n => n.Category).Distinct().ToHashSet();
            var groupedCategories = groupedByCategory.Select(g => g.Key).ToHashSet();
            Assert.True(originalCategories.SetEquals(groupedCategories),
                "All original categories should be represented in grouped result");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 10: Node Palette Grouping
    /// Plugin nodes SHALL be distinguishable from built-in nodes.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void NodePaletteGrouping_PluginNodesDistinguishable_IsPluginNodePropertyCorrect()
    {
        GenMixedNodeDefinitionSet.Sample(scenario =>
        {
            // Arrange
            var workflowStateService = new WorkflowStateService();
            workflowStateService.SetNodeDefinitions(scenario.NodeDefinitions);

            // Act
            var definitions = workflowStateService.NodeDefinitions;
            var pluginNodes = definitions.Where(n => n.IsPluginNode).ToList();
            var builtInNodes = definitions.Where(n => !n.IsPluginNode).ToList();

            // Assert: Plugin nodes should have SourcePackage set
            foreach (var node in pluginNodes)
            {
                Assert.False(string.IsNullOrEmpty(node.SourcePackage),
                    $"Plugin node {node.Name} should have SourcePackage set");
            }

            // Assert: Built-in nodes should NOT have SourcePackage set
            foreach (var node in builtInNodes)
            {
                Assert.True(string.IsNullOrEmpty(node.SourcePackage),
                    $"Built-in node {node.Name} should not have SourcePackage set");
            }

            // Property: IsPluginNode should be consistent with SourcePackage
            foreach (var node in definitions)
            {
                var expectedIsPlugin = !string.IsNullOrEmpty(node.SourcePackage);
                Assert.Equal(expectedIsPlugin, node.IsPluginNode);
            }

            // Property: Plugin and built-in counts should match expected
            Assert.Equal(scenario.ExpectedPluginCount, pluginNodes.Count);
            Assert.Equal(scenario.ExpectedBuiltInCount, builtInNodes.Count);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 10: Node Palette Grouping
    /// Grouping should be consistent regardless of node order.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void NodePaletteGrouping_OrderIndependent_SameGroupingRegardlessOfOrder()
    {
        GenNodeDefinitionSet.Sample(scenario =>
        {
            // Arrange
            var workflowStateService1 = new WorkflowStateService();
            var workflowStateService2 = new WorkflowStateService();

            // Set definitions in original order
            workflowStateService1.SetNodeDefinitions(scenario.NodeDefinitions);

            // Set definitions in reversed order
            var reversedDefinitions = scenario.NodeDefinitions.AsEnumerable().Reverse().ToList();
            workflowStateService2.SetNodeDefinitions(reversedDefinitions);

            // Act
            var grouped1 = workflowStateService1.NodeDefinitions
                .GroupBy(n => n.Category)
                .OrderBy(g => g.Key)
                .Select(g => new { Category = g.Key, Types = g.Select(n => n.Type).OrderBy(t => t).ToList() })
                .ToList();

            var grouped2 = workflowStateService2.NodeDefinitions
                .GroupBy(n => n.Category)
                .OrderBy(g => g.Key)
                .Select(g => new { Category = g.Key, Types = g.Select(n => n.Type).OrderBy(t => t).ToList() })
                .ToList();

            // Assert: Same number of categories
            Assert.Equal(grouped1.Count, grouped2.Count);

            // Assert: Same categories with same nodes
            for (int i = 0; i < grouped1.Count; i++)
            {
                Assert.Equal(grouped1[i].Category, grouped2[i].Category);
                Assert.Equal(grouped1[i].Types, grouped2[i].Types);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: designer-plugin-management, Property 10: Node Palette Grouping
    /// Plugin nodes from the same package should be identifiable.
    /// Validates: Requirements 9.4
    /// </summary>
    [Fact]
    public void NodePaletteGrouping_PluginNodesFromSamePackage_Identifiable()
    {
        GenMultiPackageNodeSet.Sample(scenario =>
        {
            // Arrange
            var workflowStateService = new WorkflowStateService();
            workflowStateService.SetNodeDefinitions(scenario.NodeDefinitions);

            // Act
            var definitions = workflowStateService.NodeDefinitions;
            var nodesByPackage = definitions
                .Where(n => n.IsPluginNode)
                .GroupBy(n => n.SourcePackage)
                .ToDictionary(g => g.Key!, g => g.ToList());

            // Assert: Each package should have the expected number of nodes
            foreach (var (packageId, expectedCount) in scenario.ExpectedNodesPerPackage)
            {
                if (nodesByPackage.TryGetValue(packageId, out var nodes))
                {
                    Assert.Equal(expectedCount, nodes.Count);

                    // All nodes from the same package should have the same SourcePackage
                    Assert.True(nodes.All(n => n.SourcePackage == packageId),
                        $"All nodes from package {packageId} should have matching SourcePackage");
                }
                else
                {
                    Assert.Equal(0, expectedCount);
                }
            }
        }, iter: 100);
    }

    #region Test Scenarios

    private record NodeDefinitionSetScenario
    {
        public required List<NodeDefinition> NodeDefinitions { get; init; }
    }

    private record MixedNodeDefinitionSetScenario
    {
        public required List<NodeDefinition> NodeDefinitions { get; init; }
        public required int ExpectedPluginCount { get; init; }
        public required int ExpectedBuiltInCount { get; init; }
    }

    private record MultiPackageNodeSetScenario
    {
        public required List<NodeDefinition> NodeDefinitions { get; init; }
        public required Dictionary<string, int> ExpectedNodesPerPackage { get; init; }
    }

    #endregion

    #region Generators

    private static readonly Gen<string> GenPackageId =
        Gen.Char['a', 'z'].Array[5, 10].Select(chars => $"FlowForge.Plugin.{new string(chars)}");

    private static readonly Gen<string> GenNodeType =
        Gen.Char['a', 'z'].Array[5, 15].Select(chars => new string(chars));

    private static readonly Gen<string> GenNodeName =
        Gen.Char['A', 'Z'].Array[1, 1].Select(c => new string(c))
            .SelectMany(first => Gen.Char['a', 'z'].Array[4, 14].Select(rest => first + new string(rest)));

    private static readonly Gen<NodeCategory> GenCategory =
        Gen.Int[0, 3].Select(i => (NodeCategory)i);

    private static NodeDefinition CreateNodeDefinition(string type, string name, NodeCategory category,
        string? sourcePackage)
    {
        return new NodeDefinition
        {
            Type = type,
            Name = name,
            Description = $"Description for {name}",
            Category = category,
            Icon = "📦",
            SourcePackage = sourcePackage,
            ConfigurationSchema = JsonDocument.Parse("{}").RootElement
        };
    }

    private static readonly Gen<NodeDefinition> GenBuiltInNodeDefinition =
        from type in GenNodeType
        from name in GenNodeName
        from category in GenCategory
        select CreateNodeDefinition(type, name, category, null);

    private static readonly Gen<NodeDefinition> GenPluginNodeDefinition =
        from type in GenNodeType
        from name in GenNodeName
        from category in GenCategory
        from sourcePackage in GenPackageId
        select CreateNodeDefinition(type, name, category, sourcePackage);

    private static readonly Gen<NodeDefinitionSetScenario> GenNodeDefinitionSet =
        from builtInCount in Gen.Int[1, 5]
        from pluginCount in Gen.Int[1, 5]
        from builtInNodes in GenBuiltInNodeDefinition.List[builtInCount, builtInCount]
        from pluginNodes in GenPluginNodeDefinition.List[pluginCount, pluginCount]
        select new NodeDefinitionSetScenario
        {
            NodeDefinitions = builtInNodes.Concat(pluginNodes).ToList()
        };

    private static readonly Gen<MixedNodeDefinitionSetScenario> GenMixedNodeDefinitionSet =
        from builtInCount in Gen.Int[1, 5]
        from pluginCount in Gen.Int[1, 5]
        from builtInNodes in GenBuiltInNodeDefinition.List[builtInCount, builtInCount]
        from pluginNodes in GenPluginNodeDefinition.List[pluginCount, pluginCount]
        select new MixedNodeDefinitionSetScenario
        {
            NodeDefinitions = builtInNodes.Concat(pluginNodes).ToList(),
            ExpectedPluginCount = pluginCount,
            ExpectedBuiltInCount = builtInCount
        };

    private static readonly Gen<MultiPackageNodeSetScenario> GenMultiPackageNodeSet =
        from packageCount in Gen.Int[1, 3]
        from packageIds in GenPackageId.List[packageCount, packageCount]
        from nodesPerPackage in Gen.Int[1, 3].List[packageCount, packageCount]
        select CreateMultiPackageScenario(packageIds, nodesPerPackage);

    private static MultiPackageNodeSetScenario CreateMultiPackageScenario(
        List<string> packageIds,
        List<int> nodesPerPackage)
    {
        var nodes = new List<NodeDefinition>();
        var expectedCounts = new Dictionary<string, int>();
        var random = new Random();

        for (int i = 0; i < packageIds.Count; i++)
        {
            var packageId = packageIds[i];
            var nodeCount = nodesPerPackage[i];
            expectedCounts[packageId] = nodeCount;

            for (int j = 0; j < nodeCount; j++)
            {
                var type = $"node_{packageId}_{j}_{random.Next(1000)}";
                var name = $"Node{j}From{packageId.Split('.').Last()}";
                var category = (NodeCategory)(j % 4);
                nodes.Add(CreateNodeDefinition(type, name, category, packageId));
            }
        }

        return new MultiPackageNodeSetScenario
        {
            NodeDefinitions = nodes,
            ExpectedNodesPerPackage = expectedCounts
        };
    }

    #endregion
}
