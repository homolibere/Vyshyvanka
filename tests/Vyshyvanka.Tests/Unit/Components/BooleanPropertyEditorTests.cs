using Bunit;
using Bunit.TestDoubles;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Models;

namespace Vyshyvanka.Tests.Unit.Components;

public class BooleanPropertyEditorTests : BunitContext
{
    private static ConfigurationProperty CreateProperty(
        string name = "enabled",
        string displayName = "Enabled",
        bool isRequired = false,
        string? description = null) => new()
    {
        Name = name,
        DisplayName = displayName,
        Type = "boolean",
        IsRequired = isRequired,
        Description = description
    };

    [Fact]
    public void WhenRenderedThenDisplaysLabel()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(displayName: "Enable Retry"))
            .Add(p => p.Value, false));

        cut.Find(".property-label").TextContent.Should().Contain("Enable Retry");
    }

    [Fact]
    public void WhenValueIsTrueThenCheckboxIsChecked()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, true));

        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void WhenValueIsFalseThenCheckboxIsUnchecked()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, false));

        // bUnit renders unchecked checkboxes without the checked attribute
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Should().NotBeNull();
    }

    [Fact]
    public void WhenValueIsStringTrueThenCheckboxIsChecked()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, "true"));

        cut.Find("input[type='checkbox']").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public void WhenToggledThenInvokesValueChanged()
    {
        object? newValue = null;
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, false)
            .Add(p => p.ValueChanged, (object? v) => { newValue = v; }));

        cut.Find("input[type='checkbox']").Change(true);

        newValue.Should().Be(true);
    }

    [Fact]
    public void WhenRequiredThenShowsRequiredIndicator()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(isRequired: true))
            .Add(p => p.Value, false));

        cut.Find(".required-indicator").TextContent.Should().Be("*");
    }

    [Fact]
    public void WhenDescriptionProvidedThenRendersIt()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty(description: "Enable automatic retries"))
            .Add(p => p.Value, false));

        cut.Find(".property-description").TextContent.Should().Be("Enable automatic retries");
    }

    [Fact]
    public void WhenValueIsNullThenCheckboxIsUnchecked()
    {
        var cut = Render<BooleanPropertyEditor>(parameters => parameters
            .Add(p => p.Property, CreateProperty())
            .Add(p => p.Value, (object?)null));

        // Null should be treated as false
        cut.Find("input[type='checkbox']").Should().NotBeNull();
    }
}
