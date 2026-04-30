using Bunit;
using Bunit.TestDoubles;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Tests.Unit.Components;

public class StringPropertyEditorTests : BunitContext
{
    private static ConfigurationProperty CreateProperty(
        string name = "url",
        string displayName = "URL",
        bool isRequired = false,
        string? description = null) => new()
    {
        Name = name,
        DisplayName = displayName,
        Type = "string",
        IsRequired = isRequired,
        Description = description
    };

    [Fact]
    public void WhenRenderedThenDisplaysLabel()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(displayName: "API URL"))
            .Add(p => p.Value, ""));

        cut.Find(".property-label").TextContent.Should().Contain("API URL");
    }

    [Fact]
    public void WhenRequiredThenShowsRequiredIndicator()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(isRequired: true))
            .Add(p => p.Value, ""));

        cut.Find(".required-indicator").TextContent.Should().Be("*");
    }

    [Fact]
    public void WhenNotRequiredThenNoRequiredIndicator()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(isRequired: false))
            .Add(p => p.Value, ""));

        cut.FindAll(".required-indicator").Should().BeEmpty();
    }

    [Fact]
    public void WhenValueContainsExpressionThenShowsExpressionIndicator()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, "{{ nodes.http.data.url }}"));

        cut.FindAll(".expression-indicator").Should().NotBeEmpty();
        cut.Find(".property-input").ClassList.Should().Contain("has-expression");
    }

    [Fact]
    public void WhenValueHasNoExpressionThenNoExpressionIndicator()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, "https://api.example.com"));

        cut.FindAll(".expression-indicator").Should().BeEmpty();
    }

    [Fact]
    public void WhenDescriptionProvidedThenRendersIt()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(description: "The target URL"))
            .Add(p => p.Value, ""));

        cut.Find(".property-description").TextContent.Should().Be("The target URL");
    }

    [Fact]
    public void WhenShowValidationErrorThenRendersError()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(isRequired: true))
            .Add(p => p.Value, "")
            .Add(p => p.ShowValidationError, true));

        cut.Find(".validation-error").TextContent.Should().Be("This field is required");
        cut.Find(".property-input").ClassList.Should().Contain("has-error");
    }

    [Fact]
    public void WhenInputChangedThenInvokesValueChanged()
    {
        object? newValue = null;
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, "")
            .Add(p => p.ValueChanged, (object? v) => { newValue = v; }));

        cut.Find(".property-input").Input("https://new-url.com");

        newValue.Should().Be("https://new-url.com");
    }

    [Fact]
    public void WhenValueProvidedThenInputShowsValue()
    {
        var cut = Render<StringPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, "existing-value"));

        cut.Find(".property-input").GetAttribute("value").Should().Be("existing-value");
    }

    // --- Static method tests ---

    [Theory]
    [InlineData("{{ nodes.http.data }}", true)]
    [InlineData("prefix {{ nodes.x.y }} suffix", true)]
    [InlineData("no expressions here", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("{{ }}", true)]
    public void WhenCheckingContainsExpressionThenReturnsCorrectly(string? value, bool expected)
    {
        StringPropertyEditor.ContainsExpression(value).Should().Be(expected);
    }
}
