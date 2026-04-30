using Bunit;
using Bunit.TestDoubles;
using Vyshyvanka.Designer.Components;

namespace Vyshyvanka.Tests.Unit.Components;

public class NodeIconTests : BunitContext
{
    [Fact]
    public void WhenIconIsNullThenRendersDefaultEmoji()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, (string?)null));

        cut.Find(".node-icon-emoji").TextContent.Should().Be("📦");
    }

    [Fact]
    public void WhenIconIsEmptyThenRendersDefaultEmoji()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, ""));

        cut.Find(".node-icon-emoji").TextContent.Should().Be("📦");
    }

    [Fact]
    public void WhenCustomDefaultIconThenRendersIt()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, "")
            .Add(p => p.DefaultIcon, "⚡"));

        cut.Find(".node-icon-emoji").TextContent.Should().Be("⚡");
    }

    [Fact]
    public void WhenIconIsEmojiThenRendersAsEmoji()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, "🔥"));

        cut.Find(".node-icon-emoji").TextContent.Should().Be("🔥");
    }

    [Fact]
    public void WhenIconIsFontAwesomeThenRendersAsIElement()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, "fa-solid fa-bolt"));

        var icon = cut.Find("i");
        icon.ClassList.Should().Contain("fa-solid");
        icon.ClassList.Should().Contain("fa-bolt");
    }

    [Theory]
    [InlineData("fas fa-check")]
    [InlineData("far fa-circle")]
    [InlineData("fab fa-github")]
    [InlineData("fa-solid fa-gear")]
    public void WhenIconIsFontAwesomeVariantThenRendersAsIElement(string icon)
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, icon));

        cut.Find("i").Should().NotBeNull();
    }

    [Fact]
    public void WhenIconIsHttpUrlThenRendersAsImage()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, "https://example.com/icon.png"));

        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("https://example.com/icon.png");
        img.ClassList.Should().Contain("node-icon-img");
    }

    [Fact]
    public void WhenSizeProvidedThenAppliesSizeStyle()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, "🔥")
            .Add(p => p.Size, "24px"));

        var style = cut.Find(".node-icon-emoji").GetAttribute("style");
        style.Should().Contain("font-size: 24px");
        style.Should().Contain("width: 24px");
        style.Should().Contain("height: 24px");
    }

    [Fact]
    public void WhenNoSizeThenNoInlineStyle()
    {
        var cut = Render<NodeIcon>(parameters => parameters
            .Add(p => p.Icon, "🔥"));

        var style = cut.Find(".node-icon-emoji").GetAttribute("style");
        style.Should().BeEmpty();
    }
}
