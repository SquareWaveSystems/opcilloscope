using System.Text.Json.Serialization;
using OpcScope.Configuration.Models;

namespace OpcScope.Configuration;

/// <summary>
/// Source-generated JSON serialization context for OpcScope configuration types.
/// This enables AOT/trimming-compatible JSON serialization without reflection.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpcScopeConfig))]
[JsonSerializable(typeof(List<string>))]
internal partial class OpcScopeJsonContext : JsonSerializerContext
{
}
