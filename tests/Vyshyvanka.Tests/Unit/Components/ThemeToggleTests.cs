using Bunit;
using Bunit.TestDoubles;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Vyshyvanka.Tests.Unit.Components;

public class ThemeToggleTests : BunitContext
{
    public ThemeToggleTests()
    {
        // ThemeService needs IJSRuntime — bUnit provides a fake one
        JSInterop.SetupVoid("document.documentElement.setAttribute", _ => true);
        JSInterop.SetupVoid("localStorage.setItem", _ => true);
        JSInterop.Setup<string?>("localStorage.getItem", "vyshyvanka-theme").SetResult("light");
        JSInterop.Setup<string?>("localStorage.getItem", "vyshyvanka-canvas-pattern").SetResult("vyshyvanka");

        Services.AddSingleton(new ThemeService(JSInterop.JSRuntime));
    }

    [Fact]
    public void WhenLightThemeThenShowsMoonIcon()
    {
        var cut = Render<ThemeToggle>();

        cut.Find("i").ClassList.Should().Contain("fa-moon");
    }

    [Fact]
    public async Task WhenToggledThenSwitchesToDarkTheme()
    {
        var themeService = Services.GetRequiredService<ThemeService>();
        var cut = Render<ThemeToggle>();

        // Click the toggle button
        await cut.Find(".theme-toggle").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        themeService.IsDark.Should().BeTrue();
        cut.Find("i").ClassList.Should().Contain("fa-sun");
    }

    [Fact]
    public void WhenRenderedThenHasToggleButton()
    {
        var cut = Render<ThemeToggle>();

        var button = cut.Find(".theme-toggle");
        button.Should().NotBeNull();
        button.GetAttribute("title").Should().Contain("dark");
    }
}
