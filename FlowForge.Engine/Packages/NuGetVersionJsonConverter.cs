using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace FlowForge.Engine.Packages;

/// <summary>
/// JSON converter for NuGetVersion, serializing as a normalized version string.
/// </summary>
public class NuGetVersionJsonConverter : JsonConverter<NuGetVersion>
{
    public override NuGetVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : NuGetVersion.Parse(value);
    }

    public override void Write(Utf8JsonWriter writer, NuGetVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToNormalizedString());
    }
}
