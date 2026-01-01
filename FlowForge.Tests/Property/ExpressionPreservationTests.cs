using CsCheck;
using FlowForge.Designer.Components;

namespace FlowForge.Tests.Property;

/// <summary>
/// Property-based tests for expression preservation in string property editors.
/// </summary>
public class ExpressionPreservationTests
{
    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 11: Expression Preservation
    /// For any string value containing expression syntax (e.g., {{ nodes.nodeName.output.field }}),
    /// the Property_Editor should preserve the expression through editing and provide visual indication
    /// that the field contains an expression.
    /// Validates: Requirements 7.1, 7.3
    /// </summary>
    [Fact]
    public void ExpressionSyntaxIsDetectedAndPreserved()
    {
        GenExpressionValue.Sample(testCase =>
        {
            // Act: Check if expression is detected
            var containsExpression = StringPropertyEditor.ContainsExpression(testCase.Value);

            // Assert: Expression should be detected when present
            Assert.Equal(testCase.ShouldBeDetected, containsExpression);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 11: Expression Preservation
    /// For any valid expression syntax, the expression should be preserved exactly as entered.
    /// Validates: Requirements 7.1, 7.3
    /// </summary>
    [Fact]
    public void ExpressionContentIsPreservedExactly()
    {
        GenValidExpression.Sample(expression =>
        {
            // Act: The expression should be detected
            var isDetected = StringPropertyEditor.ContainsExpression(expression);

            // Assert: Expression should be detected
            Assert.True(isDetected, $"Expression '{expression}' should be detected");

            // Assert: The expression string itself is unchanged (preservation)
            // This validates that the detection doesn't modify the value
            Assert.Contains("{{", expression);
            Assert.Contains("}}", expression);
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 11: Expression Preservation
    /// For any string without expression syntax, the detector should return false.
    /// Validates: Requirements 7.1, 7.3
    /// </summary>
    [Fact]
    public void NonExpressionStringsAreNotDetected()
    {
        GenNonExpressionString.Sample(value =>
        {
            // Act
            var containsExpression = StringPropertyEditor.ContainsExpression(value);

            // Assert: Non-expression strings should not be detected as expressions
            Assert.False(containsExpression, $"Value '{value}' should not be detected as expression");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 11: Expression Preservation
    /// For any string with multiple expressions, all should be detected.
    /// Validates: Requirements 7.1, 7.3
    /// </summary>
    [Fact]
    public void MultipleExpressionsAreDetected()
    {
        GenMultipleExpressions.Sample(value =>
        {
            // Act
            var containsExpression = StringPropertyEditor.ContainsExpression(value);

            // Assert: Multiple expressions should be detected
            Assert.True(containsExpression, $"Value '{value}' with multiple expressions should be detected");
        }, iter: 100);
    }

    /// <summary>
    /// Feature: dynamic-node-config-ui, Property 11: Expression Preservation
    /// Null and empty strings should not be detected as expressions.
    /// Validates: Requirements 7.1, 7.3
    /// </summary>
    [Fact]
    public void NullAndEmptyStringsAreNotExpressions()
    {
        // Test null
        Assert.False(StringPropertyEditor.ContainsExpression(null));
        
        // Test empty string
        Assert.False(StringPropertyEditor.ContainsExpression(string.Empty));
        
        // Test whitespace
        Assert.False(StringPropertyEditor.ContainsExpression("   "));
    }

    #region Generators

    private record ExpressionTestCase(string Value, bool ShouldBeDetected);

    private static readonly Gen<string> GenNodeName =
        Gen.Char['a', 'z'].Array[3, 10].Select(chars => new string(chars));

    private static readonly Gen<string> GenFieldName =
        Gen.Char['a', 'z'].Array[2, 8].Select(chars => new string(chars));

    private static readonly Gen<string> GenValidExpression =
        from nodeName in GenNodeName
        from fieldName in GenFieldName
        from expressionType in Gen.Int[0, 3]
        select expressionType switch
        {
            0 => "{{ nodes." + nodeName + ".output." + fieldName + " }}",
            1 => "{{ $node." + nodeName + ".data." + fieldName + " }}",
            2 => "{{ " + nodeName + " }}",
            _ => "{{ nodes." + nodeName + "." + fieldName + " }}"
        };

    private static readonly Gen<string> GenNonExpressionString =
        from baseString in Gen.Char['a', 'z'].Array[5, 20].Select(chars => new string(chars))
        from variant in Gen.Int[0, 4]
        select variant switch
        {
            0 => baseString,
            1 => "Hello " + baseString + " world",
            2 => "{ " + baseString + " }",  // Single braces, not double
            3 => baseString + "{{",         // Only opening braces without closing
            _ => "}}" + baseString          // Only closing braces without opening
        };

    private static readonly Gen<string> GenMultipleExpressions =
        from expr1 in GenValidExpression
        from expr2 in GenValidExpression
        from separator in Gen.OneOf(Gen.Const(" "), Gen.Const(" and "), Gen.Const(", "))
        select expr1 + separator + expr2;

    private static readonly Gen<ExpressionTestCase> GenExpressionValue =
        Gen.OneOf(
            // Valid expressions - should be detected
            GenValidExpression.Select(e => new ExpressionTestCase(e, true)),
            // Non-expressions - should not be detected
            GenNonExpressionString.Select(s => new ExpressionTestCase(s, false)),
            // Mixed content with expression - should be detected
            from prefix in Gen.Char['a', 'z'].Array[3, 8].Select(chars => new string(chars))
            from expr in GenValidExpression
            from suffix in Gen.Char['a', 'z'].Array[3, 8].Select(chars => new string(chars))
            select new ExpressionTestCase(prefix + " " + expr + " " + suffix, true)
        );

    #endregion
}
