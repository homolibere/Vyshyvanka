using System.Text.Json;
using CsCheck;
using FlowForge.Core.Enums;
using FlowForge.Core.Interfaces;
using FlowForge.Core.Models;
using FlowForge.Engine.Execution;
using FlowForge.Engine.Expressions;
using FlowForge.Engine.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkflowExecutionContext = FlowForge.Engine.Execution.ExecutionContext;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for execution state persistence.
/// Feature: flowforge, Property 9: Execution State Persistence
/// </summary>
public class ExecutionPersistenceTests : IDisposable
{
    private readonly FlowForgeDbContext _dbContext;
    private readonly ExecutionRepository _repository;

    public ExecutionPersistenceTests()
    {
        var options = new DbContextOptionsBuilder<FlowForgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new FlowForgeDbContext(options);
        _dbContext.Database.EnsureCreated();
        _repository = new ExecutionRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Feature: flowforge, Property 9: Execution State Persistence
    /// For any workflow execution (successful or failed), the System SHALL persist a complete 
    /// execution record containing start time, end time, status, trigger information, and for 
    /// each node: input data, output data, and any error details.
    /// Validates: Requirements 3.6, 7.1, 7.4
    /// </summary>
    [Fact]
    public void SuccessfulExecution_PersistsCompleteExecutionRecord()
    {
        GenSuccessfulWorkflow.Sample(workflow =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            var nodeRegistry = new TestNodeRegistry(shouldFail: false);
            var expressionEvaluator = new ExpressionEvaluator();
            var innerEngine = new WorkflowEngine(nodeRegistry, expressionEvaluator);
            var persistentEngine = new PersistentWorkflowEngine(innerEngine, repository);
            
            var executionId = Guid.NewGuid();
            var context = new WorkflowExecutionContext(
                executionId,
                workflow.Id,
                new NullCredentialProvider());
            
            // Act
            var result = persistentEngine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();
            
            // Assert - Execution succeeded
            Assert.True(result.Success, $"Workflow execution failed: {result.ErrorMessage}");
            
            // Assert - Execution record was persisted
            var persistedExecution = repository.GetByIdAsync(executionId).GetAwaiter().GetResult();
            Assert.NotNull(persistedExecution);
            
            // Assert - Required fields are present
            AssertExecutionRecordComplete(persistedExecution, workflow, isSuccess: true);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 9: Execution State Persistence
    /// For any failed workflow execution, the System SHALL capture error details including 
    /// error message and node identifier.
    /// Validates: Requirements 3.6, 7.1, 7.4
    /// </summary>
    [Fact]
    public void FailedExecution_PersistsErrorDetails()
    {
        GenWorkflowWithFailingNode.Sample(workflow =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            var nodeRegistry = new TestNodeRegistry(shouldFail: true);
            var expressionEvaluator = new ExpressionEvaluator();
            var innerEngine = new WorkflowEngine(nodeRegistry, expressionEvaluator);
            var persistentEngine = new PersistentWorkflowEngine(innerEngine, repository);
            
            var executionId = Guid.NewGuid();
            var context = new WorkflowExecutionContext(
                executionId,
                workflow.Id,
                new NullCredentialProvider());
            
            // Act
            var result = persistentEngine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();
            
            // Assert - Execution failed
            Assert.False(result.Success);
            
            // Assert - Execution record was persisted
            var persistedExecution = repository.GetByIdAsync(executionId).GetAwaiter().GetResult();
            Assert.NotNull(persistedExecution);
            
            // Assert - Error details are captured
            AssertExecutionRecordComplete(persistedExecution, workflow, isSuccess: false);
            Assert.NotNull(persistedExecution.ErrorMessage);
            Assert.False(string.IsNullOrWhiteSpace(persistedExecution.ErrorMessage));
            
        }, iter: 100);
    }


    /// <summary>
    /// Feature: flowforge, Property 9: Execution State Persistence
    /// For any workflow execution, node execution records SHALL contain input data, 
    /// output data, and timing information.
    /// Validates: Requirements 3.6, 7.1, 7.4
    /// </summary>
    [Fact]
    public void Execution_PersistsNodeExecutionDetails()
    {
        GenWorkflowWithMultipleNodes.Sample(workflow =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            var nodeRegistry = new TestNodeRegistry(shouldFail: false);
            var expressionEvaluator = new ExpressionEvaluator();
            var innerEngine = new WorkflowEngine(nodeRegistry, expressionEvaluator);
            var persistentEngine = new PersistentWorkflowEngine(innerEngine, repository);
            
            var executionId = Guid.NewGuid();
            var context = new WorkflowExecutionContext(
                executionId,
                workflow.Id,
                new NullCredentialProvider());
            
            // Act
            var result = persistentEngine.ExecuteAsync(workflow, context).GetAwaiter().GetResult();
            
            // Assert - Execution record was persisted
            var persistedExecution = repository.GetByIdAsync(executionId).GetAwaiter().GetResult();
            Assert.NotNull(persistedExecution);
            
            // Assert - Node executions are persisted
            Assert.Equal(workflow.Nodes.Count, persistedExecution.NodeExecutions.Count);
            
            // Assert - Each node execution has required fields
            foreach (var nodeExecution in persistedExecution.NodeExecutions)
            {
                Assert.False(string.IsNullOrWhiteSpace(nodeExecution.NodeId));
                Assert.True(nodeExecution.StartedAt > DateTime.MinValue);
                Assert.NotNull(nodeExecution.CompletedAt);
                Assert.True(nodeExecution.CompletedAt >= nodeExecution.StartedAt);
                
                // Successful nodes should have output data
                if (nodeExecution.Status == ExecutionStatus.Completed)
                {
                    Assert.NotNull(nodeExecution.OutputData);
                }
            }
            
        }, iter: 100);
    }

    #region Helper Methods

    private static FlowForgeDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<FlowForgeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        var dbContext = new FlowForgeDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static void AssertExecutionRecordComplete(
        Execution execution, 
        Workflow workflow, 
        bool isSuccess)
    {
        // Verify execution ID is set
        Assert.NotEqual(Guid.Empty, execution.Id);
        
        // Verify workflow reference
        Assert.Equal(workflow.Id, execution.WorkflowId);
        Assert.Equal(workflow.Version, execution.WorkflowVersion);
        
        // Verify timing information
        Assert.True(execution.StartedAt > DateTime.MinValue, "StartedAt should be set");
        Assert.NotNull(execution.CompletedAt);
        Assert.True(execution.CompletedAt >= execution.StartedAt, 
            "CompletedAt should be >= StartedAt");
        
        // Verify status
        if (isSuccess)
        {
            Assert.Equal(ExecutionStatus.Completed, execution.Status);
        }
        else
        {
            Assert.Equal(ExecutionStatus.Failed, execution.Status);
        }
        
        // Verify mode is set
        Assert.True(Enum.IsDefined(typeof(ExecutionMode), execution.Mode));
    }

    #endregion

    #region Test Infrastructure

    /// <summary>
    /// A test node registry that creates test nodes.
    /// </summary>
    private sealed class TestNodeRegistry : INodeRegistry
    {
        private readonly bool _shouldFail;

        public TestNodeRegistry(bool shouldFail)
        {
            _shouldFail = shouldFail;
        }

        public void Register<TNode>() where TNode : INode { }
        
        public void RegisterFromAssembly(System.Reflection.Assembly assembly) { }
        
        public INode CreateNode(string nodeType, JsonElement configuration)
        {
            string? workflowNodeId = null;
            if (configuration.ValueKind == JsonValueKind.Object &&
                configuration.TryGetProperty("workflowNodeId", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String)
            {
                workflowNodeId = idElement.GetString();
            }
            return new TestNode(workflowNodeId ?? Guid.NewGuid().ToString(), _shouldFail);
        }
        
        public NodeDefinition? GetDefinition(string nodeType) => null;
        
        public IEnumerable<NodeDefinition> GetAllDefinitions() => [];
        
        public bool IsRegistered(string nodeType) => true;
    }

    /// <summary>
    /// A test node that can succeed or fail based on configuration.
    /// </summary>
    private sealed class TestNode : INode
    {
        private readonly string _workflowNodeId;
        private readonly bool _shouldFail;

        public TestNode(string workflowNodeId, bool shouldFail)
        {
            _workflowNodeId = workflowNodeId;
            _shouldFail = shouldFail;
        }

        public string Id => _workflowNodeId;
        public string Type => "test-node";
        public NodeCategory Category => NodeCategory.Action;

        public Task<NodeOutput> ExecuteAsync(NodeInput input, IExecutionContext context)
        {
            if (_shouldFail)
            {
                return Task.FromResult(new NodeOutput
                {
                    Data = default,
                    Success = false,
                    ErrorMessage = $"Test node '{_workflowNodeId}' failed intentionally"
                });
            }

            var outputData = new
            {
                nodeId = _workflowNodeId,
                timestamp = DateTime.UtcNow.Ticks,
                result = "success"
            };
            
            return Task.FromResult(new NodeOutput
            {
                Data = JsonSerializer.SerializeToElement(outputData),
                Success = true
            });
        }
    }

    /// <summary>
    /// A credential provider that returns no credentials (for testing).
    /// </summary>
    private sealed class NullCredentialProvider : ICredentialProvider
    {
        public Task<IDictionary<string, string>?> GetCredentialAsync(
            Guid credentialId, 
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IDictionary<string, string>?>(null);
        }
    }

    #endregion


    /// <summary>
    /// Feature: flowforge, Property 19: Execution History Filtering
    /// For any combination of filter criteria (workflow ID, status, date range), 
    /// the execution history query SHALL return exactly the executions matching 
    /// all specified criteria and no others.
    /// Validates: Requirements 7.6
    /// </summary>
    [Fact]
    public void QueryAsync_ReturnsExactlyMatchingExecutions()
    {
        GenExecutionFilteringScenario.Sample(scenario =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            // Insert all executions
            foreach (var execution in scenario.Executions)
            {
                repository.CreateAsync(execution).GetAwaiter().GetResult();
            }
            
            // Act - Query with the filter criteria
            var results = repository.QueryAsync(scenario.Query).GetAwaiter().GetResult();
            
            // Assert - All returned executions match ALL specified criteria
            foreach (var result in results)
            {
                AssertExecutionMatchesQuery(result, scenario.Query);
            }
            
            // Assert - No matching executions are missing (within pagination limits)
            var expectedMatches = scenario.Executions
                .Where(e => ExecutionMatchesQuery(e, scenario.Query))
                .OrderByDescending(e => e.StartedAt)
                .Skip(scenario.Query.Skip)
                .Take(scenario.Query.Take)
                .ToList();
            
            Assert.Equal(expectedMatches.Count, results.Count);
            
            // Verify the same execution IDs are returned
            var resultIds = results.Select(r => r.Id).ToHashSet();
            var expectedIds = expectedMatches.Select(e => e.Id).ToHashSet();
            Assert.True(resultIds.SetEquals(expectedIds), 
                $"Expected IDs: [{string.Join(", ", expectedIds)}], Got: [{string.Join(", ", resultIds)}]");
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 19: Execution History Filtering
    /// For any query with no matching executions, the result SHALL be empty.
    /// Validates: Requirements 7.6
    /// </summary>
    [Fact]
    public void QueryAsync_WithNoMatches_ReturnsEmptyList()
    {
        GenExecutionListWithNonMatchingQuery.Sample(scenario =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            // Insert all executions
            foreach (var execution in scenario.Executions)
            {
                repository.CreateAsync(execution).GetAwaiter().GetResult();
            }
            
            // Act - Query with criteria that won't match any execution
            var results = repository.QueryAsync(scenario.Query).GetAwaiter().GetResult();
            
            // Assert - No results returned
            Assert.Empty(results);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 19: Execution History Filtering
    /// For any query filtering by workflow ID, only executions for that workflow SHALL be returned.
    /// Validates: Requirements 7.6
    /// </summary>
    [Fact]
    public void QueryAsync_ByWorkflowId_ReturnsOnlyMatchingWorkflow()
    {
        GenMultiWorkflowExecutions.Sample(scenario =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            // Insert all executions
            foreach (var execution in scenario.Executions)
            {
                repository.CreateAsync(execution).GetAwaiter().GetResult();
            }
            
            // Act - Query by specific workflow ID
            var query = new ExecutionQuery
            {
                WorkflowId = scenario.TargetWorkflowId,
                Take = 100
            };
            var results = repository.QueryAsync(query).GetAwaiter().GetResult();
            
            // Assert - All results are for the target workflow
            Assert.All(results, r => Assert.Equal(scenario.TargetWorkflowId, r.WorkflowId));
            
            // Assert - Count matches expected
            var expectedCount = scenario.Executions.Count(e => e.WorkflowId == scenario.TargetWorkflowId);
            Assert.Equal(expectedCount, results.Count);
            
        }, iter: 100);
    }

    /// <summary>
    /// Feature: flowforge, Property 19: Execution History Filtering
    /// For any query filtering by date range, only executions within that range SHALL be returned.
    /// Validates: Requirements 7.6
    /// </summary>
    [Fact]
    public void QueryAsync_ByDateRange_ReturnsOnlyExecutionsInRange()
    {
        GenDateRangeFilterScenario.Sample(scenario =>
        {
            // Arrange - Create fresh database for each iteration
            using var dbContext = CreateFreshDbContext();
            var repository = new ExecutionRepository(dbContext);
            
            // Insert all executions
            foreach (var execution in scenario.Executions)
            {
                repository.CreateAsync(execution).GetAwaiter().GetResult();
            }
            
            // Act - Query by date range
            var query = new ExecutionQuery
            {
                StartDateFrom = scenario.RangeStart,
                StartDateTo = scenario.RangeEnd,
                Take = 100
            };
            var results = repository.QueryAsync(query).GetAwaiter().GetResult();
            
            // Assert - All results are within the date range
            Assert.All(results, r =>
            {
                Assert.True(r.StartedAt >= scenario.RangeStart, 
                    $"Execution started at {r.StartedAt} is before range start {scenario.RangeStart}");
                Assert.True(r.StartedAt <= scenario.RangeEnd, 
                    $"Execution started at {r.StartedAt} is after range end {scenario.RangeEnd}");
            });
            
            // Assert - Count matches expected
            var expectedCount = scenario.Executions.Count(e => 
                e.StartedAt >= scenario.RangeStart && e.StartedAt <= scenario.RangeEnd);
            Assert.Equal(expectedCount, results.Count);
            
        }, iter: 100);
    }

    private static void AssertExecutionMatchesQuery(Execution execution, ExecutionQuery query)
    {
        if (query.WorkflowId.HasValue)
        {
            Assert.Equal(query.WorkflowId.Value, execution.WorkflowId);
        }
        
        if (query.Status.HasValue)
        {
            Assert.Equal(query.Status.Value, execution.Status);
        }
        
        if (query.Mode.HasValue)
        {
            Assert.Equal(query.Mode.Value, execution.Mode);
        }
        
        if (query.StartDateFrom.HasValue)
        {
            Assert.True(execution.StartedAt >= query.StartDateFrom.Value,
                $"Execution started at {execution.StartedAt} is before filter start {query.StartDateFrom.Value}");
        }
        
        if (query.StartDateTo.HasValue)
        {
            Assert.True(execution.StartedAt <= query.StartDateTo.Value,
                $"Execution started at {execution.StartedAt} is after filter end {query.StartDateTo.Value}");
        }
    }

    private static bool ExecutionMatchesQuery(Execution execution, ExecutionQuery query)
    {
        if (query.WorkflowId.HasValue && execution.WorkflowId != query.WorkflowId.Value)
            return false;
        
        if (query.Status.HasValue && execution.Status != query.Status.Value)
            return false;
        
        if (query.Mode.HasValue && execution.Mode != query.Mode.Value)
            return false;
        
        if (query.StartDateFrom.HasValue && execution.StartedAt < query.StartDateFrom.Value)
            return false;
        
        if (query.StartDateTo.HasValue && execution.StartedAt > query.StartDateTo.Value)
            return false;
        
        return true;
    }

    #region Generators

    /// <summary>Generator for non-empty alphanumeric strings.</summary>
    private static Gen<string> GenNonEmptyString(int minLength, int maxLength) =>
        Gen.Char['a', 'z'].Array[minLength, maxLength].Select(chars => new string(chars));

    /// <summary>Generator for Position.</summary>
    private static readonly Gen<Position> GenPosition =
        from x in Gen.Double[-1000, 1000]
        from y in Gen.Double[-1000, 1000]
        select new Position(x, y);

    /// <summary>Generator for ErrorHandlingMode.</summary>
    private static readonly Gen<ErrorHandlingMode> GenErrorHandlingMode =
        Gen.OneOf(
            Gen.Const(ErrorHandlingMode.StopOnFirstError),
            Gen.Const(ErrorHandlingMode.ContinueOnError),
            Gen.Const(ErrorHandlingMode.RetryWithBackoff)
        );

    /// <summary>Generator for WorkflowSettings.</summary>
    private static readonly Gen<WorkflowSettings> GenWorkflowSettings =
        from maxRetries in Gen.Int[0, 5]
        from errorHandling in GenErrorHandlingMode
        select new WorkflowSettings
        {
            Timeout = TimeSpan.FromMinutes(5),
            MaxRetries = maxRetries,
            ErrorHandling = errorHandling
        };

    /// <summary>
    /// Generator for a successful workflow (single node, no connections).
    /// </summary>
    private static readonly Gen<Workflow> GenSuccessfulWorkflow =
        from id in Gen.Guid
        from name in GenNonEmptyString(1, 50)
        from version in Gen.Int[1, 100]
        from settings in GenWorkflowSettings
        from createdBy in Gen.Guid
        let nodeId = "test_node_1"
        let node = new WorkflowNode
        {
            Id = nodeId,
            Type = "test-node",
            Name = "Test Node",
            Position = new Position(0, 0),
            Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
        }
        select new Workflow
        {
            Id = id,
            Name = name,
            Description = "Test workflow for persistence",
            Version = version,
            IsActive = true,
            Nodes = [node],
            Connections = [],
            Settings = settings with { ErrorHandling = ErrorHandlingMode.StopOnFirstError },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

    /// <summary>
    /// Generator for a workflow with a failing node.
    /// </summary>
    private static readonly Gen<Workflow> GenWorkflowWithFailingNode =
        from id in Gen.Guid
        from name in GenNonEmptyString(1, 50)
        from version in Gen.Int[1, 100]
        from createdBy in Gen.Guid
        let nodeId = "failing_node"
        let node = new WorkflowNode
        {
            Id = nodeId,
            Type = "test-node",
            Name = "Failing Node",
            Position = new Position(0, 0),
            Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
        }
        select new Workflow
        {
            Id = id,
            Name = name,
            Description = "Test workflow with failing node",
            Version = version,
            IsActive = true,
            Nodes = [node],
            Connections = [],
            Settings = new WorkflowSettings
            {
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

    /// <summary>
    /// Generator for a workflow with multiple nodes.
    /// </summary>
    private static readonly Gen<Workflow> GenWorkflowWithMultipleNodes =
        from id in Gen.Guid
        from name in GenNonEmptyString(1, 50)
        from version in Gen.Int[1, 100]
        from nodeCount in Gen.Int[2, 5]
        from createdBy in Gen.Guid
        select CreateWorkflowWithNodes(id, name, version, nodeCount, createdBy);

    private static Workflow CreateWorkflowWithNodes(
        Guid id, 
        string name, 
        int version, 
        int nodeCount, 
        Guid createdBy)
    {
        var nodes = new List<WorkflowNode>();
        var connections = new List<Connection>();

        for (int i = 0; i < nodeCount; i++)
        {
            var nodeId = $"node_{i}";
            nodes.Add(new WorkflowNode
            {
                Id = nodeId,
                Type = "test-node",
                Name = $"Node {i}",
                Position = new Position(i * 100, 0),
                Configuration = JsonSerializer.SerializeToElement(new { workflowNodeId = nodeId })
            });
        }

        // Create linear connections
        for (int i = 0; i < nodeCount - 1; i++)
        {
            connections.Add(new Connection
            {
                SourceNodeId = $"node_{i}",
                SourcePort = "output",
                TargetNodeId = $"node_{i + 1}",
                TargetPort = "input"
            });
        }

        return new Workflow
        {
            Id = id,
            Name = name,
            Description = "Test workflow with multiple nodes",
            Version = version,
            IsActive = true,
            Nodes = nodes,
            Connections = connections,
            Settings = new WorkflowSettings
            {
                ErrorHandling = ErrorHandlingMode.StopOnFirstError
            },
            Tags = [],
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };
    }

    #region Property 19 Generators - Execution Filtering

    /// <summary>Generator for ExecutionStatus.</summary>
    private static readonly Gen<ExecutionStatus> GenExecutionStatus =
        Gen.OneOf(
            Gen.Const(ExecutionStatus.Pending),
            Gen.Const(ExecutionStatus.Running),
            Gen.Const(ExecutionStatus.Completed),
            Gen.Const(ExecutionStatus.Failed),
            Gen.Const(ExecutionStatus.Cancelled)
        );

    /// <summary>Generator for ExecutionMode.</summary>
    private static readonly Gen<ExecutionMode> GenExecutionMode =
        Gen.OneOf(
            Gen.Const(ExecutionMode.Manual),
            Gen.Const(ExecutionMode.Trigger),
            Gen.Const(ExecutionMode.Api),
            Gen.Const(ExecutionMode.Scheduled)
        );

    /// <summary>Generator for a base date within a reasonable range.</summary>
    private static readonly Gen<DateTime> GenBaseDate =
        Gen.Int[0, 365].Select(days => DateTime.UtcNow.AddDays(-days));

    /// <summary>Generator for a simple Execution record (without node executions).</summary>
    private static Gen<Execution> GenExecution(IReadOnlyList<Guid> workflowIds) =>
        from id in Gen.Guid
        from workflowIdIndex in Gen.Int[0, workflowIds.Count - 1]
        from version in Gen.Int[1, 10]
        from status in GenExecutionStatus
        from mode in GenExecutionMode
        from startedAt in GenBaseDate
        from durationMinutes in Gen.Int[1, 60]
        let workflowId = workflowIds[workflowIdIndex]
        let completedAt = status == ExecutionStatus.Running || status == ExecutionStatus.Pending 
            ? (DateTime?)null 
            : startedAt.AddMinutes(durationMinutes)
        select new Execution
        {
            Id = id,
            WorkflowId = workflowId,
            WorkflowVersion = version,
            Status = status,
            Mode = mode,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ErrorMessage = status == ExecutionStatus.Failed ? "Test error" : null,
            NodeExecutions = []
        };

    /// <summary>Generator for optional filter value.</summary>
    private static Gen<T?> GenOptional<T>(Gen<T> gen) where T : struct =>
        Gen.Frequency(
            (1, Gen.Const(default(T?))),
            (2, gen.Select(v => (T?)v))
        );

    /// <summary>Generator for ExecutionQuery with random filter criteria.</summary>
    private static Gen<ExecutionQuery> GenExecutionQuery(IReadOnlyList<Guid> workflowIds) =>
        from workflowIdIndex in GenOptional(Gen.Int[0, workflowIds.Count - 1])
        from status in GenOptional(GenExecutionStatus)
        from mode in GenOptional(GenExecutionMode)
        from hasDateRange in Gen.Bool
        from rangeStartDaysAgo in Gen.Int[30, 180]
        from rangeDurationDays in Gen.Int[7, 60]
        let workflowId = workflowIdIndex.HasValue ? (Guid?)workflowIds[workflowIdIndex.Value] : null
        let startDateFrom = hasDateRange ? (DateTime?)DateTime.UtcNow.AddDays(-rangeStartDaysAgo) : null
        let startDateTo = hasDateRange ? (DateTime?)DateTime.UtcNow.AddDays(-rangeStartDaysAgo + rangeDurationDays) : null
        select new ExecutionQuery
        {
            WorkflowId = workflowId,
            Status = status,
            Mode = mode,
            StartDateFrom = startDateFrom,
            StartDateTo = startDateTo,
            Skip = 0,
            Take = 100
        };

    /// <summary>Scenario for testing execution filtering.</summary>
    private record ExecutionFilteringScenario(
        List<Execution> Executions,
        ExecutionQuery Query);

    /// <summary>Generator for execution filtering test scenario.</summary>
    private static readonly Gen<ExecutionFilteringScenario> GenExecutionFilteringScenario =
        from workflowCount in Gen.Int[2, 5]
        let workflowIds = Enumerable.Range(0, workflowCount).Select(_ => Guid.NewGuid()).ToList()
        from executionCount in Gen.Int[5, 20]
        from executions in GenExecution(workflowIds).List[executionCount, executionCount]
        from query in GenExecutionQuery(workflowIds)
        select new ExecutionFilteringScenario(executions, query);

    /// <summary>Scenario for testing non-matching query.</summary>
    private record NonMatchingQueryScenario(
        List<Execution> Executions,
        ExecutionQuery Query);

    /// <summary>Generator for scenario where query won't match any executions.</summary>
    private static readonly Gen<ExecutionListWithNonMatchingQuery> GenExecutionListWithNonMatchingQuery =
        from executionCount in Gen.Int[3, 10]
        let workflowIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList()
        from executions in GenExecution(workflowIds).List[executionCount, executionCount]
        let nonExistentWorkflowId = Guid.NewGuid() // This ID won't exist in any execution
        select new ExecutionListWithNonMatchingQuery(
            executions,
            new ExecutionQuery
            {
                WorkflowId = nonExistentWorkflowId,
                Take = 100
            });

    private record ExecutionListWithNonMatchingQuery(
        List<Execution> Executions,
        ExecutionQuery Query);

    /// <summary>Scenario for testing multi-workflow filtering.</summary>
    private record MultiWorkflowScenario(
        List<Execution> Executions,
        Guid TargetWorkflowId);

    /// <summary>Generator for multi-workflow execution scenario.</summary>
    private static readonly Gen<MultiWorkflowScenario> GenMultiWorkflowExecutions =
        from workflowCount in Gen.Int[3, 5]
        let workflowIds = Enumerable.Range(0, workflowCount).Select(_ => Guid.NewGuid()).ToList()
        from executionCount in Gen.Int[10, 20]
        from executions in GenExecution(workflowIds).List[executionCount, executionCount]
        from targetIndex in Gen.Int[0, workflowCount - 1]
        select new MultiWorkflowScenario(executions, workflowIds[targetIndex]);

    /// <summary>Scenario for testing date range filtering.</summary>
    private record DateRangeFilterScenario(
        List<Execution> Executions,
        DateTime RangeStart,
        DateTime RangeEnd);

    /// <summary>Generator for date range filter scenario.</summary>
    private static readonly Gen<DateRangeFilterScenario> GenDateRangeFilterScenario =
        from executionCount in Gen.Int[10, 20]
        let workflowIds = new List<Guid> { Guid.NewGuid() }
        from executions in GenExecution(workflowIds).List[executionCount, executionCount]
        from rangeStartDaysAgo in Gen.Int[100, 200]
        from rangeDurationDays in Gen.Int[30, 100]
        let rangeStart = DateTime.UtcNow.AddDays(-rangeStartDaysAgo)
        let rangeEnd = DateTime.UtcNow.AddDays(-rangeStartDaysAgo + rangeDurationDays)
        select new DateRangeFilterScenario(executions, rangeStart, rangeEnd);

    #endregion

    #endregion
}
