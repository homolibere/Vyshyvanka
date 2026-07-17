using Bunit;
using Vyshyvanka.Designer.Components;
using Vyshyvanka.Designer.Services;
using Vyshyvanka.Designer.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Vyshyvanka.Tests.Unit.Components;

/// <summary>
/// Legacy ThemeToggle tests — kept for backward compatibility.
/// ThemeToggle has been replaced by ThemeSelector in layouts.
/// </summary>
public class ThemeToggleTests : BunitContext
{
    public ThemeToggleTests()
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
    public void WhenLightThemeThenShowsMoonIcon()
    {
        var cut = Render<ThemeToggle>();

        cut.Find("i").ClassList.Should().Contain("fa-moon");
    }

    [Fact]
    public void WhenRenderedThenHasToggleButton()
    {
        var cut = Render<ThemeToggle>();

        var button = cut.Find(".theme-toggle");
        button.Should().NotBeNull();
        button.GetAttribute("title").Should().Contain("dark");
    }

    private void SetupThemeFetch(string path, string id, string name, string baseMode)
    {
        var theme = new ThemeDefinition
        {
            Id = id,
            Name = name,
            BaseMode = baseMode,
            Colors = new() { ["bg-primary"] = "#fff" },
            Icons = new() { ["trigger"] = "fa-solid fa-bolt" }
        };
        var json = JsonSerializer.Serialize(theme);
        JSInterop.Setup<string?>("vyshyvankaTheme.fetchThemeJson", path).SetResult(json);
    }
}
