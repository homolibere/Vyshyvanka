using Bunit;
using Bunit.TestDoubles;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Tests.Unit.Components;

public class NumberPropertyEditorTests : BunitContext
{
    private static ConfigurationProperty CreateProperty(
        string name = "timeout",
        string displayName = "Timeout",
        bool isRequired = false,
        string? description = null) => new()
    {
        Name = name,
        DisplayName = displayName,
        Type = "number",
        IsRequired = isRequired,
        Description = description
    };

    [Fact]
    public void WhenRenderedThenDisplaysLabel()
    {
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(displayName: "Timeout (ms)"))
            .Add(p => p.Value, 5000));

        cut.Find(".property-label").TextContent.Should().Contain("Timeout (ms)");
    }

    [Fact]
    public void WhenValueProvidedThenInputShowsValue()
    {
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, 42));

        cut.Find(".property-input").GetAttribute("value").Should().Be("42");
    }

    [Fact]
    public void WhenValidNumberEnteredThenInvokesValueChanged()
    {
        object? newValue = null;
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, (object?)null)
            .Add(p => p.ValueChanged, (object? v) => { newValue = v; }));

        cut.Find(".property-input").Input("123");

        newValue.Should().Be(123);
    }

    [Fact]
    public void WhenInvalidNumberEnteredThenShowsValidationError()
    {
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, (object?)null));

        cut.Find(".property-input").Input("not-a-number");

        cut.Find(".validation-error").TextContent.Should().Contain("valid number");
    }

    [Fact]
    public void WhenRequiredThenShowsRequiredIndicator()
    {
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(isRequired: true))
            .Add(p => p.Value, (object?)null));

        cut.Find(".required-indicator").TextContent.Should().Be("*");
    }

    [Fact]
    public void WhenDescriptionProvidedThenRendersIt()
    {
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(description: "Timeout in milliseconds"))
            .Add(p => p.Value, 5000));

        cut.Find(".property-description").TextContent.Should().Be("Timeout in milliseconds");
    }

    [Fact]
    public void WhenShowValidationErrorThenRendersRequiredMessage()
    {
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(isRequired: true))
            .Add(p => p.Value, (object?)null)
            .Add(p => p.ShowValidationError, true));

        cut.Find(".validation-error").TextContent.Should().Contain("required");
    }

    [Fact]
    public void WhenDecimalEnteredThenParsesAsDouble()
    {
        object? newValue = null;
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, (object?)null)
            .Add(p => p.ValueChanged, (object? v) => { newValue = v; }));

        cut.Find(".property-input").Input("3.14");

        newValue.Should().Be(3.14);
    }

    [Fact]
    public void WhenEmptyInputThenSendsNull()
    {
        object? newValue = "not-null";
        var cut = Render<NumberPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, 42)
            .Add(p => p.ValueChanged, (object? v) => { newValue = v; }));

        cut.Find(".property-input").Input("");

        newValue.Should().BeNull();
    }
}
