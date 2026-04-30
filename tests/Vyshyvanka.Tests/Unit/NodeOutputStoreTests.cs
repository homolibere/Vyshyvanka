using System.Text.Json;
using Vyshyvanka.Engine.Execution;

namespace Vyshyvanka.Tests.Unit;

public class NodeOutputStoreTests
{
    private readonly NodeOutputStore _sut = new();

    private static JsonElement ToJson(object value) =>
        JsonSerializer.SerializeToElement(value);

    [Fact]
    public void WhenSettingOutputThenGetReturnsIt()
    {
        var data = ToJson(new { result = "success" });

        _sut.Set("node-1", data);

        var output = _sut.Get("node-1");
        output.Should().NotBeNull();
        output!.Value.GetProperty("result").GetString().Should().Be("success");
    }

    [Fact]
    public void WhenGettingNonexistentNodeThenReturnsNull()
    {
        var output = _sut.Get("nonexistent");

        output.Should().BeNull();
    }

    [Fact]
    public void WhenSettingPortSpecificOutputThenGetByPortReturnsIt()
    {
        var data = ToJson(new { branch = "true" });

        _sut.Set("node-1", "truePort", data);

        var output = _sut.Get("node-1", "truePort");
        output.Should().NotBeNull();
        output!.Value.GetProperty("branch").GetString().Should().Be("true");
    }

    [Fact]
    public void WhenGettingWrongPortThenReturnsNull()
    {
        _sut.Set("node-1", "portA", ToJson("data"));

        var output = _sut.Get("node-1", "portB");

        output.Should().BeNull();
    }

    [Fact]
    public void WhenCheckingHasOutputForExistingNodeThenReturnsTrue()
    {
        _sut.Set("node-1", ToJson("data"));

        _sut.HasOutput("node-1").Should().BeTrue();
    }

    [Fact]
    public void WhenCheckingHasOutputForMissingNodeThenReturnsFalse()
    {
        _sut.HasOutput("nonexistent").Should().BeFalse();
    }

    [Fact]
    public void WhenCheckingHasOutputForSpecificPortThenReturnsCorrectly()
    {
        _sut.Set("node-1", "portA", ToJson("data"));

        _sut.HasOutput("node-1", "portA").Should().BeTrue();
        _sut.HasOutput("node-1", "portB").Should().BeFalse();
    }

    [Fact]
    public void WhenGettingAllOutputsThenReturnsAllPorts()
    {
        _sut.Set("node-1", "portA", ToJson("dataA"));
        _sut.Set("node-1", "portB", ToJson("dataB"));

        var allOutputs = _sut.GetAllOutputs("node-1");

        allOutputs.Should().HaveCount(2);
        allOutputs.Should().ContainKey("portA");
        allOutputs.Should().ContainKey("portB");
    }

    [Fact]
    public void WhenGettingAllOutputsForMissingNodeThenReturnsEmpty()
    {
        var allOutputs = _sut.GetAllOutputs("nonexistent");

        allOutputs.Should().BeEmpty();
    }

    [Fact]
    public void WhenOverwritingOutputThenLatestValueWins()
    {
        _sut.Set("node-1", ToJson("first"));
        _sut.Set("node-1", ToJson("second"));

        var output = _sut.Get("node-1");
        output.Should().NotBeNull();
        output!.Value.GetString().Should().Be("second");
    }

    [Fact]
    public void WhenNodeIdIsNullOrEmptyThenThrowsArgumentException()
    {
        var act = () => _sut.Set("", ToJson("data"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WhenPortNameIsNullOrEmptyThenThrowsArgumentException()
    {
        var act = () => _sut.Set("node-1", "", ToJson("data"));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WhenNodeIdIsCaseInsensitiveThenMatchesCorrectly()
    {
        _sut.Set("MyNode", ToJson("data"));

        _sut.HasOutput("mynode").Should().BeTrue();
        _sut.Get("MYNODE").Should().NotBeNull();
    }
}
