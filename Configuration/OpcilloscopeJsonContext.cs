using System.Text.Json.Serialization;
using Opcilloscope.Configuration.Models;

namespace Opcilloscope.Configuration;

/// <summary>
/// Source-generated JSON serialization context for Opcilloscope configuration types.
/// This enables AOT/trimming-compatible JSON serialization without reflection.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpcilloscopeConfig))]
[JsonSerializable(typeof(List<string>))]
internal partial class OpcilloscopeJsonContext : JsonSerializerContext
{
}
