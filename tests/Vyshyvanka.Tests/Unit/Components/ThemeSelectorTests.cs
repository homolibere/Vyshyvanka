using Bunit;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Designer.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Vyshyvanka.Tests.Unit.Components;

public class ThemeSelectorTests : BunitContext
{
    public ThemeSelectorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        SetupThemeFetch("themes/vyshyvanka-light.json", "vyshyvanka-light", "Vyshyvanka Light", "light");
        SetupThemeFetch("themes/vyshyvanka-dark.json", "vyshyvanka-dark", "Vyshyvanka Dark", "dark");
        SetupThemeFetch("themes/slate.json", "slate", "Slate", "light");
        SetupThemeFetch("themes/ocean-dark.json", "ocean-dark", "Ocean Dark", "dark");
        SetupThemeFetch("themes/minimal.json", "minimal", "Minimal", "light");

        JSInterop.Setup<string?>("vyshyvankaTheme.getActiveThemeId").SetResult("vyshyvanka-light");
        JSInterop.Setup<JsonElement[]>("vyshyvankaTheme.getCustomThemes").SetResult([]);

        var themeService = new ThemeService(JSInterop.JSRuntime);
        themeService.InitializeAsync().GetAwaiter().GetResult();

        Services.AddSingleton(themeService);
    }

    [Fact]
    public void WhenRenderedThenShowsActiveThemeName()
    {
        var cut = Render<ThemeSelector>();

        cut.Find(".theme-name").TextContent.Should().Contain("Vyshyvanka Light");
    }

    [Fact]
    public void WhenClickedThenOpensDropdown()
    {
        var cut = Render<ThemeSelector>();

        cut.Find(".theme-selector-trigger").Click();

        cut.FindAll(".theme-option").Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task WhenThemeSelectedThenUpdatesActiveTheme()
    {
        var themeService = Services.GetRequiredService<ThemeService>();
        var cut = Render<ThemeSelector>();

        // Open dropdown
        cut.Find(".theme-selector-trigger").Click();

        // Select second option (vyshyvanka-dark)
        var options = cut.FindAll(".theme-option");
        if (options.Count > 1)
        {
            await options[1].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
            themeService.CurrentThemeId.Should().NotBe("vyshyvanka-light");
        }
    }

    private void SetupThemeFetch(string path, string id, string name, string baseMode)
    {
        var theme = new ThemeDefinition
        {
            Id = id,
            Name = name,
            BaseMode = baseMode,
            Preview = new ThemePreview { Bg = "#fff", Accent = "#000", Surface = "#eee" },
            Colors = new Dictionary<string, string> { ["bg-primary"] = "#fff" },
            Icons = new Dictionary<string, string> { ["trigger"] = "fa-solid fa-bolt" },
            Canvas = new ThemeCanvas { Pattern = "dots" }
        };
        var json = JsonSerializer.Serialize(theme);
        JSInterop.Setup<string?>("vyshyvankaTheme.fetchThemeJson", path).SetResult(json);
    }
}
