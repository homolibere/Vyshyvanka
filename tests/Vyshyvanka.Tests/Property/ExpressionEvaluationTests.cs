using System.Text.Json;
using CsCheck;
using Vyshyvanka.Core.Interfaces;
using Vyshyvanka.Engine.Execution;
using Vyshyvanka.Engine.Expressions;
using WorkflowExecutionContext = Vyshyvanka.Engine.Execution.ExecutionContext;

namespace Vyshyvanka.Tests.Property;

/// <summary>
/// Property-based tests for expression evaluation.
/// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
/// </summary>
public class ExpressionEvaluationTests
{
    private readonly ExpressionEvaluator _evaluator = new();

    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any valid expression referencing node outputs with simple properties,
    /// the Expression_Evaluator SHALL return the correct value from the Execution_Context.
    /// Validates: Requirements 5.2, 5.4
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_NodeOutputSimpleProperty_ReturnsCorrectValue()
    {
        GenNodeOutputWithProperty.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            var nodeOutput = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                [testCase.PropertyName] = testCase.PropertyValue
            });
            context.NodeOutputs.Set(testCase.NodeId, nodeOutput);

            var expression = $"{{{{ nodes.{testCase.NodeId}.{testCase.PropertyName} }}}}";

            // Act
            var success = _evaluator.TryEvaluate(expression, context, out var result, out var error);

            // Assert
            Assert.True(success, $"Expression evaluation failed: {error}");
            AssertValueEquals(testCase.PropertyValue, result);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any valid expression referencing nested properties in node outputs,
    /// the Expression_Evaluator SHALL return the correct nested value.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_NestedProperties_ReturnsCorrectValue()
    {
        GenNestedPropertyAccess.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            context.NodeOutputs.Set(testCase.NodeId, testCase.NodeOutput);

            var expression = $"{{{{ nodes.{testCase.NodeId}.{testCase.PropertyPath} }}}}";

            // Act
            var success = _evaluator.TryEvaluate(expression, context, out var result, out var error);

            // Assert
            Assert.True(success, $"Expression evaluation failed for path '{testCase.PropertyPath}': {error}");
            AssertValueEquals(testCase.ExpectedValue, result);
        }, iter: 100);
    }


    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any valid expression referencing array elements in node outputs,
    /// the Expression_Evaluator SHALL return the correct array element.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_ArrayIndexing_ReturnsCorrectElement()
    {
        GenArrayIndexAccess.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            context.NodeOutputs.Set(testCase.NodeId, testCase.NodeOutput);

            var expression = $"{{{{ nodes.{testCase.NodeId}.items[{testCase.Index}] }}}}";

            // Act
            var success = _evaluator.TryEvaluate(expression, context, out var result, out var error);

            // Assert
            Assert.True(success, $"Expression evaluation failed for index {testCase.Index}: {error}");
            AssertValueEquals(testCase.ExpectedValue, result);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any valid expression referencing variables,
    /// the Expression_Evaluator SHALL return the correct variable value.
    /// Validates: Requirements 5.2, 5.4
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_Variables_ReturnsCorrectValue()
    {
        GenVariableAccess.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            context.Variables[testCase.VariableName] = testCase.VariableValue;

            var expression = $"{{{{ variables.{testCase.VariableName} }}}}";

            // Act
            var success = _evaluator.TryEvaluate(expression, context, out var result, out var error);

            // Assert
            Assert.True(success, $"Expression evaluation failed: {error}");
            AssertValueEquals(testCase.VariableValue, result);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any invalid expression (missing node, missing property, invalid path),
    /// the Expression_Evaluator SHALL produce descriptive error messages with location information.
    /// Validates: Requirements 5.5
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_InvalidExpression_ReturnsDescriptiveError()
    {
        GenInvalidExpression.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            
            // Set up some valid data so we can test specific invalid paths
            var nodeOutput = JsonSerializer.SerializeToElement(new { existingProp = "value" });
            context.NodeOutputs.Set("existingNode", nodeOutput);

            // Act
            var success = _evaluator.TryEvaluate(testCase.Expression, context, out _, out var error);

            // Assert
            Assert.False(success, $"Expected evaluation to fail for: {testCase.Expression}");
            Assert.NotNull(error);
            Assert.NotEmpty(error);
            
            // Error should contain context about what went wrong
            Assert.True(
                error.Contains(testCase.ExpectedErrorFragment, StringComparison.OrdinalIgnoreCase),
                $"Error '{error}' should contain '{testCase.ExpectedErrorFragment}'");
        }, iter: 100);
    }


    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any valid expression with string interpolation (multiple expressions in one string),
    /// the Expression_Evaluator SHALL correctly evaluate and concatenate all expressions.
    /// Validates: Requirements 5.2, 5.4
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_StringInterpolation_ConcatenatesCorrectly()
    {
        GenStringInterpolation.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            foreach (var (nodeId, output) in testCase.NodeOutputs)
            {
                context.NodeOutputs.Set(nodeId, output);
            }

            // Act
            var success = _evaluator.TryEvaluate(testCase.Expression, context, out var result, out var error);

            // Assert
            Assert.True(success, $"Expression evaluation failed: {error}");
            Assert.Equal(testCase.ExpectedResult, result?.ToString());
        }, iter: 100);
    }

    /// <summary>
    /// Feature: vyshyvanka, Property 12: Expression Evaluation Correctness
    /// For any valid expression with built-in transformation functions,
    /// the Expression_Evaluator SHALL correctly apply the transformation.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void ExpressionEvaluation_TransformationFunctions_AppliesCorrectly()
    {
        GenFunctionCall.Sample(testCase =>
        {
            // Arrange
            var context = CreateContext();
            var nodeOutput = JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["value"] = testCase.InputValue
            });
            context.NodeOutputs.Set("testNode", nodeOutput);

            var expression = $"{{{{ {testCase.FunctionName}(nodes.testNode.value) }}}}";

            // Act
            var success = _evaluator.TryEvaluate(expression, context, out var result, out var error);

            // Assert
            Assert.True(success, $"Function '{testCase.FunctionName}' evaluation failed: {error}");
            AssertValueEquals(testCase.ExpectedResult, result);
        }, iter: 100);
    }

    #region Helper Methods

    private static WorkflowExecutionContext CreateContext()
    {
        return new WorkflowExecutionContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new NullCredentialProvider());
    }

    private static void AssertValueEquals(object? expected, object? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        if (actual is JsonElement je)
        {
            actual = ConvertJsonElement(je);
        }

        // Handle numeric comparisons with tolerance
        if (expected is double expectedDouble && actual is double actualDouble)
        {
            Assert.True(Math.Abs(expectedDouble - actualDouble) < 0.0001,
                $"Expected {expectedDouble} but got {actualDouble}");
            return;
        }

        if (expected is int expectedInt && actual is int actualInt)
        {
            Assert.Equal(expectedInt, actualInt);
            return;
        }

        // Convert both to string for comparison if types differ
        Assert.Equal(expected.ToString(), actual?.ToString());
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element
        };
    }

    #endregion


    #region Test Infrastructure

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

    #region Test Case Records

    private record NodeOutputWithPropertyTestCase(
        string NodeId,
        string PropertyName,
        object PropertyValue);

    private record NestedPropertyAccessTestCase(
        string NodeId,
        JsonElement NodeOutput,
        string PropertyPath,
        object ExpectedValue);

    private record ArrayIndexAccessTestCase(
        string NodeId,
        JsonElement NodeOutput,
        int Index,
        object ExpectedValue);

    private record VariableAccessTestCase(
        string VariableName,
        object VariableValue);

    private record InvalidExpressionTestCase(
        string Expression,
        string ExpectedErrorFragment);

    private record StringInterpolationTestCase(
        string Expression,
        Dictionary<string, JsonElement> NodeOutputs,
        string ExpectedResult);

    private record FunctionCallTestCase(
        string FunctionName,
        object InputValue,
        object ExpectedResult);

    #endregion


    #region Generators

    /// <summary>Generator for valid node IDs (alphanumeric, no special chars).</summary>
    private static Gen<string> GenNodeId =>
        Gen.Char['a', 'z'].Array[3, 10].Select(chars => new string(chars));

    /// <summary>Generator for valid property names.</summary>
    private static Gen<string> GenPropertyName =>
        Gen.Char['a', 'z'].Array[3, 15].Select(chars => new string(chars));

    /// <summary>Generator for simple property values (string, int, bool).</summary>
    private static Gen<object> GenSimpleValue =>
        Gen.OneOf(
            Gen.String[1, 50].Select(s => (object)s),
            Gen.Int[-1000, 1000].Select(i => (object)i),
            Gen.Bool.Select(b => (object)b)
        );

    /// <summary>Generator for node output with simple property test cases.</summary>
    private static readonly Gen<NodeOutputWithPropertyTestCase> GenNodeOutputWithProperty =
        from nodeId in GenNodeId
        from propName in GenPropertyName
        from propValue in GenSimpleValue
        select new NodeOutputWithPropertyTestCase(nodeId, propName, propValue);

    /// <summary>Generator for nested property access test cases.</summary>
    private static readonly Gen<NestedPropertyAccessTestCase> GenNestedPropertyAccess =
        from nodeId in GenNodeId
        from level1 in GenPropertyName
        from level2 in GenPropertyName
        from value in GenSimpleValue
        let output = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            [level1] = new Dictionary<string, object>
            {
                [level2] = value
            }
        })
        select new NestedPropertyAccessTestCase(nodeId, output, $"{level1}.{level2}", value);

    /// <summary>Generator for array index access test cases.</summary>
    private static readonly Gen<ArrayIndexAccessTestCase> GenArrayIndexAccess =
        from nodeId in GenNodeId
        from arraySize in Gen.Int[1, 10]
        from index in Gen.Int[0, arraySize - 1]
        let items = Enumerable.Range(0, arraySize).Select(i => $"item_{i}").ToArray()
        let output = JsonSerializer.SerializeToElement(new { items })
        select new ArrayIndexAccessTestCase(nodeId, output, index, items[index]);

    /// <summary>Generator for variable access test cases.</summary>
    private static readonly Gen<VariableAccessTestCase> GenVariableAccess =
        from varName in GenPropertyName
        from varValue in GenSimpleValue
        select new VariableAccessTestCase(varName, varValue);

    /// <summary>Generator for invalid expression test cases.</summary>
    private static readonly Gen<InvalidExpressionTestCase> GenInvalidExpression =
        Gen.OneOf(
            // Missing node
            GenNodeId.Select(nodeId => new InvalidExpressionTestCase(
                $"{{{{ nodes.{nodeId}.prop }}}}",
                "no output")),
            // Missing property on existing node
            GenPropertyName.Select(prop => new InvalidExpressionTestCase(
                $"{{{{ nodes.existingNode.{prop} }}}}",
                "not found")),
            // Invalid root
            GenPropertyName.Select(name => new InvalidExpressionTestCase(
                $"{{{{ invalid.{name} }}}}",
                "Unknown expression root")),
            // Missing variable
            GenPropertyName.Select(name => new InvalidExpressionTestCase(
                $"{{{{ variables.{name} }}}}",
                "not found")),
            // Array index out of bounds
            Gen.Const(new InvalidExpressionTestCase(
                "{{ nodes.existingNode.items[999] }}",
                "not found"))
        );


    /// <summary>Generator for string interpolation test cases.</summary>
    private static readonly Gen<StringInterpolationTestCase> GenStringInterpolation =
        from node1Id in GenNodeId
        from node2Id in GenNodeId.Where(id => id != node1Id)
        from value1 in Gen.String[1, 20]
        from value2 in Gen.String[1, 20]
        let outputs = new Dictionary<string, JsonElement>
        {
            [node1Id] = JsonSerializer.SerializeToElement(new { text = value1 }),
            [node2Id] = JsonSerializer.SerializeToElement(new { text = value2 })
        }
        let expression = $"Hello {{{{ nodes.{node1Id}.text }}}} and {{{{ nodes.{node2Id}.text }}}}!"
        let expected = $"Hello {value1} and {value2}!"
        select new StringInterpolationTestCase(expression, outputs, expected);

    /// <summary>Generator for non-whitespace strings (at least one non-whitespace character).</summary>
    private static Gen<string> GenNonWhitespaceString =>
        from chars in Gen.Char['a', 'z'].Array[1, 15]
        select new string(chars);

    /// <summary>Generator for function call test cases.</summary>
    private static readonly Gen<FunctionCallTestCase> GenFunctionCall =
        Gen.OneOf(
            // toUpper function
            Gen.String[1, 20].Select(s => new FunctionCallTestCase(
                "toUpper",
                s,
                s.ToUpperInvariant())),
            // toLower function
            Gen.String[1, 20].Select(s => new FunctionCallTestCase(
                "toLower",
                s,
                s.ToLowerInvariant())),
            // trim function - use non-whitespace string to avoid edge case where s is all whitespace
            from s in GenNonWhitespaceString
            let padded = $"  {s}  "
            select new FunctionCallTestCase("trim", padded, s),
            // abs function
            Gen.Int[-100, 100].Select(i => new FunctionCallTestCase(
                "abs",
                i,
                Math.Abs(i)))
        );

    #endregion
}
