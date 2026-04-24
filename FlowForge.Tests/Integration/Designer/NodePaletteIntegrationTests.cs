using Bunit;
using CsCheck;
using FlowForge.Core.Interfaces;
using FlowForge.Designer.Components;
using FlowForge.Designer.Services;
using FlowForge.Tests.Integration.Designer.Generators;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Tests.Integration.Designer;

/// <summary>
/// Integration tests for NodePalette component.
/// Tests node grouping by category, search filtering, and drag operations.
/// </summary>
public class NodePaletteIntegrationTests : BunitContext
{
    /// <summary>
    /// Helper method to expand all categories in the NodePalette.
    /// Uses a while loop to avoid stale element references by re-querying after each click.
    /// </summary>
    private static void ExpandAllCategories(IRenderedComponent<NodePalette> cut)
    {
        // Keep expanding until no more collapsed categories
        while (true)
        {
            // Find a collapsed category header (one with ▶ instead of ▼)
            var collapsedHeader = cut.FindAll(".category-header")
                .FirstOrDefault(h => !h.TextContent.Contains("▼"));
            
            if (collapsedHeader == null)
                break;
            
            // Use InvokeAsync to ensure atomic Find + Click
            cut.InvokeAsync(() =>
            {
                var header = cut.FindAll(".category-header")
                    .FirstOrDefault(h => !h.TextContent.Contains("▼"));
                header?.Click();
            }).GetAwaiter().GetResult();
        }
    }

    #region Category Grouping Tests (Task 10.1)

    /// <summary>
    /// Tests that when node definitions are set, nodes are grouped by category.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void WhenNodeDefinitionsSetThenNodesAreGroupedByCategory()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);

        // Act
        var cut = Render<NodePalette>();

        // Assert - Check that category headers are present
        Assert.Contains("Trigger", cut.Markup);
        Assert.Contains("Action", cut.Markup);
        Assert.Contains("Logic", cut.Markup);
    }

    /// <summary>
    /// Tests that each category contains the correct nodes when expanded.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void WhenNodeDefinitionsSetThenEachCategoryContainsCorrectNodes()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = new List<NodeDefinition>
        {
            TestFixtures.CreateTriggerDefinition(),
            TestFixtures.CreateHttpRequestDefinition(),
            TestFixtures.CreateIfDefinition()
        };
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);

        // Act
        var cut = Render<NodePalette>();
        
        // Expand all categories
        ExpandAllCategories(cut);

        // Assert - Verify specific nodes are in the markup
        Assert.Contains("Manual Trigger", cut.Markup);
        Assert.Contains("HTTP Request", cut.Markup);
        Assert.Contains("If Condition", cut.Markup);
    }

    /// <summary>
    /// Tests that categories are ordered correctly.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void WhenNodeDefinitionsSetThenCategoriesAreOrdered()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);

        // Act
        var cut = Render<NodePalette>();

        // Assert - Trigger should appear before Action in the markup
        var triggerIndex = cut.Markup.IndexOf("Trigger", StringComparison.Ordinal);
        var actionIndex = cut.Markup.IndexOf("Action", StringComparison.Ordinal);
        var logicIndex = cut.Markup.IndexOf("Logic", StringComparison.Ordinal);
        
        Assert.True(triggerIndex < actionIndex, "Trigger should appear before Action");
        Assert.True(actionIndex < logicIndex, "Action should appear before Logic");
    }

    #endregion

    #region Search Filtering Tests (Task 10.1)

    /// <summary>
    /// Tests that search filtering filters nodes by name.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void WhenSearchTermEnteredThenNodesAreFilteredByName()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();
        
        // Expand all categories first
        ExpandAllCategories(cut);

        // Act - Enter search term
        var searchInput = cut.Find("input[type='text']");
        searchInput.Input("HTTP");
        
        // Expand categories again after search (search may change visible categories)
        ExpandAllCategories(cut);

        // Assert - Only HTTP Request should be visible
        Assert.Contains("HTTP Request", cut.Markup);
        // Other nodes should not be visible (they don't contain "HTTP")
        Assert.DoesNotContain("Manual Trigger", cut.Markup);
        Assert.DoesNotContain("If Condition", cut.Markup);
    }

    /// <summary>
    /// Tests that search filtering is case-insensitive.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void WhenSearchTermEnteredThenFilteringIsCaseInsensitive()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();

        // Act - Enter lowercase search term
        var searchInput = cut.Find("input[type='text']");
        searchInput.Input("http");
        
        // Expand all categories after search
        ExpandAllCategories(cut);

        // Assert - HTTP Request should still be visible (search shows matching nodes)
        Assert.Contains("HTTP Request", cut.Markup);
    }

    /// <summary>
    /// Tests that search filtering filters nodes by description.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void WhenSearchTermEnteredThenNodesAreFilteredByDescription()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();

        // Act - Search by description content
        var searchInput = cut.Find("input[type='text']");
        searchInput.Input("external APIs");
        
        // Expand all categories after search
        ExpandAllCategories(cut);

        // Assert - HTTP Request should be visible (its description contains "external APIs")
        Assert.Contains("HTTP Request", cut.Markup);
    }

    /// <summary>
    /// Tests that clearing search shows all nodes again.
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void WhenSearchClearedThenAllNodesAreShown()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();
        
        // Expand all categories
        ExpandAllCategories(cut);

        // Act - Enter search term then clear it
        var searchInput = cut.Find("input[type='text']");
        searchInput.Input("HTTP");
        searchInput.Input("");
        
        // Expand all categories again
        ExpandAllCategories(cut);

        // Assert - All nodes in expanded categories should be visible again
        Assert.Contains("Manual Trigger", cut.Markup);
        Assert.Contains("HTTP Request", cut.Markup);
        Assert.Contains("If Condition", cut.Markup);
    }

    #endregion

    #region Drag Start/End Tests (Task 10.1)

    /// <summary>
    /// Tests that when a node drag starts, the WorkflowStateService is notified.
    /// Validates: Requirements 6.3
    /// </summary>
    [Fact]
    public void WhenNodeDragStartsThenStateServiceIsNotified()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();

        // Act - Trigger drag start on a node (Trigger category is expanded by default)
        var paletteNode = cut.Find(".palette-node");
        paletteNode.DragStart();

        // Assert - DraggedNodeType should be set
        Assert.NotNull(stateService.DraggedNodeType);
    }

    /// <summary>
    /// Tests that when a node drag ends, the WorkflowStateService clears the dragged node type.
    /// Validates: Requirements 6.4
    /// </summary>
    [Fact]
    public void WhenNodeDragEndsThenStateServiceClearsDraggedNodeType()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var definitions = TestFixtures.CreateCommonNodeDefinitions();
        stateService.SetNodeDefinitions(definitions);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();

        // Act - Trigger drag start then drag end
        var paletteNode = cut.Find(".palette-node");
        paletteNode.DragStart();
        Assert.NotNull(stateService.DraggedNodeType);
        
        paletteNode.DragEnd();

        // Assert - DraggedNodeType should be cleared
        Assert.Null(stateService.DraggedNodeType);
    }

    /// <summary>
    /// Tests that drag start sets the correct node type.
    /// Validates: Requirements 6.3
    /// </summary>
    [Fact]
    public void WhenNodeDragStartsThenCorrectNodeTypeIsSet()
    {
        // Arrange
        var stateService = new WorkflowStateService();
        var triggerDef = TestFixtures.CreateTriggerDefinition();
        stateService.SetNodeDefinitions([triggerDef]);
        
        Services.AddSingleton(stateService);
        var cut = Render<NodePalette>();

        // Act - Trigger drag start on the trigger node
        var paletteNode = cut.Find(".palette-node");
        paletteNode.DragStart();

        // Assert - DraggedNodeType should match the trigger definition type
        Assert.Equal(triggerDef.Type, stateService.DraggedNodeType);
    }

    #endregion

    #region Property Tests

    /// <summary>
    /// Feature: blazor-integration-tests, Property 12: NodePalette Category Grouping
    /// For any set of node definitions, the NodePalette SHALL render nodes grouped by their category.
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void Property12_NodePaletteCategoryGrouping()
    {
        // Generate lists of node definitions with various categories
        var nodeDefinitionsGen = DesignerGenerators.NodeDefinitionGen.List[1, 10];

        nodeDefinitionsGen.Sample(definitions =>
        {
            // Create a fresh BunitContext for each iteration
            using var ctx = new BunitContext();
            
            // Arrange
            var stateService = new WorkflowStateService();
            stateService.SetNodeDefinitions(definitions);
            
            ctx.Services.AddSingleton(stateService);

            // Act
            var cut = ctx.Render<NodePalette>();

            // Assert - For each category present in definitions, verify it appears in markup
            var categoriesInDefinitions = definitions
                .Select(d => d.Category)
                .Distinct()
                .ToList();

            foreach (var category in categoriesInDefinitions)
            {
                Assert.Contains(category.ToString(), cut.Markup);
            }

            // Expand all categories to verify nodes - use while loop to avoid stale references
            while (true)
            {
                var collapsedHeader = cut.FindAll(".category-header")
                    .FirstOrDefault(h => !h.TextContent.Contains("▼"));
                
                if (collapsedHeader == null)
                    break;
                
                cut.InvokeAsync(() =>
                {
                    var header = cut.FindAll(".category-header")
                        .FirstOrDefault(h => !h.TextContent.Contains("▼"));
                    header?.Click();
                }).GetAwaiter().GetResult();
            }

            // Assert - Each node name should appear in the markup (after expanding categories)
            foreach (var definition in definitions)
            {
                Assert.Contains(definition.Name, cut.Markup);
            }
        }, iter: 100);
    }

    /// <summary>
    /// Feature: blazor-integration-tests, Property 13: NodePalette Search Filtering
    /// For any search term, the NodePalette SHALL display only nodes whose name or description 
    /// contains the search term (case-insensitive).
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void Property13_NodePaletteSearchFiltering()
    {
        // Generate node definitions and search terms
        var testGen = from definitions in DesignerGenerators.NodeDefinitionGen.List[2, 8]
                      from searchTerm in DesignerGenerators.NonEmptyString(2, 10)
                      select (definitions, searchTerm);

        testGen.Sample(data =>
        {
            var (definitions, searchTerm) = data;
            
            // Create a fresh BunitContext for each iteration
            using var ctx = new BunitContext();
            
            // Arrange
            var stateService = new WorkflowStateService();
            stateService.SetNodeDefinitions(definitions);
            
            ctx.Services.AddSingleton(stateService);
            var cut = ctx.Render<NodePalette>();

            // Act - Enter search term
            var searchInput = cut.Find("input[type='text']");
            searchInput.Input(searchTerm);
            
            // Expand all categories after search - use while loop to avoid stale references
            while (true)
            {
                var collapsedHeader = cut.FindAll(".category-header")
                    .FirstOrDefault(h => !h.TextContent.Contains("▼"));
                
                if (collapsedHeader == null)
                    break;
                
                cut.InvokeAsync(() =>
                {
                    var header = cut.FindAll(".category-header")
                        .FirstOrDefault(h => !h.TextContent.Contains("▼"));
                    header?.Click();
                }).GetAwaiter().GetResult();
            }

            // Assert - Only nodes matching the search term should be visible
            var matchingNodes = definitions.Where(d =>
                d.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                d.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(d.SourcePackage) && d.SourcePackage.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

            var nonMatchingNodes = definitions.Where(d =>
                !d.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !d.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(d.SourcePackage) || !d.SourcePackage.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

            // Matching nodes should be in markup (if any match)
            foreach (var node in matchingNodes)
            {
                Assert.Contains(node.Name, cut.Markup);
            }

            // Non-matching nodes should not be in markup
            foreach (var node in nonMatchingNodes)
            {
                // Check that the node name doesn't appear in a node-name span
                Assert.DoesNotContain($"\"node-name\">\n                                    {node.Name}", cut.Markup);
            }
        }, iter: 100);
    }

    #endregion
}
